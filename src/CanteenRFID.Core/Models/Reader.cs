using System.ComponentModel.DataAnnotations;

namespace CanteenRFID.Core.Models;

public class Reader
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string ReaderId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    [Required, MaxLength(256)]
    public string ApiKeyHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
