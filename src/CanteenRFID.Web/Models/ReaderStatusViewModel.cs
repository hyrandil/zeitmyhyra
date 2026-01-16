using CanteenRFID.Core.Models;

namespace CanteenRFID.Web.Models;

public class ReaderStatusViewModel
{
    public Reader Reader { get; set; } = default!;
    public DateTime? LastSeenUtc { get; set; }

    public bool IsOnline(TimeSpan? threshold = null)
    {
        if (LastSeenUtc is null) return false;
        var limit = threshold ?? TimeSpan.FromSeconds(30);
        return LastSeenUtc.Value >= DateTime.UtcNow.Subtract(limit);
    }
}
