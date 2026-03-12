using CanteenRFID.Core.Models;
using CanteenRFID.Data.Contexts;
using Microsoft.EntityFrameworkCore;

namespace CanteenRFID.Web.Services;

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
