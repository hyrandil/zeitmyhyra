using System.ComponentModel.DataAnnotations;
using CanteenRFID.Core.Enums;

namespace CanteenRFID.Core.Models;

public class MealRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public MealType MealType { get; set; }

    public TimeOnly StartTimeLocal { get; set; }

    public TimeOnly EndTimeLocal { get; set; }

    public int DaysOfWeekMask { get; set; } = 127; // default all days

    public int Priority { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}
