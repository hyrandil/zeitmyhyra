using CanteenRFID.Core.Enums;
using CanteenRFID.Core.Models;
using CanteenRFID.Data.Contexts;
using CanteenRFID.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenRFID.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class CostsController : Controller
{
    private readonly ApplicationDbContext _db;

    public CostsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var costs = await _db.MealCosts.ToListAsync();
        var model = new MealCostViewModel
        {
            Breakfast = costs.FirstOrDefault(c => c.MealType == MealType.Breakfast)?.Cost ?? 0m,
            Lunch = costs.FirstOrDefault(c => c.MealType == MealType.Lunch)?.Cost ?? 0m,
            Dinner = costs.FirstOrDefault(c => c.MealType == MealType.Dinner)?.Cost ?? 0m
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(MealCostViewModel model)
    {
        await UpsertAsync(MealType.Breakfast, model.Breakfast);
        await UpsertAsync(MealType.Lunch, model.Lunch);
        await UpsertAsync(MealType.Dinner, model.Dinner);

        TempData["Info"] = "Kosten gespeichert.";
        return RedirectToAction(nameof(Index));
    }

    private async Task UpsertAsync(MealType mealType, decimal cost)
    {
        var existing = await _db.MealCosts.FirstOrDefaultAsync(c => c.MealType == mealType);
        if (existing == null)
        {
            _db.MealCosts.Add(new MealCost { MealType = mealType, Cost = cost });
        }
        else
        {
            existing.Cost = cost;
        }
        await _db.SaveChangesAsync();
    }
}
