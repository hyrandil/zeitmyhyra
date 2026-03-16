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

    public async Task<IActionResult> Index(
        DateTime? from,
        DateTime? to,
        string? name,
        string? personnelNo,
        string? uid,
        string? readerId,
        MealType? mealType,
        int page = 1,
        int pageSize = 25)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 25 : Math.Min(pageSize, 100);

        var query = _db.Stamps.Include(s => s.User).AsQueryable();
        if (from.HasValue) query = query.Where(s => s.TimestampLocal >= from.Value);
        if (to.HasValue) query = query.Where(s => s.TimestampLocal <= to.Value);
        if (!string.IsNullOrWhiteSpace(uid)) query = query.Where(s => s.UidRaw.Contains(uid));
        if (!string.IsNullOrWhiteSpace(readerId)) query = query.Where(s => s.ReaderId.Contains(readerId));
        if (mealType.HasValue) query = query.Where(s => s.MealType == mealType.Value);
        if (!string.IsNullOrWhiteSpace(name))
        {
            var n = name.Trim();
            query = query.Where(s =>
                (s.UserDisplayName != null && s.UserDisplayName.Contains(n)) ||
                (s.User != null && ((s.User.FirstName + " " + s.User.LastName).Contains(n) || (s.User.LastName + " " + s.User.FirstName).Contains(n))));
        }
        if (!string.IsNullOrWhiteSpace(personnelNo))
        {
            var pNo = personnelNo.Trim();
            query = query.Where(s =>
                (s.UserPersonnelNo != null && s.UserPersonnelNo.Contains(pNo)) ||
                (s.User != null && s.User.PersonnelNo.Contains(pNo)));
        }

        var total = await query.CountAsync();

        var initial = await query
            .OrderByDescending(s => s.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.MealTypes = new[] { MealType.Breakfast, MealType.Lunch, MealType.Dinner };
        ViewBag.DefaultManualReaderId = _configuration["ManualStamp:DefaultReaderId"] ?? "MANUAL-WEB";
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Name = name;
        ViewBag.PersonnelNo = personnelNo;
        ViewBag.Uid = uid;
        ViewBag.ReaderId = readerId;
        ViewBag.MealType = mealType;
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
    public async Task<IActionResult> DeleteSelectedFromFormV4([FromForm(Name = "ids")] List<string>? idValues)
    {
        var ids = (idValues ?? new List<string>())
            .Select(v => Guid.TryParse(v, out var parsed) ? parsed : Guid.Empty)
            .Where(v => v != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
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
