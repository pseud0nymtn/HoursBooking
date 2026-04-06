using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HoursBooking.App.Services;
using HoursBooking.Core.Models;
using HoursBooking.Core.Services;

namespace HoursBooking.App.ViewModels;

public partial class WeeklyAnalysisViewModel : ViewModelBase
{
    private readonly BookingCalculator _calculator;
    private readonly LocalizationService _localizer;
    private IReadOnlyList<WorkSegment> _segments = [];
    private BookingSettings _settings = new();
    private DateTimeOffset _currentTime = DateTimeOffset.Now;

    public WeeklyAnalysisViewModel(BookingCalculator calculator, LocalizationService localizer)
    {
        _calculator = calculator;
        _localizer = localizer;
        _localizer.LanguageChanged += OnLanguageChanged;

        WeeklyDayAnalysisItems = new ObservableCollection<WeeklyDayAnalysisItemViewModel>();
        WeeklyTrendPoints = new ObservableCollection<WeeklyTrendPointItemViewModel>();
    }

    public ObservableCollection<WeeklyDayAnalysisItemViewModel> WeeklyDayAnalysisItems { get; }

    public ObservableCollection<WeeklyTrendPointItemViewModel> WeeklyTrendPoints { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShowNextWeekCommand))]
    private int selectedWeekOffset;

    [ObservableProperty]
    private string selectedWeekRangeText = string.Empty;

    [ObservableProperty]
    private string weeklyGrossWorkedText = "00:00";

    [ObservableProperty]
    private string weeklyNetWorkedText = "00:00";

    [ObservableProperty]
    private string weeklyDesiredText = "00:00";

    [ObservableProperty]
    private string weeklyDifferenceStatus = "00:00";

    [ObservableProperty]
    private string weeklyAveragePerWorkedDayText = "00:00";

    [ObservableProperty]
    private string weeklyBestDayText = "-";

    [ObservableProperty]
    private string weeklyTargetReachedDaysText = "0 / 5";

    [ObservableProperty]
    private string weeklyLongestBreakText = "00:00";

    [ObservableProperty]
    private bool hasWeeklyAnalysisData;

    [ObservableProperty]
    private bool showWeeklyNoData = true;

    [ObservableProperty]
    private string weeklyTrendPolylinePoints = string.Empty;

    public string TabHeader => _localizer["Tabs.Analysis"];

    public string WeeklyMetricsTitle => _localizer["Metrics.Weekly"];

    public string WeeklyGrossLabel => _localizer["Metrics.WeeklyGross"];

    public string WeeklyNetLabel => _localizer["Metrics.WeeklyNet"];

    public string WeeklyDesiredLabel => _localizer["Metrics.WeeklyDesired"];

    public string WeeklyDifferenceLabel => _localizer["Metrics.WeeklyDifference"];

    public string WeeklyAnalysisTitle => _localizer["Analytics.WeeklyTitle"];

    public string WeeklyChartTitle => _localizer["Analytics.ChartTitle"];

    public string WeeklyInsightsTitle => _localizer["Analytics.InsightsTitle"];

    public string WeeklyAveragePerWorkedDayLabel => _localizer["Analytics.AveragePerWorkedDay"];

    public string WeeklyBestDayLabel => _localizer["Analytics.BestDay"];

    public string WeeklyTargetReachedDaysLabel => _localizer["Analytics.TargetReachedDays"];

    public string WeeklyLongestBreakLabel => _localizer["Analytics.LongestBreak"];

    public string WeeklyNoDataText => _localizer["Analytics.NoData"];

    public string PreviousWeekText => _localizer["Analytics.PreviousWeek"];

    public string NextWeekText => _localizer["Analytics.NextWeek"];

    public string WeeklyTrendTitle => _localizer["Analytics.TrendTitle"];

    public void Update(IEnumerable<WorkSegment> segments, DateTimeOffset currentTime, BookingSettings settings)
    {
        _segments = segments.OrderBy(segment => segment.Start).ToList();
        _currentTime = currentTime;
        _settings = settings;
        Recalculate();
    }

    [RelayCommand]
    private void ShowPreviousWeek()
    {
        SelectedWeekOffset--;
    }

    [RelayCommand(CanExecute = nameof(CanShowNextWeek))]
    private void ShowNextWeek()
    {
        if (!CanShowNextWeek())
        {
            return;
        }

        SelectedWeekOffset++;
    }

    private bool CanShowNextWeek() => SelectedWeekOffset < 0;

    partial void OnSelectedWeekOffsetChanged(int value)
    {
        Recalculate();
    }

    private void Recalculate()
    {
        var weekStart = GetWeekStartForOffset(SelectedWeekOffset);
        var weekEnd = weekStart.AddDays(6);
        var weekSegments = GetWeekSegments(weekStart);
        var weeklyGross = _calculator.GetGrossDuration(weekSegments, _currentTime);
        var weeklyNet = CalculateWeeklyNetDuration(weekSegments, _settings);
        var weeklyDesired = TimeSpan.FromHours(Math.Max(0, _settings.WeeklyDesiredHours));
        var difference = weeklyNet - weeklyDesired;

        WeeklyGrossWorkedText = FormatDuration(weeklyGross);
        WeeklyNetWorkedText = FormatDuration(weeklyNet);
        WeeklyDesiredText = FormatDuration(weeklyDesired);
        SelectedWeekRangeText = _localizer.Format("Analytics.WeekRange", weekStart, weekEnd);

        if (difference > TimeSpan.Zero)
        {
            WeeklyDifferenceStatus = $"+{FormatDuration(difference)}";
        }
        else if (difference < TimeSpan.Zero)
        {
            WeeklyDifferenceStatus = $"-{FormatDuration(TimeSpan.FromTicks(Math.Abs(difference.Ticks)))}";
        }
        else
        {
            WeeklyDifferenceStatus = "00:00";
        }

        var dailySegments = weekSegments
            .GroupBy(segment => segment.Start.ToLocalTime().Date)
            .ToDictionary(group => group.Key, group => group.ToList());

        RecalculateWeeklyAnalytics(weekStart, dailySegments);
        RecalculateWeeklyTrend();
    }

    private void RecalculateWeeklyAnalytics(DateTime weekStart, IReadOnlyDictionary<DateTime, List<WorkSegment>> dailySegments)
    {
        WeeklyDayAnalysisItems.Clear();

        var dayTargetMinutes = Math.Max(0, _settings.WeeklyDesiredHours) / 5d * 60d;
        var safeTargetMinutes = Math.Max(dayTargetMinutes, 1d);

        var workedDays = 0;
        var workedTotal = TimeSpan.Zero;
        var bestDayDate = default(DateTime);
        var bestDayDuration = TimeSpan.Zero;
        var longestBreak = TimeSpan.Zero;
        var targetReachedDays = 0;

        for (var dayIndex = 0; dayIndex < 7; dayIndex++)
        {
            var date = weekStart.AddDays(dayIndex);
            dailySegments.TryGetValue(date, out var daySegmentsForDate);
            daySegmentsForDate ??= [];

            var dayNet = _calculator.GetNetDuration(daySegmentsForDate, _settings.BreakRules, _currentTime, _settings.CountStampedOutTimeAsBreak);
            var dayBreak = _calculator.GetStampedOutDuration(daySegmentsForDate);

            if (dayNet > TimeSpan.Zero)
            {
                workedDays++;
                workedTotal += dayNet;
            }

            if (dayNet > bestDayDuration)
            {
                bestDayDuration = dayNet;
                bestDayDate = date;
            }

            if (dayBreak > longestBreak)
            {
                longestBreak = dayBreak;
            }

            var isWeekday = date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
            if (isWeekday && dayTargetMinutes > 0 && dayNet.TotalMinutes >= dayTargetMinutes)
            {
                targetReachedDays++;
            }

            var difference = dayNet - TimeSpan.FromMinutes(dayTargetMinutes);
            var differencePrefix = difference >= TimeSpan.Zero ? "+" : "-";
            var absoluteDifference = TimeSpan.FromTicks(Math.Abs(difference.Ticks));

            WeeklyDayAnalysisItems.Add(new WeeklyDayAnalysisItemViewModel
            {
                DayLabel = date.ToString("ddd", _localizer.CurrentCulture),
                DateLabel = date.ToString("dd.MM", _localizer.CurrentCulture),
                NetWorkedText = FormatDuration(dayNet),
                DifferenceText = $"{differencePrefix}{FormatDuration(absoluteDifference)}",
                NetMinutes = Math.Max(0, dayNet.TotalMinutes),
                TargetMinutes = safeTargetMinutes
            });
        }

        WeeklyAveragePerWorkedDayText = workedDays > 0
            ? FormatDuration(TimeSpan.FromMinutes(workedTotal.TotalMinutes / workedDays))
            : "00:00";

        WeeklyBestDayText = bestDayDuration > TimeSpan.Zero
            ? $"{bestDayDate.ToString("ddd dd.MM", _localizer.CurrentCulture)} ({FormatDuration(bestDayDuration)})"
            : _localizer["Analytics.NotAvailable"];

        WeeklyTargetReachedDaysText = $"{targetReachedDays} / 5";
        WeeklyLongestBreakText = FormatDuration(longestBreak);
        HasWeeklyAnalysisData = workedDays > 0;
        ShowWeeklyNoData = !HasWeeklyAnalysisData;
    }

    private void RecalculateWeeklyTrend()
    {
        WeeklyTrendPoints.Clear();

        const int trendWeeks = 8;
        var startOffset = SelectedWeekOffset - (trendWeeks - 1);
        var totals = new List<double>(trendWeeks);

        for (var offset = startOffset; offset <= SelectedWeekOffset; offset++)
        {
            var weekStart = GetWeekStartForOffset(offset);
            var weekTotal = CalculateWeeklyNetDuration(GetWeekSegments(weekStart), _settings);
            totals.Add(weekTotal.TotalMinutes);

            WeeklyTrendPoints.Add(new WeeklyTrendPointItemViewModel
            {
                WeekLabel = _localizer.Format("Analytics.WeekShort", ISOWeek.GetWeekOfYear(weekStart)),
                RangeLabel = weekStart.ToString("dd.MM", _localizer.CurrentCulture),
                NetWorkedText = FormatDuration(weekTotal)
            });
        }

        WeeklyTrendPolylinePoints = BuildPolylinePoints(totals, Math.Max(60, _settings.WeeklyDesiredHours * 60));
    }

    private TimeSpan CalculateWeeklyNetDuration(IEnumerable<WorkSegment> weekSegments, BookingSettings settings)
    {
        var dayGroups = weekSegments
            .GroupBy(segment => segment.Start.ToLocalTime().Date)
            .Select(group => _calculator.GetNetDuration(group, settings.BreakRules, _currentTime, settings.CountStampedOutTimeAsBreak));

        return dayGroups.Aggregate(TimeSpan.Zero, (current, value) => current + value);
    }

    private List<WorkSegment> GetWeekSegments(DateTime weekStart)
    {
        var weekEnd = weekStart.AddDays(6);
        return _segments
            .Where(segment =>
            {
                var segmentDate = segment.Start.ToLocalTime().Date;
                return segmentDate >= weekStart && segmentDate <= weekEnd;
            })
            .OrderBy(segment => segment.Start)
            .ToList();
    }

    private DateTime GetCurrentWeekStart()
    {
        var currentDate = _currentTime.LocalDateTime.Date;
        var dayOfWeek = currentDate.DayOfWeek;
        var daysUntilMonday = dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
        return currentDate.AddDays(-daysUntilMonday);
    }

    private DateTime GetWeekStartForOffset(int weekOffset)
    {
        return GetCurrentWeekStart().AddDays(weekOffset * 7);
    }

    private static string BuildPolylinePoints(IReadOnlyList<double> values, double referenceMaxMinutes)
    {
        if (values.Count == 0)
        {
            return string.Empty;
        }

        const double width = 480;
        const double height = 120;
        var maxValue = Math.Max(referenceMaxMinutes, values.Max());
        var safeMax = Math.Max(1, maxValue);
        var stepX = values.Count > 1 ? width / (values.Count - 1) : width;

        var points = new StringBuilder();
        for (var index = 0; index < values.Count; index++)
        {
            var x = index * stepX;
            var y = height - ((Math.Max(0, values[index]) / safeMax) * height);
            if (index > 0)
            {
                points.Append(' ');
            }

            points.Append(x.ToString("F2", CultureInfo.InvariantCulture));
            points.Append(',');
            points.Append(y.ToString("F2", CultureInfo.InvariantCulture));
        }

        return points.ToString();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}";
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TabHeader));
        OnPropertyChanged(nameof(WeeklyMetricsTitle));
        OnPropertyChanged(nameof(WeeklyGrossLabel));
        OnPropertyChanged(nameof(WeeklyNetLabel));
        OnPropertyChanged(nameof(WeeklyDesiredLabel));
        OnPropertyChanged(nameof(WeeklyDifferenceLabel));
        OnPropertyChanged(nameof(WeeklyAnalysisTitle));
        OnPropertyChanged(nameof(WeeklyChartTitle));
        OnPropertyChanged(nameof(WeeklyInsightsTitle));
        OnPropertyChanged(nameof(WeeklyAveragePerWorkedDayLabel));
        OnPropertyChanged(nameof(WeeklyBestDayLabel));
        OnPropertyChanged(nameof(WeeklyTargetReachedDaysLabel));
        OnPropertyChanged(nameof(WeeklyLongestBreakLabel));
        OnPropertyChanged(nameof(WeeklyNoDataText));
        OnPropertyChanged(nameof(PreviousWeekText));
        OnPropertyChanged(nameof(NextWeekText));
        OnPropertyChanged(nameof(WeeklyTrendTitle));
        Recalculate();
    }
}
