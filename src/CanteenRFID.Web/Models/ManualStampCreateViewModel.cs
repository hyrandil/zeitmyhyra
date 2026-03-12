using System.ComponentModel.DataAnnotations;

namespace CanteenRFID.Web.Models;

public class ManualStampCreateViewModel
{
    [Required]
    [MaxLength(200)]
    public string Uid { get; set; } = string.Empty;

    [Required]
    public DateTime TimestampLocal { get; set; } = DateTime.Now;

    [MaxLength(100)]
    public string? ReaderId { get; set; }
}
