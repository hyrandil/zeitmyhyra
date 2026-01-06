using System.Security.Cryptography;
using System.Text;
using CanteenRFID.Core.Models;
using CanteenRFID.Data.Contexts;
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
        var readers = await _db.Readers.OrderBy(r => r.ReaderId).ToListAsync();
        return View(readers);
    }

    public IActionResult Create()
    {
        return View(new Reader());
    }

    [HttpPost]
    public async Task<IActionResult> Create(Reader reader)
    {
        reader.ApiKeyHash = _hasher.Hash(Guid.NewGuid().ToString("N"));
        _db.Readers.Add(reader);
        await _db.SaveChangesAsync();
        TempData["Info"] = "Reader erstellt. API Key bitte regenerieren.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
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
}
