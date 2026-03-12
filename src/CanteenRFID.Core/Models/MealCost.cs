using System.ComponentModel.DataAnnotations;
using CanteenRFID.Core.Enums;

namespace CanteenRFID.Core.Models;

public class MealCost
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public MealType MealType { get; set; }

    [Range(0, 1000)]
    public decimal Cost { get; set; }
}
