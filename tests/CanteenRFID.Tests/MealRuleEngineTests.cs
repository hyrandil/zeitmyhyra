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
}
