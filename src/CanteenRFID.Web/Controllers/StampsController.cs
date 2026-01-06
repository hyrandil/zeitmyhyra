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

    public async Task<IActionResult> Index(DateTime? from = null, DateTime? to = null, string? search = null, MealType? mealType = null)
    {
        var query = _db.Stamps.Include(s => s.User).AsQueryable();
        if (from.HasValue) query = query.Where(s => s.TimestampUtc >= from);
        if (to.HasValue) query = query.Where(s => s.TimestampUtc <= to);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s => s.UidRaw.Contains(search) || (s.User != null && (s.User.FirstName + " " + s.User.LastName).Contains(search)) || s.ReaderId.Contains(search));
        }
        if (mealType.HasValue) query = query.Where(s => s.MealType == mealType);

        var items = await query.OrderByDescending(s => s.TimestampUtc).Take(200).ToListAsync();
        return View(items);
    }
}
