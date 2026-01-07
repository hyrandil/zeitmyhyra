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

    [HttpGet]
    [HttpGet("/ReaderDisplay={readerId}")]
    public async Task<IActionResult> Index(string? readerId, string? pw)
    {
        if (string.IsNullOrWhiteSpace(readerId) || string.IsNullOrWhiteSpace(pw))
        {
            return Unauthorized("ReaderDisplay benötigt readerId und pw in der URL.");
        }

        var reader = await _db.Readers.FirstOrDefaultAsync(r => r.ReaderId == readerId);
        if (reader == null || string.IsNullOrWhiteSpace(reader.DisplayPassword) || reader.DisplayPassword != pw)
        {
            return Unauthorized("Ungültiges ReaderDisplay-Passwort.");
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
