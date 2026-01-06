using CanteenRFID.Core.Enums;
using CanteenRFID.Core.Models;
using CanteenRFID.Core.Services;
using CanteenRFID.Data.Contexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenRFID.Web.Controllers;

[Authorize]
public class MealRulesController : Controller
{
    private readonly ApplicationDbContext _db;

    public MealRulesController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var rules = await _db.MealRules.OrderByDescending(r => r.Priority).ToListAsync();
        return View(rules);
    }

    public IActionResult Create()
    {
        return View(new MealRule { StartTimeLocal = new TimeOnly(7, 0), EndTimeLocal = new TimeOnly(10, 0), MealType = MealType.Breakfast });
    }

    [HttpPost]
    public async Task<IActionResult> Create(MealRule rule)
    {
        if (!ModelState.IsValid)
        {
            return View(rule);
        }
        _db.MealRules.Add(rule);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var rule = await _db.MealRules.FindAsync(id);
        if (rule == null) return NotFound();
        return View(rule);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, MealRule rule)
    {
        var existing = await _db.MealRules.FindAsync(id);
        if (existing == null) return NotFound();
        if (!ModelState.IsValid) return View(rule);
        existing.Name = rule.Name;
        existing.MealType = rule.MealType;
        existing.StartTimeLocal = rule.StartTimeLocal;
        existing.EndTimeLocal = rule.EndTimeLocal;
        existing.Priority = rule.Priority;
        existing.DaysOfWeekMask = rule.DaysOfWeekMask;
        existing.IsActive = rule.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> Delete(Guid id)
    {
        var rule = await _db.MealRules.FindAsync(id);
        if (rule != null)
        {
            _db.MealRules.Remove(rule);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> Recalculate(DateTime from, DateTime to)
    {
        var engine = new MealRuleEngine(await _db.MealRules.Where(r => r.IsActive).ToListAsync());
        var stamps = await _db.Stamps.Where(s => s.TimestampUtc >= from && s.TimestampUtc <= to).ToListAsync();
        foreach (var stamp in stamps)
        {
            stamp.MealType = engine.ResolveMealType(stamp.TimestampLocal);
        }
        await _db.SaveChangesAsync();
        TempData["Message"] = $"{stamps.Count} Stempel neu berechnet";
        return RedirectToAction(nameof(Index));
    }
}
