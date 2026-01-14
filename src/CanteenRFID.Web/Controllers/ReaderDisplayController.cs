using CanteenRFID.Data.Contexts;
using CanteenRFID.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenRFID.Web.Controllers;

[AllowAnonymous]
public class ReaderDisplayController : Controller
{
    private readonly ApplicationDbContext _db;

    public ReaderDisplayController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("/ReaderDisplay")]
    [HttpGet("/ReaderDisplay={readerId}")]
    public async Task<IActionResult> Index(string? readerId, string? pw)
    {
        if (!string.IsNullOrWhiteSpace(readerId) && string.IsNullOrWhiteSpace(pw))
        {
            const string token = "&pw=";
            var index = readerId.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                pw = readerId[(index + token.Length)..];
                readerId = readerId[..index];
            }
        }

        if (string.IsNullOrWhiteSpace(readerId) || string.IsNullOrWhiteSpace(pw))
        {
            return Unauthorized("ReaderDisplay benötigt readerId und pw in der URL.");
        }

        var reader = await _db.Readers.FirstOrDefaultAsync(r => r.ReaderId == readerId);
        if (reader == null)
        {
            return Unauthorized("Ungültiges ReaderDisplay-Passwort.");
        }

        if (!string.IsNullOrWhiteSpace(reader.DisplayPassword))
        {
            if (reader.DisplayPassword != pw)
            {
                return Unauthorized("Ungültiges ReaderDisplay-Passwort.");
            }
        }
        else
        {
            try
            {
                var storedPassword = await _db.Database
                    .SqlQuery<string?>(
                        $"SELECT DisplayPassword FROM Readers WHERE ReaderId = {readerId} LIMIT 1")
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(storedPassword) || storedPassword != pw)
                {
                    return Unauthorized("Ungültiges ReaderDisplay-Passwort.");
                }
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                return Unauthorized("ReaderDisplay ist noch nicht konfiguriert.");
            }
        }

        var readers = new List<ReaderDisplayOption>
        {
            new()
            {
                ReaderId = reader.ReaderId,
                DisplayName = reader.Name ?? reader.ReaderId,
                Location = reader.Location,
                IsActive = reader.IsActive
            }
        };

        var model = new ReaderDisplayViewModel
        {
            Readers = readers,
            SelectedReaderId = reader.ReaderId
        };
        return View(model);
    }
}
