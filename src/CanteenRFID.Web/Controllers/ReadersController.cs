using System.Security.Cryptography;
using System.Text;
using System.Linq;
using CanteenRFID.Core.Models;
using CanteenRFID.Data.Contexts;
using CanteenRFID.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenRFID.Web.Controllers;

[Authorize]
public class ReadersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IApiKeyHasher _hasher;

    public ReadersController(ApplicationDbContext db, IApiKeyHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<IActionResult> Index()
    {
        var readers = await _db.Readers
            .OrderBy(r => r.ReaderId)
            .Select(r => new Reader
            {
                Id = r.Id,
                ReaderId = r.ReaderId,
                Name = r.Name,
                Location = r.Location,
                ApiKeyHash = r.ApiKeyHash,
                IsActive = r.IsActive,
                CreatedAtUtc = r.CreatedAtUtc,
                LastPingUtc = r.LastPingUtc,
                DisplayPassword = null
            })
            .ToListAsync();
        var models = readers.Select(r => new ReaderStatusViewModel
        {
            Reader = r,
            LastSeenUtc = r.LastPingUtc
        }).ToList();

        return View(models);
    }

    public IActionResult Create()
    {
        return View(new Reader());
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Reader reader)
    {
        if (!string.IsNullOrWhiteSpace(reader.DisplayPassword))
        {
            try
            {
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"ALTER TABLE Readers ADD COLUMN DisplayPassword TEXT");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                // Column already exists.
            }
        }
        reader.ApiKeyHash = _hasher.Hash(Guid.NewGuid().ToString("N"));
        _db.Readers.Add(reader);
        await _db.SaveChangesAsync();
        TempData["Info"] = "Reader erstellt. API Key bitte regenerieren.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Regenerate(Guid id)
    {
        var reader = await _db.Readers.FindAsync(id);
        if (reader == null) return NotFound();
        var apiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        reader.ApiKeyHash = _hasher.Hash(apiKey);
        await _db.SaveChangesAsync();
        TempData["ApiKey"] = apiKey;
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id)
    {
        var reader = await _db.Readers.FindAsync(id);
        if (reader == null) return NotFound();
        reader.IsActive = !reader.IsActive;
        await _db.SaveChangesAsync();
        TempData["Info"] = $"Reader {(reader.IsActive ? "aktiviert" : "deaktiviert")}.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDisplayPassword(Guid id, string? displayPassword)
    {
        var reader = await _db.Readers.FirstOrDefaultAsync(r => r.Id == id);
        if (reader == null) return NotFound();
        var trimmed = string.IsNullOrWhiteSpace(displayPassword) ? null : displayPassword.Trim();
        if (trimmed != null)
        {
            try
            {
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"ALTER TABLE Readers ADD COLUMN DisplayPassword TEXT");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                // Column already exists.
            }
        }

        if (trimmed == null)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Readers SET DisplayPassword = NULL WHERE Id = {id}");
        }
        else
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Readers SET DisplayPassword = {trimmed} WHERE Id = {id}");
        }
        TempData["Info"] = "Reader-Display Passwort aktualisiert.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var reader = await _db.Readers.FindAsync(id);
        if (reader == null) return NotFound();
        _db.Readers.Remove(reader);
        await _db.SaveChangesAsync();
        TempData["Info"] = "Reader gelöscht.";
        return RedirectToAction(nameof(Index));
    }
}
