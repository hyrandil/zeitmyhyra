using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using CanteenRFID.Core.Enums;
using CanteenRFID.Core.Models;
using CanteenRFID.Core.Services;
using CanteenRFID.Data.Contexts;
using CanteenRFID.Data.Services;
using CanteenRFID.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, 5000);
});

QuestPDF.Settings.License = LicenseType.Community;

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
builder.Services.AddSingleton<AdminCredentialStore>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(AdminCredentialStore.AdminRole));
});

var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "canteen.db");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddControllersWithViews().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IApiKeyHasher, Sha256ApiKeyHasher>();
builder.Services.AddScoped<MealRuleEngineFactory>();
builder.Services.AddScoped<StampService>();
builder.Services.AddScoped<ApiKeyValidator>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DataSeeder.SeedAsync(db);
    var store = scope.ServiceProvider.GetRequiredService<AdminCredentialStore>();
    var existed = store.SecretExists;
    var adminOptions = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetSection("Admin").Get<AdminOptions>() ?? new AdminOptions();
    var creds = await store.EnsureAsync();
    if (!existed)
    {
        Console.WriteLine($"Admin Login -> User: {creds.Username}, Password: {adminOptions.Password}");
    }
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

app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        if (ctx.User?.Identity?.IsAuthenticated == true)
        {
            ctx.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            ctx.Response.Headers["Pragma"] = "no-cache";
            ctx.Response.Headers["Expires"] = "0";
        }

        return Task.CompletedTask;
    });

    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI();

var api = app.MapGroup("/api/v1");

