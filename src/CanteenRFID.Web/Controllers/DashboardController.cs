using CanteenRFID.Core.Enums;
using CanteenRFID.Data.Contexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenRFID.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.UtcNow.Date;
        var counts = await _db.Stamps
            .Where(s => s.TimestampUtc >= today)
            .GroupBy(s => s.MealType)
            .Select(g => new MealCountView { MealType = g.Key, Count = g.Count() })
            .ToListAsync();

        ViewBag.TotalUsers = await _db.Users.CountAsync();
        ViewBag.TotalReaders = await _db.Readers.CountAsync();
        ViewBag.TodayCounts = counts;
        return View();
    }
}

public class MealCountView
{
    public MealType MealType { get; set; }
    public int Count { get; set; }
}
