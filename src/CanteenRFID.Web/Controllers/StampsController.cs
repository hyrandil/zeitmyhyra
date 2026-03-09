using CanteenRFID.Core.Enums;
using CanteenRFID.Data.Contexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenRFID.Web.Controllers;

[Authorize]
public class StampsController : Controller
{
    private readonly ApplicationDbContext _db;

    public StampsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var initial = await _db.Stamps.Include(s => s.User)
            .OrderByDescending(s => s.TimestampUtc)
            .Take(50)
            .ToListAsync();
        ViewBag.MealTypes = new[] { MealType.Breakfast, MealType.Lunch, MealType.Dinner };
        return View(initial);
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
    public async Task<IActionResult> DeleteSelected(List<Guid> ids)
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

