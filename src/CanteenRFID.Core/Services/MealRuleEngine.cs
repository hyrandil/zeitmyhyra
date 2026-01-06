using CanteenRFID.Core.Enums;
using CanteenRFID.Core.Models;

namespace CanteenRFID.Core.Services;

public class MealRuleEngine
{
    private readonly IReadOnlyCollection<MealRule> _rules;

    public MealRuleEngine(IEnumerable<MealRule> rules)
    {
        _rules = rules.Where(r => r.IsActive).OrderByDescending(r => r.Priority).ToList();
    }

    public MealType ResolveMealType(DateTime timestampLocal)
    {
        foreach (var rule in _rules)
        {
            if (!IsDayIncluded(rule.DaysOfWeekMask, timestampLocal.DayOfWeek))
            {
                continue;
            }

            var time = TimeOnly.FromDateTime(timestampLocal);
            if (IsTimeWithin(rule.StartTimeLocal, rule.EndTimeLocal, time))
            {
                return rule.MealType;
            }
        }

        return MealType.Unknown;
    }

    public static bool IsDayIncluded(int mask, DayOfWeek day)
    {
        var bit = 1 << ((int)day);
        return (mask & bit) == bit;
    }

    private static bool IsTimeWithin(TimeOnly start, TimeOnly end, TimeOnly target)
    {
        if (start <= end)
        {
            return target >= start && target <= end;
        }

        // overnight span
        return target >= start || target <= end;
    }
}
