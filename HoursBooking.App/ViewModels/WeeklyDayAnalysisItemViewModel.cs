namespace HoursBooking.App.ViewModels;

public sealed class WeeklyDayAnalysisItemViewModel
{
    public string DayLabel { get; init; } = string.Empty;

    public string DateLabel { get; init; } = string.Empty;

    public string NetWorkedText { get; init; } = "00:00";

    public string DifferenceText { get; init; } = "00:00";

    public double NetMinutes { get; init; }

    public double TargetMinutes { get; init; } = 1;
}
