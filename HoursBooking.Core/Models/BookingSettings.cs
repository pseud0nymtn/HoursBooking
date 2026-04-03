namespace HoursBooking.Core.Models;

public sealed class BookingSettings
{
    public double MaxWorkHours { get; set; } = 8.0;

    public double DesiredWorkHours { get; set; } = 7.5;

    public bool CountStampedOutTimeAsBreak { get; set; }

    public double InfoThresholdMinutes { get; set; } = 60;

    public double WarningThresholdMinutes { get; set; } = 30;

    public double ErrorThresholdMinutes { get; set; } = 10;

    public List<BreakRule> BreakRules { get; set; } =
    [
        new BreakRule { MinWorkedHours = 0, DeductedBreakHours = 0.25 },
        new BreakRule { MinWorkedHours = 6, DeductedBreakHours = 0.75 }
    ];
}
