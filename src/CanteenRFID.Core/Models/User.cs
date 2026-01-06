using System.ComponentModel.DataAnnotations;

namespace CanteenRFID.Core.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string PersonnelNo { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Uid { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string FullName => $"{FirstName} {LastName}";
}
