using CanteenRFID.Core.Enums;
using CanteenRFID.Core.Models;
using CanteenRFID.Core.Services;
using Xunit;

public class MealRuleEngineTests
{
    [Fact]
    public void ResolvesHighestPriority()
    {
        var rules = new[]
        {
            new MealRule { Name = "Früh", MealType = MealType.Breakfast, StartTimeLocal = new TimeOnly(7,0), EndTimeLocal = new TimeOnly(10,0), Priority = 1, IsActive = true },
            new MealRule { Name = "Mittag", MealType = MealType.Lunch, StartTimeLocal = new TimeOnly(9,0), EndTimeLocal = new TimeOnly(12,0), Priority = 5, IsActive = true }
        };
        var engine = new MealRuleEngine(rules);
        var result = engine.ResolveMealType(new DateTime(2024,1,1,9,30,0));
        Assert.Equal(MealType.Lunch, result);
    }

    [Fact]
    public void SkipsInactiveDays()
    {
        var rules = new[]
        {
            new MealRule { Name = "Abend", MealType = MealType.Dinner, StartTimeLocal = new TimeOnly(18,0), EndTimeLocal = new TimeOnly(20,0), Priority = 1, IsActive = true, DaysOfWeekMask = 1 << (int)DayOfWeek.Monday }
        };
        var engine = new MealRuleEngine(rules);
        var tuesday = new DateTime(2024, 1, 2, 19, 0, 0); // Tuesday
        Assert.Equal(MealType.Unknown, engine.ResolveMealType(tuesday));
    }

    [Fact]
    public void SupportsOvernightWindow()
    {
        var rules = new[]
        {
            new MealRule { Name = "Nachtschicht", MealType = MealType.Snack, StartTimeLocal = new TimeOnly(22,0), EndTimeLocal = new TimeOnly(2,0), Priority = 1, IsActive = true }
        };
        var engine = new MealRuleEngine(rules);
        var midnight = new DateTime(2024, 1, 2, 1, 0, 0);
        Assert.Equal(MealType.Snack, engine.ResolveMealType(midnight));
    }
}
