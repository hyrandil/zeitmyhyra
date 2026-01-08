namespace CanteenRFID.Web.Models;

public class ReaderDisplayOption
{
    public string ReaderId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Location { get; init; }
    public bool IsActive { get; init; }
}
