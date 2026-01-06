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
    public async Task<IActionResult> Excel(DateTime from, DateTime to)
    {
        var stamps = await _db.Stamps.Include(s => s.User)
            .Where(s => s.TimestampUtc >= from && s.TimestampUtc <= to)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var summarySheet = workbook.Worksheets.Add("Summary");
        summarySheet.Cell(1, 1).Value = "Personnel";
        summarySheet.Cell(1, 2).Value = "Name";
        summarySheet.Cell(1, 3).Value = "Breakfast";
        summarySheet.Cell(1, 4).Value = "Lunch";
        summarySheet.Cell(1, 5).Value = "Dinner";
        summarySheet.Cell(1, 6).Value = "Snack";
        summarySheet.Cell(1, 7).Value = "Unknown";

        var grouped = stamps.GroupBy(s => s.User?.PersonnelNo ?? "Unbekannt").ToList();
        var row = 2;
        foreach (var g in grouped)
        {
            summarySheet.Cell(row, 1).Value = g.Key;
            summarySheet.Cell(row, 2).Value = g.First().User?.FullName ?? "Unbekannt";
            summarySheet.Cell(row, 3).Value = g.Count(x => x.MealType == MealType.Breakfast);
            summarySheet.Cell(row, 4).Value = g.Count(x => x.MealType == MealType.Lunch);
            summarySheet.Cell(row, 5).Value = g.Count(x => x.MealType == MealType.Dinner);
            summarySheet.Cell(row, 6).Value = g.Count(x => x.MealType == MealType.Snack);
            summarySheet.Cell(row, 7).Value = g.Count(x => x.MealType == MealType.Unknown);
            row++;
        }

        var rawSheet = workbook.Worksheets.Add("RawEvents");
        rawSheet.Cell(1, 1).Value = "Timestamp UTC";
        rawSheet.Cell(1, 2).Value = "Timestamp Local";
        rawSheet.Cell(1, 3).Value = "UID";
        rawSheet.Cell(1, 4).Value = "User";
        rawSheet.Cell(1, 5).Value = "Reader";
        rawSheet.Cell(1, 6).Value = "Meal Type";

        var rawRow = 2;
        foreach (var s in stamps.OrderBy(s => s.TimestampUtc))
        {
            rawSheet.Cell(rawRow, 1).Value = s.TimestampUtc;
            rawSheet.Cell(rawRow, 2).Value = s.TimestampLocal;
            rawSheet.Cell(rawRow, 3).Value = s.UidRaw;
            rawSheet.Cell(rawRow, 4).Value = s.User?.FullName ?? "Unbekannt";
            rawSheet.Cell(rawRow, 5).Value = s.ReaderId;
            rawSheet.Cell(rawRow, 6).Value = s.MealType.ToString();
            rawRow++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var filename = $"canteenrfid_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    [HttpPost]
    public async Task<IActionResult> Pdf(DateTime from, DateTime to)
    {
        var stamps = await _db.Stamps.Include(s => s.User)
            .Where(s => s.TimestampUtc >= from && s.TimestampUtc <= to)
            .ToListAsync();

        var grouped = stamps.GroupBy(s => s.User?.PersonnelNo ?? "Unbekannt").Select(g => new
        {
            PersonnelNo = g.Key,
            Name = g.First().User?.FullName ?? "Unbekannt",
            Breakfast = g.Count(x => x.MealType == MealType.Breakfast),
            Lunch = g.Count(x => x.MealType == MealType.Lunch),
            Dinner = g.Count(x => x.MealType == MealType.Dinner),
            Snack = g.Count(x => x.MealType == MealType.Snack),
            Unknown = g.Count(x => x.MealType == MealType.Unknown)
        }).ToList();

        byte[] pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Header().Text($"CanteenRFID Summary {from:d} - {to:d}").FontSize(20).Bold();
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(100);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Personal");
                        header.Cell().Text("Name");
                        header.Cell().Text("Breakfast");
                        header.Cell().Text("Lunch");
                        header.Cell().Text("Dinner");
                        header.Cell().Text("Snack");
                        header.Cell().Text("Unknown");
                    });

                    foreach (var row in grouped)
                    {
                        table.Cell().Text(row.PersonnelNo);
                        table.Cell().Text(row.Name);
                        table.Cell().Text(row.Breakfast.ToString());
                        table.Cell().Text(row.Lunch.ToString());
                        table.Cell().Text(row.Dinner.ToString());
                        table.Cell().Text(row.Snack.ToString());
                        table.Cell().Text(row.Unknown.ToString());
                    }
                });
            });
        }).GeneratePdf();

        var filename = $"canteenrfid_{from:yyyyMMdd}_{to:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", filename);
    }
}
