using System.ComponentModel.DataAnnotations;
using CanteenRFID.Core.Enums;

namespace CanteenRFID.Core.Models;

public class Stamp
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public DateTime TimestampLocal { get; set; } = DateTime.Now;

    [Required, MaxLength(200)]
    public string UidRaw { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public User? User { get; set; }

    [MaxLength(200)]
    public string? UserDisplayName { get; set; }

    [MaxLength(50)]
    public string? UserPersonnelNo { get; set; }

    [Required, MaxLength(100)]
    public string ReaderId { get; set; } = string.Empty;

    public MealType MealType { get; set; } = MealType.Unknown;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
