namespace HoursBooking.Core.Models;

public sealed class WorkSegment
{
    public DateTimeOffset Start { get; set; }

    public DateTimeOffset? End { get; set; }
}
