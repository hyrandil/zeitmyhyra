using CanteenRFID.Core.Enums;
using CanteenRFID.Data.Contexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenRFID.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ReportsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Consumption(DateTime? from = null, DateTime? to = null)
    {
        var summary = await BuildSummaryAsync(from, to);
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        return View(summary);
    }

    [HttpGet]
    public async Task<IActionResult> ConsumptionData(DateTime? from = null, DateTime? to = null)
    {
        var summary = await BuildSummaryAsync(from, to);
        return Json(summary);
    }

    private async Task<List<ConsumptionRow>> BuildSummaryAsync(DateTime? from, DateTime? to)
    {
        var query = _db.Stamps.Include(s => s.User).AsQueryable();
        if (from.HasValue) query = query.Where(s => s.TimestampUtc >= from);
        if (to.HasValue) query = query.Where(s => s.TimestampUtc <= to);

        return await query
            .GroupBy(s => s.User != null ? s.User.PersonnelNo : "Unbekannt")
            .Select(g => new ConsumptionRow
            {
                PersonnelNo = g.Key,
                Name = g.First().User != null ? g.First().User!.FullName : "Unbekannte UID",
                Breakfast = g.Count(x => x.MealType == MealType.Breakfast),
                Lunch = g.Count(x => x.MealType == MealType.Lunch),
                Dinner = g.Count(x => x.MealType == MealType.Dinner),
                Snack = g.Count(x => x.MealType == MealType.Snack),
                Unknown = g.Count(x => x.MealType == MealType.Unknown)
            }).ToListAsync();
    }
}

public class ConsumptionRow
{
    public string PersonnelNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Breakfast { get; set; }
    public int Lunch { get; set; }
    public int Dinner { get; set; }
    public int Snack { get; set; }
    public int Unknown { get; set; }
}
