using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CanteenRFID.Web.Services;

public class AdminCredentialStore
{
    public const string AdminRole = "Admin";

    private readonly string _secretPath;
    private readonly ILogger<AdminCredentialStore> _logger;
    private readonly AdminOptions _defaults;

    public AdminCredentialStore(IOptionsMonitor<AdminOptions> adminOptions, ILogger<AdminCredentialStore> logger)
    {
        _defaults = adminOptions.CurrentValue;
        _logger = logger;
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        _secretPath = Path.Combine(dataDir, "admin.secret.json");
    }

    public bool SecretExists => File.Exists(_secretPath);

    public async Task<StoredAdmin> EnsureAsync()
    {
        if (File.Exists(_secretPath))
        {
            var content = await File.ReadAllTextAsync(_secretPath);
            var stored = JsonSerializer.Deserialize<StoredAdmin>(content);
            if (stored != null && !string.IsNullOrWhiteSpace(stored.PasswordHash))
            {
                return stored;
            }
        }

        var seed = new StoredAdmin
        {
            Username = _defaults.Username,
            Role = string.IsNullOrWhiteSpace(_defaults.Role) ? AdminRole : _defaults.Role,
            PasswordHash = PasswordHashing.HashPassword(_defaults.Password)
        };

        await PersistAsync(seed);
        _logger.LogWarning("Admin-Passwort wurde aus Konfiguration neu geschrieben. Bitte sofort ändern.");
        return seed;
    }

    public async Task<bool> ValidateAsync(string username, string password)
    {
        var stored = await EnsureAsync();
        if (!string.Equals(stored.Username, username, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return PasswordHashing.Verify(password, stored.PasswordHash);
    }

    public async Task ChangePasswordAsync(string newPassword)
    {
        var stored = await EnsureAsync();
        stored.PasswordHash = PasswordHashing.HashPassword(newPassword);
        await PersistAsync(stored);
        _logger.LogInformation("Admin-Passwort wurde geändert.");
    }

    public async Task ResetAsync(string newPassword)
    {
        var stored = await EnsureAsync();
        stored.PasswordHash = PasswordHashing.HashPassword(newPassword);
        await PersistAsync(stored);
        _logger.LogInformation("Admin-Passwort wurde per CLI zurückgesetzt.");
    }

    private async Task PersistAsync(StoredAdmin admin)
    {
        var json = JsonSerializer.Serialize(admin, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_secretPath, json);
    }

    public class StoredAdmin
    {
        public string Username { get; set; } = "admin";
        public string Role { get; set; } = AdminRole;
        public string PasswordHash { get; set; } = string.Empty;
    }
}
