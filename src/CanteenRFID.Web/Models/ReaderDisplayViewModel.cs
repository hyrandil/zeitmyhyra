namespace CanteenRFID.Web.Models;

public class ReaderDisplayViewModel
{
    public IReadOnlyList<ReaderDisplayOption> Readers { get; init; } = Array.Empty<ReaderDisplayOption>();
}
