using CanteenRFID.Core.Enums;
using CanteenRFID.Data.Contexts;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CanteenRFID.Web.Controllers;

[Authorize]
public class ExportsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ExportsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Excel(DateTime from, DateTime to, MealType? mealType, Guid? userId)
    {
        var stamps = await BuildQuery(from, to, mealType, userId).Include(s => s.User).ToListAsync();
        var costs = await LoadCostsAsync();

        using var workbook = new XLWorkbook();
        var summarySheet = workbook.Worksheets.Add("Summary");
        summarySheet.Cell(1, 1).Value = "Personnel";
        summarySheet.Cell(1, 2).Value = "Name";
        summarySheet.Cell(1, 3).Value = "Breakfast";
        summarySheet.Cell(1, 4).Value = "Lunch";
        summarySheet.Cell(1, 5).Value = "Dinner";
        summarySheet.Cell(1, 6).Value = "Breakfast Cost";
        summarySheet.Cell(1, 7).Value = "Lunch Cost";
        summarySheet.Cell(1, 8).Value = "Dinner Cost";
        summarySheet.Cell(1, 9).Value = "Breakfast Total";
        summarySheet.Cell(1, 10).Value = "Lunch Total";
        summarySheet.Cell(1, 11).Value = "Dinner Total";
        summarySheet.Cell(1, 12).Value = "Total";

        var grouped = stamps.GroupBy(s => s.UserPersonnelNo ?? s.User?.PersonnelNo ?? "Unbekannt").ToList();
        var row = 2;
        foreach (var g in grouped)
        {
            summarySheet.Cell(row, 1).Value = g.Key;
            summarySheet.Cell(row, 2).Value = g.First().UserDisplayName ?? g.First().User?.FullName ?? "Unbekannt";
            var breakfastCount = g.Count(x => x.MealType == MealType.Breakfast);
            var lunchCount = g.Count(x => x.MealType == MealType.Lunch);
            var dinnerCount = g.Count(x => x.MealType == MealType.Dinner);
            summarySheet.Cell(row, 3).Value = breakfastCount;
            summarySheet.Cell(row, 4).Value = lunchCount;
            summarySheet.Cell(row, 5).Value = dinnerCount;
            summarySheet.Cell(row, 6).Value = costs.Breakfast;
            summarySheet.Cell(row, 7).Value = costs.Lunch;
            summarySheet.Cell(row, 8).Value = costs.Dinner;
            var breakfastTotal = breakfastCount * costs.Breakfast;
            var lunchTotal = lunchCount * costs.Lunch;
            var dinnerTotal = dinnerCount * costs.Dinner;
            summarySheet.Cell(row, 9).Value = breakfastTotal;
            summarySheet.Cell(row, 10).Value = lunchTotal;
            summarySheet.Cell(row, 11).Value = dinnerTotal;
            summarySheet.Cell(row, 12).Value = breakfastTotal + lunchTotal + dinnerTotal;
            row++;
        }

        var rawSheet = workbook.Worksheets.Add("RawEvents");
        rawSheet.Cell(1, 1).Value = "Timestamp UTC";
        rawSheet.Cell(1, 2).Value = "Timestamp Local";
        rawSheet.Cell(1, 3).Value = "UID";
        rawSheet.Cell(1, 4).Value = "User";
        rawSheet.Cell(1, 5).Value = "Reader";
        rawSheet.Cell(1, 6).Value = "Meal Type";
        rawSheet.Cell(1, 7).Value = "Meal Cost";

        var rawRow = 2;
        foreach (var s in stamps.OrderBy(s => s.TimestampUtc))
        {
            rawSheet.Cell(rawRow, 1).Value = s.TimestampUtc;
            rawSheet.Cell(rawRow, 2).Value = s.TimestampLocal;
            rawSheet.Cell(rawRow, 3).Value = s.UidRaw;
            rawSheet.Cell(rawRow, 4).Value = s.UserDisplayName ?? s.User?.FullName ?? "Unbekannt";
            rawSheet.Cell(rawRow, 5).Value = s.ReaderId;
            rawSheet.Cell(rawRow, 6).Value = s.MealType.ToString();
            rawSheet.Cell(rawRow, 7).Value = costs.GetCost(s.MealType);
            rawRow++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var filename = $"canteenrfid_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    [HttpPost]
    public async Task<IActionResult> Pdf(DateTime from, DateTime to, MealType? mealType, Guid? userId)
    {
        var stamps = await BuildQuery(from, to, mealType, userId).Include(s => s.User).ToListAsync();

        var grouped = stamps.GroupBy(s => s.UserPersonnelNo ?? s.User?.PersonnelNo ?? "Unbekannt").Select(g => new
        {
            PersonnelNo = g.Key,
            Name = g.First().UserDisplayName ?? g.First().User?.FullName ?? "Unbekannt",
            Breakfast = g.Count(x => x.MealType == MealType.Breakfast),
            Lunch = g.Count(x => x.MealType == MealType.Lunch),
            Dinner = g.Count(x => x.MealType == MealType.Dinner),
            Total = g.Count(x => x.MealType == MealType.Breakfast || x.MealType == MealType.Lunch || x.MealType == MealType.Dinner)
        }).ToList();

        var daily = stamps.GroupBy(s => s.TimestampLocal.Date)
            .Select(g => new
            {
                Date = g.Key,
                Breakfast = g.Count(x => x.MealType == MealType.Breakfast),
                Lunch = g.Count(x => x.MealType == MealType.Lunch),
                Dinner = g.Count(x => x.MealType == MealType.Dinner),
                Total = g.Count(x => x.MealType == MealType.Breakfast || x.MealType == MealType.Lunch || x.MealType == MealType.Dinner)
            })
            .OrderBy(g => g.Date)
            .ToList();

        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Header().Text($"CanteenRFID Summary {from:d} - {to:d}").FontSize(20).Bold();
                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(90);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Personal").Bold();
                            header.Cell().Text("Name").Bold();
                            header.Cell().Text("Breakfast").Bold();
                            header.Cell().Text("Lunch").Bold();
                            header.Cell().Text("Dinner").Bold();
                            header.Cell().Text("Total").Bold();
                        });

                        foreach (var row in grouped)
                        {
                            table.Cell().Text(row.PersonnelNo);
                            table.Cell().Text(row.Name);
                            table.Cell().Text(row.Breakfast.ToString());
                            table.Cell().Text(row.Lunch.ToString());
                            table.Cell().Text(row.Dinner.ToString());
                            table.Cell().Text(row.Total.ToString());
                        }
                    });

                    col.Item().PaddingTop(10).Text("Tagesübersicht").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Datum").Bold();
                            header.Cell().Text("Breakfast").Bold();
                            header.Cell().Text("Lunch").Bold();
                            header.Cell().Text("Dinner").Bold();
                            header.Cell().Text("Total").Bold();
                        });

                        foreach (var row in daily)
                        {
                            table.Cell().Text(row.Date.ToShortDateString());
                            table.Cell().Text(row.Breakfast.ToString());
                            table.Cell().Text(row.Lunch.ToString());
                            table.Cell().Text(row.Dinner.ToString());
                            table.Cell().Text(row.Total.ToString());
                        }
                    });
                });
            });
        }).GeneratePdf();

        var filename = $"canteenrfid_{from:yyyyMMdd}_{to:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", filename);
    }

    private IQueryable<Core.Models.Stamp> BuildQuery(DateTime from, DateTime to, MealType? mealType, Guid? userId)
    {
        var query = _db.Stamps.Where(s => s.TimestampUtc >= from && s.TimestampUtc <= to);
        if (mealType.HasValue)
        {
            query = query.Where(s => s.MealType == mealType);
        }
        if (userId.HasValue)
        {
            query = query.Where(s => s.UserId == userId);
        }
        return query;
    }

    private async Task<MealCostSnapshot> LoadCostsAsync()
    {
        var costs = await _db.MealCosts.AsNoTracking().ToListAsync();
        var breakfast = costs.FirstOrDefault(c => c.MealType == MealType.Breakfast)?.Cost ?? 0m;
        var lunch = costs.FirstOrDefault(c => c.MealType == MealType.Lunch)?.Cost ?? 0m;
        var dinner = costs.FirstOrDefault(c => c.MealType == MealType.Dinner)?.Cost ?? 0m;
        return new MealCostSnapshot(breakfast, lunch, dinner);
    }

    private readonly record struct MealCostSnapshot(decimal Breakfast, decimal Lunch, decimal Dinner)
    {
        public decimal GetCost(MealType mealType)
        {
            return mealType switch
            {
                MealType.Breakfast => Breakfast,
                MealType.Lunch => Lunch,
                MealType.Dinner => Dinner,
                _ => 0m
            };
        }
    }
}
