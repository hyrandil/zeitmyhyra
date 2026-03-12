using CanteenRFID.Core.Enums;
using CanteenRFID.Data.Contexts;
using CanteenRFID.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenRFID.Web.Controllers;

[Authorize]
public class StampsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly StampService _stampService;
    private readonly IConfiguration _configuration;

    public StampsController(ApplicationDbContext db, StampService stampService, IConfiguration configuration)
    {
        _db = db;
        _stampService = stampService;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        var initial = await _db.Stamps.Include(s => s.User)
            .OrderByDescending(s => s.TimestampUtc)
            .Take(25)
            .ToListAsync();
        ViewBag.MealTypes = new[] { MealType.Breakfast, MealType.Lunch, MealType.Dinner };
        ViewBag.DefaultManualReaderId = _configuration["ManualStamp:DefaultReaderId"] ?? "MANUAL-WEB";
        return View(initial);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateManual(ManualStampCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Info"] = "Manuelle Buchung konnte nicht erstellt werden.";
            return RedirectToAction(nameof(Index));
        }

        var timestampUtc = DateTime.SpecifyKind(model.TimestampLocal, DateTimeKind.Local).ToUniversalTime();
        var readerId = string.IsNullOrWhiteSpace(model.ReaderId)
            ? (_configuration["ManualStamp:DefaultReaderId"] ?? "MANUAL-WEB")
            : model.ReaderId.Trim();

        var result = await _stampService.AddStampAsync(model.Uid.Trim(), readerId, timestampUtc);

        TempData["Info"] = result.Created
            ? "Manuelle Buchung erfasst."
            : (result.StatusMessage ?? "Buchung schon vorhanden.");

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var stamp = await _db.Stamps.FindAsync(id);
        if (stamp == null)
        {
            return RedirectToAction(nameof(Index));
        }

        _db.Stamps.Remove(stamp);
        await _db.SaveChangesAsync();
        TempData["Info"] = "Stempelung gelöscht.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSelectedBatchV3(List<Guid> ids)
    {
        if (ids is null || ids.Count == 0)
        {
            TempData["Info"] = "Keine Buchungen ausgewählt.";
            return RedirectToAction(nameof(Index));
        }

        var stamps = await _db.Stamps.Where(s => ids.Contains(s.Id)).ToListAsync();
        if (stamps.Count > 0)
        {
            _db.Stamps.RemoveRange(stamps);
            await _db.SaveChangesAsync();
        }

        TempData["Info"] = $"{stamps.Count} Buchung(en) gelöscht.";
        return RedirectToAction(nameof(Index));
    }
}