api.MapPost("/stamps", async ([FromBody] StampRequest request, HttpContext context, ApiKeyValidator validator, StampService stampService) =>
{
    if (!context.Request.Headers.TryGetValue("X-API-KEY", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Unauthorized();
    }

    var reader = await validator.ValidateAsync(apiKey!);
    if (reader is null)
    {
        return Results.Unauthorized();
    }

    var saved = await stampService.AddStampAsync(request.Uid, request.ReaderId ?? reader.ReaderId, request.TimestampUtc);
    return Results.Created($"/api/v1/stamps/{saved.Id}", saved);
}).WithTags("Stamps");

api.MapGet("/stamps", async ([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? userId, [FromQuery] string? uid, [FromQuery] string? readerId, [FromQuery] MealType? mealType, [FromQuery] string? search, ApplicationDbContext db) =>
{
    var query = db.Stamps.Include(s => s.User).AsQueryable();
    if (from.HasValue) query = query.Where(s => s.TimestampUtc >= from);
    if (to.HasValue) query = query.Where(s => s.TimestampUtc <= to);
    if (userId.HasValue) query = query.Where(s => s.UserId == userId);
    if (!string.IsNullOrWhiteSpace(uid)) query = query.Where(s => s.UidRaw == uid);
    if (!string.IsNullOrWhiteSpace(readerId)) query = query.Where(s => s.ReaderId == readerId);
    if (mealType.HasValue) query = query.Where(s => s.MealType == mealType);
    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(s => s.UidRaw.Contains(search) || s.ReaderId.Contains(search) || (s.User != null && (s.User.FirstName + " " + s.User.LastName).Contains(search)));
    }

    var items = await query.OrderByDescending(s => s.TimestampUtc).Take(500).ToListAsync();
    return Results.Ok(items);
}).RequireAuthorization().WithTags("Stamps");

api.MapDelete("/stamps/{id:guid}", async (Guid id, ApplicationDbContext db) =>
{
    var stamp = await db.Stamps.FindAsync(id);
    if (stamp == null) return Results.NotFound();
    db.Stamps.Remove(stamp);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("AdminOnly").WithTags("Stamps");

api.MapPost("/readers/ping", async (HttpContext context, ApiKeyValidator validator, ApplicationDbContext db, [FromBody] ReaderPingRequest request) =>
{
    if (!context.Request.Headers.TryGetValue("X-API-KEY", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Unauthorized();
    }

    var reader = await validator.ValidateAsync(apiKey!);
    if (reader is null)
    {
        return Results.Unauthorized();
    }

    if (!string.IsNullOrWhiteSpace(request.ReaderId) && !string.Equals(request.ReaderId, reader.ReaderId, StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest("ReaderId stimmt nicht mit API-Key überein.");
    }

    reader.LastPingUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new { status = "ok", serverTimeUtc = DateTime.UtcNow });
}).WithTags("Readers");

api.MapGet("/users", async ([FromQuery] string? search, [FromQuery] bool? activeOnly, ApplicationDbContext db) =>
{
    var query = db.Users.AsQueryable();
    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(u => u.FirstName.Contains(search) || u.LastName.Contains(search) || u.PersonnelNo.Contains(search) || (u.Uid != null && u.Uid.Contains(search)));
    }

    if (activeOnly == true)
    {
        query = query.Where(u => u.IsActive);
    }

    var items = await query.OrderBy(u => u.LastName).ToListAsync();
    return Results.Ok(items);
}).RequireAuthorization().WithTags("Users");

api.MapPost("/users", async ([FromBody] User user, ApplicationDbContext db) =>
{
    if (await db.Users.AnyAsync(u => u.PersonnelNo == user.PersonnelNo))
    {
        return Results.BadRequest("Personalnummer bereits vergeben");
    }
    if (!string.IsNullOrWhiteSpace(user.Uid) && await db.Users.AnyAsync(u => u.Uid == user.Uid))
    {
        return Results.BadRequest("UID bereits vergeben");
    }
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/users/{user.Id}", user);
}).RequireAuthorization("AdminOnly").WithTags("Users");

api.MapPut("/users/{id:guid}", async (Guid id, [FromBody] User user, ApplicationDbContext db) =>
{
    var existing = await db.Users.FindAsync(id);
    if (existing == null) return Results.NotFound();
    if (await db.Users.AnyAsync(u => u.PersonnelNo == user.PersonnelNo && u.Id != id))
    {
        return Results.BadRequest("Personalnummer bereits vergeben");
    }
    if (!string.IsNullOrWhiteSpace(user.Uid) && await db.Users.AnyAsync(u => u.Uid == user.Uid && u.Id != id))
    {
        return Results.BadRequest("UID bereits vergeben");
    }
    existing.FirstName = user.FirstName;
    existing.LastName = user.LastName;
    existing.PersonnelNo = user.PersonnelNo;
    existing.Uid = user.Uid;
    existing.IsActive = user.IsActive;
    await db.SaveChangesAsync();
    return Results.Ok(existing);
}).RequireAuthorization("AdminOnly").WithTags("Users");

api.MapDelete("/users/{id:guid}", async (Guid id, ApplicationDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user == null) return Results.NotFound();
    db.Users.Remove(user);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("AdminOnly").WithTags("Users");

api.MapGet("/mealrules", async (ApplicationDbContext db) =>
{
    var rules = await db.MealRules.OrderByDescending(r => r.Priority).ToListAsync();
    return Results.Ok(rules);
}).RequireAuthorization().WithTags("MealRules");

api.MapPost("/mealrules", async ([FromBody] MealRule rule, ApplicationDbContext db) =>
{
    db.MealRules.Add(rule);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/mealrules/{rule.Id}", rule);
}).RequireAuthorization("AdminOnly").WithTags("MealRules");

api.MapPut("/mealrules/{id:guid}", async (Guid id, [FromBody] MealRule rule, ApplicationDbContext db) =>
{
    var existing = await db.MealRules.FindAsync(id);
    if (existing == null) return Results.NotFound();
    existing.Name = rule.Name;
    existing.MealType = rule.MealType;
    existing.StartTimeLocal = rule.StartTimeLocal;
    existing.EndTimeLocal = rule.EndTimeLocal;
    existing.Priority = rule.Priority;
    existing.DaysOfWeekMask = rule.DaysOfWeekMask;
    existing.IsActive = rule.IsActive;
    await db.SaveChangesAsync();
    return Results.Ok(existing);
}).RequireAuthorization("AdminOnly").WithTags("MealRules");

api.MapDelete("/mealrules/{id:guid}", async (Guid id, ApplicationDbContext db) =>
{
    var rule = await db.MealRules.FindAsync(id);
    if (rule == null) return Results.NotFound();
    db.MealRules.Remove(rule);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("AdminOnly").WithTags("MealRules");

api.MapPost("/mealrules/recalculate", async ([FromBody] RecalculateRequest model, ApplicationDbContext db) =>
{
    var engine = new MealRuleEngine(await db.MealRules.Where(r => r.IsActive).ToListAsync());
    var stamps = await db.Stamps.Where(s => s.TimestampUtc >= model.From && s.TimestampUtc <= model.To).ToListAsync();
    foreach (var stamp in stamps)
    {
        stamp.MealType = engine.ResolveMealType(stamp.TimestampLocal);
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { Updated = stamps.Count });
}).RequireAuthorization("AdminOnly").WithTags("MealRules");

api.MapGet("/readers", async (ApplicationDbContext db) =>
{
    var readers = await db.Readers.OrderBy(r => r.ReaderId).ToListAsync();
    return Results.Ok(readers);
}).RequireAuthorization().WithTags("Readers");

api.MapPost("/readers", async ([FromBody] Reader reader, IApiKeyHasher hasher, ApplicationDbContext db) =>
{
    var apiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    reader.ApiKeyHash = hasher.Hash(apiKey);
    db.Readers.Add(reader);
    await db.SaveChangesAsync();
    return Results.Ok(new { reader.Id, ApiKey = apiKey });
}).RequireAuthorization("AdminOnly").WithTags("Readers");

api.MapPost("/readers/{id:guid}/regenerate", async (Guid id, IApiKeyHasher hasher, ApplicationDbContext db) =>
{
    var reader = await db.Readers.FindAsync(id);
    if (reader == null) return Results.NotFound();
    var apiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    reader.ApiKeyHash = hasher.Hash(apiKey);
    await db.SaveChangesAsync();
    return Results.Ok(new { reader.Id, ApiKey = apiKey });
}).RequireAuthorization("AdminOnly").WithTags("Readers");

api.MapPut("/readers/{id:guid}", async (Guid id, [FromBody] ReaderUpdateRequest update, ApplicationDbContext db) =>
{
    var reader = await db.Readers.FindAsync(id);
    if (reader == null) return Results.NotFound();
    reader.ReaderId = update.ReaderId;
    reader.Name = update.Name;
    reader.Location = update.Location;
    reader.IsActive = update.IsActive;
    await db.SaveChangesAsync();
    return Results.Ok(reader);
}).RequireAuthorization("AdminOnly").WithTags("Readers");

api.MapDelete("/readers/{id:guid}", async (Guid id, ApplicationDbContext db) =>
{
    var reader = await db.Readers.FindAsync(id);
    if (reader == null) return Results.NotFound();
    db.Readers.Remove(reader);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("AdminOnly").WithTags("Readers");

var resetIndex = Array.FindIndex(args, a => a.Equals("--reset-admin-password", StringComparison.OrdinalIgnoreCase));
if (resetIndex >= 0)
{
    if (resetIndex + 1 >= args.Length)
    {
        Console.WriteLine("Bitte neues Passwort nach --reset-admin-password angeben.");
        return;
    }

    using var scope = app.Services.CreateScope();
    var store = scope.ServiceProvider.GetRequiredService<AdminCredentialStore>();
    await store.ResetAsync(args[resetIndex + 1]);
    Console.WriteLine("Admin-Passwort wurde zurückgesetzt.");
    return;
}

app.Run();

public record StampRequest(string Uid, string? ReaderId, DateTime? TimestampUtc, Dictionary<string, string>? Meta);

public record RecalculateRequest(DateTime From, DateTime To);

public record ReaderUpdateRequest(string ReaderId, string? Name, string? Location, bool IsActive);

public record ReaderPingRequest(string ReaderId);

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
        var reader = await _db.Readers.FirstOrDefaultAsync(r => r.ReaderId == readerId);
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
        if (reader != null)
        {
            reader.LastPingUtc = DateTime.UtcNow;
        }
        _db.Stamps.Add(stamp);
        await _db.SaveChangesAsync();
        return stamp;
    }
}

public class ApiKeyValidator
{
    private readonly ApplicationDbContext _db;
    private readonly IApiKeyHasher _hasher;

    public ApiKeyValidator(ApplicationDbContext db, IApiKeyHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<Reader?> ValidateAsync(string apiKey)
    {
        var hash = _hasher.Hash(apiKey);
        return await _db.Readers.FirstOrDefaultAsync(r => r.ApiKeyHash == hash && r.IsActive);
    }
}
