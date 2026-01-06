using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CanteenRFID.Core.Enums;
using CanteenRFID.Core.Models;
using CanteenRFID.Core.Services;
using CanteenRFID.Data.Contexts;
using CanteenRFID.Data.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs", "web.log"), rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));
builder.Services.Configure<TimezoneOptions>(builder.Configuration.GetSection("Timezone"));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "canteen.db");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IApiKeyHasher, Sha256ApiKeyHasher>();
builder.Services.AddScoped<MealRuleEngineFactory>();
builder.Services.AddScoped<StampService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DataSeeder.SeedAsync(db);
    var adminOptions = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetSection("Admin").Get<AdminOptions>() ?? new AdminOptions();
    Console.WriteLine($"Admin Login -> User: {adminOptions.Username}, Password: {adminOptions.Password}");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/api/v1/stamps", async ([FromBody] StampRequest request, HttpContext context, ApplicationDbContext db, IApiKeyHasher hasher, IConfiguration configuration) =>
{
    if (!context.Request.Headers.TryGetValue("X-API-KEY", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Unauthorized();
    }

    var reader = await db.Readers.FirstOrDefaultAsync(r => r.ApiKeyHash == hasher.Hash(apiKey!) && r.IsActive);
    if (reader is null)
    {
        return Results.Unauthorized();
    }

    var tzOptions = configuration.GetSection("Timezone").Get<TimezoneOptions>() ?? new TimezoneOptions();
    var tz = TimeZoneInfo.FindSystemTimeZoneById(tzOptions.Windows ?? "W. Europe Standard Time");
    var timestampUtc = request.TimestampUtc ?? DateTime.UtcNow;
    var timestampLocal = TimeZoneInfo.ConvertTimeFromUtc(timestampUtc, tz);

    var engine = new MealRuleEngine(await db.MealRules.Where(r => r.IsActive).ToListAsync());
    var mealType = engine.ResolveMealType(timestampLocal);

    var user = await db.Users.FirstOrDefaultAsync(u => u.Uid == request.Uid);

    var stamp = new Stamp
    {
        TimestampUtc = timestampUtc,
        TimestampLocal = timestampLocal,
        UidRaw = request.Uid,
        ReaderId = request.ReaderId,
        MealType = mealType,
        UserId = user?.Id,
        CreatedAtUtc = DateTime.UtcNow
    };

    db.Stamps.Add(stamp);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/stamps/{stamp.Id}", stamp);
}).WithTags("Stamps");

app.MapGet("/api/v1/stamps", async (DateTime? from, DateTime? to, Guid? userId, string? uid, string? readerId, MealType? mealType, ApplicationDbContext db) =>
{
    var query = db.Stamps.Include(s => s.User).AsQueryable();
    if (from.HasValue) query = query.Where(s => s.TimestampUtc >= from);
    if (to.HasValue) query = query.Where(s => s.TimestampUtc <= to);
    if (userId.HasValue) query = query.Where(s => s.UserId == userId);
    if (!string.IsNullOrWhiteSpace(uid)) query = query.Where(s => s.UidRaw == uid);
    if (!string.IsNullOrWhiteSpace(readerId)) query = query.Where(s => s.ReaderId == readerId);
    if (mealType.HasValue) query = query.Where(s => s.MealType == mealType);
    var items = await query.OrderByDescending(s => s.TimestampUtc).Take(200).ToListAsync();
    return Results.Ok(items);
});

app.Run();

public record StampRequest(string Uid, string ReaderId, DateTime? TimestampUtc, Dictionary<string, string>? Meta);

public record AdminOptions
{
    public string Username { get; init; } = "admin";
    public string Password { get; init; } = "ChangeMe123!";
    public string Role { get; init; } = "Admin";
}

public record TimezoneOptions
{
    public string Windows { get; init; } = "W. Europe Standard Time";
}

public interface IApiKeyHasher
{
    string Hash(string apiKey);
}

public class Sha256ApiKeyHasher : IApiKeyHasher
{
    public string Hash(string apiKey)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes);
    }
}

public class MealRuleEngineFactory
{
    private readonly ApplicationDbContext _db;

    public MealRuleEngineFactory(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<MealRuleEngine> CreateAsync()
    {
        var rules = await _db.MealRules.Where(r => r.IsActive).ToListAsync();
        return new MealRuleEngine(rules);
    }
}

public class StampService
{
    private readonly ApplicationDbContext _db;
    private readonly MealRuleEngineFactory _engineFactory;
    private readonly IConfiguration _configuration;

    public StampService(ApplicationDbContext db, MealRuleEngineFactory engineFactory, IConfiguration configuration)
    {
        _db = db;
        _engineFactory = engineFactory;
        _configuration = configuration;
    }

    public async Task<Stamp> AddStampAsync(string uid, string readerId, DateTime? timestampUtc)
    {
        var tzOptions = _configuration.GetSection("Timezone").Get<TimezoneOptions>() ?? new TimezoneOptions();
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tzOptions.Windows ?? "W. Europe Standard Time");
        var utc = timestampUtc ?? DateTime.UtcNow;
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        var engine = await _engineFactory.CreateAsync();
        var mealType = engine.ResolveMealType(local);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Uid == uid);
        var stamp = new Stamp
        {
            TimestampUtc = utc,
            TimestampLocal = local,
            UidRaw = uid,
            ReaderId = readerId,
            MealType = mealType,
            UserId = user?.Id,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Stamps.Add(stamp);
        await _db.SaveChangesAsync();
        return stamp;
    }
}
