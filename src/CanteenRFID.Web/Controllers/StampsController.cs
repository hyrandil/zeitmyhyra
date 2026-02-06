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
}
