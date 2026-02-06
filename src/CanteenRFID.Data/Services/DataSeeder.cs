using CanteenRFID.Core.Enums;
using CanteenRFID.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CanteenRFID.Data.Services;

public static class DataSeeder
{
    public static async Task SeedAsync(DbContext context)
    {
        if (context is not DbContext db)
        {
            return;
        }

        await db.Database.EnsureCreatedAsync();

        var set = db.Set<MealRule>();
        if (!await set.AnyAsync())
        {
            set.AddRange(new[]
            {
                new MealRule
                {
                    Name = "Frühstück",
                    MealType = MealType.Breakfast,
                    StartTimeLocal = new TimeOnly(7,0),
                    EndTimeLocal = new TimeOnly(10,0),
                    DaysOfWeekMask = 127,
                    Priority = 10
                },
                new MealRule
                {
                    Name = "Mittag",
                    MealType = MealType.Lunch,
                    StartTimeLocal = new TimeOnly(10,0),
                    EndTimeLocal = new TimeOnly(15,0),
                    DaysOfWeekMask = 127,
                    Priority = 9
                },
                new MealRule
                {
                    Name = "Abend",
                    MealType = MealType.Dinner,
                    StartTimeLocal = new TimeOnly(15,0),
                    EndTimeLocal = new TimeOnly(20,0),
                    DaysOfWeekMask = 127,
                    Priority = 8
                }
            });
            await db.SaveChangesAsync();
        }

        var costs = db.Set<MealCost>();
        if (!await costs.AnyAsync())
        {
            costs.AddRange(new[]
            {
                new MealCost { MealType = MealType.Breakfast, Cost = 0m },
                new MealCost { MealType = MealType.Lunch, Cost = 0m },
                new MealCost { MealType = MealType.Dinner, Cost = 0m }
            });
            await db.SaveChangesAsync();
        }
    }
}
