using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HoursBooking.App.Models;
using HoursBooking.App.Services;
using HoursBooking.Core.Models;
using HoursBooking.Core.Services;

namespace HoursBooking.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly BookingCalculator _calculator;
    private readonly ISettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly DispatcherTimer _timer;
    private readonly List<WorkSegment> _segments = [];
    private WorkSegment? _activeSegment;
    private bool _isInitializing;

    public MainWindowViewModel(BookingCalculator calculator, ISettingsStore settingsStore, IThemeService themeService)
    {
        _calculator = calculator;
        _settingsStore = settingsStore;
        _themeService = themeService;

        CurrentTime = DateTimeOffset.Now;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) =>
        {
            CurrentTime = DateTimeOffset.Now;
            Recalculate();
        };
        _timer.Start();

        BreakRules = new ObservableCollection<BreakRuleViewModel>();
        Segments = new ObservableCollection<WorkSegmentItemViewModel>();
        ThemeModes = Enum.GetValues<ThemeMode>();
    }

    public ObservableCollection<BreakRuleViewModel> BreakRules { get; }

    public ObservableCollection<WorkSegmentItemViewModel> Segments { get; }

    public IReadOnlyList<ThemeMode> ThemeModes { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentDayLabel))]
    [NotifyPropertyChangedFor(nameof(CurrentTimeLabel))]
    private DateTimeOffset currentTime;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClockInCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClockOutCommand))]
    private bool isClockedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlert))]
    private AlertLevel alertLevel;

    [ObservableProperty]
    private string alertMessage = "Keine Warnung.";

    [ObservableProperty]
    private IBrush alertBackground = new SolidColorBrush(Color.Parse("#EEF2F6"));

    [ObservableProperty]
    private IBrush alertForeground = new SolidColorBrush(Color.Parse("#1B2733"));

    [ObservableProperty]
    private string grossWorkedText = "00:00";

    [ObservableProperty]
    private string deductedBreakText = "00:00";

    [ObservableProperty]
    private string netWorkedText = "00:00";

    [ObservableProperty]
    private double maxWorkHours = 8.0;

    [ObservableProperty]
    private double desiredWorkHours = 7.5;

    [ObservableProperty]
    private bool countStampedOutTimeAsBreak;

    [ObservableProperty]
    private double infoThresholdMinutes = 60;

    [ObservableProperty]
    private double warningThresholdMinutes = 30;

    [ObservableProperty]
    private double errorThresholdMinutes = 10;

    [ObservableProperty]
    private ThemeMode selectedThemeMode;

    [ObservableProperty]
    private string desiredEndTimeText = "Noch nicht gestartet";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSegment))]
    private WorkSegment? selectedSegment;

    [ObservableProperty]
    private string selectedSegmentStartText = string.Empty;

    [ObservableProperty]
    private string selectedSegmentEndText = string.Empty;

    [ObservableProperty]
    private string segmentEditMessage = "Arbeitsabschnitt auswaehlen, um Zeiten anzupassen.";

    public string CurrentDayLabel => CurrentTime.ToString("dddd, dd.MM.yyyy");

    public string CurrentTimeLabel => CurrentTime.ToString("HH:mm:ss");

    public bool HasAlert => AlertLevel != AlertLevel.None;

    public bool HasSelectedSegment => SelectedSegment is not null;

    public async Task InitializeAsync()
    {
        _isInitializing = true;

        var document = await _settingsStore.LoadAsync();
        var settings = document.BookingSettings;

        SelectedThemeMode = document.ThemeMode;
        MaxWorkHours = settings.MaxWorkHours;
        DesiredWorkHours = settings.DesiredWorkHours;
        CountStampedOutTimeAsBreak = settings.CountStampedOutTimeAsBreak;
        InfoThresholdMinutes = settings.InfoThresholdMinutes;
        WarningThresholdMinutes = settings.WarningThresholdMinutes;
        ErrorThresholdMinutes = settings.ErrorThresholdMinutes;

        BreakRules.Clear();
        foreach (var rule in settings.BreakRules.OrderBy(rule => rule.MinWorkedHours))
        {
            BreakRules.Add(new BreakRuleViewModel(rule.MinWorkedHours, rule.DeductedBreakHours, OnRuleChanged));
        }

        if (BreakRules.Count == 0)
        {
            BreakRules.Add(new BreakRuleViewModel(0, 0.25, OnRuleChanged));
        }

        _themeService.ApplyTheme(SelectedThemeMode);
        _isInitializing = false;

        Recalculate();
    }

    [RelayCommand(CanExecute = nameof(CanClockIn))]
    private void ClockIn()
    {
        if (IsClockedIn)
        {
            return;
        }

        _activeSegment = new WorkSegment { Start = DateTimeOffset.Now };
        _segments.Add(_activeSegment);
        IsClockedIn = true;
        Recalculate();
    }

    [RelayCommand(CanExecute = nameof(CanClockOut))]
    private void ClockOut()
    {
        if (!IsClockedIn || _activeSegment is null)
        {
            return;
        }

        _activeSegment.End = DateTimeOffset.Now;
        _activeSegment = null;
        IsClockedIn = false;
        Recalculate();
    }

    [RelayCommand]
    private void AddBreakRule()
    {
        BreakRules.Add(new BreakRuleViewModel(0, 0, OnRuleChanged));
        Recalculate();
        _ = SaveSettingsAsync();
    }

    [RelayCommand]
    private void RemoveBreakRule(BreakRuleViewModel? rule)
    {
        if (rule is null)
        {
            return;
        }

        BreakRules.Remove(rule);
        if (BreakRules.Count == 0)
        {
            BreakRules.Add(new BreakRuleViewModel(0, 0.25, OnRuleChanged));
        }

        Recalculate();
        _ = SaveSettingsAsync();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var document = BuildSettingsDocument();
        await _settingsStore.SaveAsync(document);
    }

    private bool CanClockIn() => !IsClockedIn;

    private bool CanClockOut() => IsClockedIn;

    private void OnRuleChanged()
    {
        Recalculate();
        _ = SaveSettingsAsync();
    }

    partial void OnSelectedThemeModeChanged(ThemeMode value)
    {
        _themeService.ApplyTheme(value);
        if (!_isInitializing)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnMaxWorkHoursChanged(double value)
    {
        Recalculate();
        if (!_isInitializing)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnDesiredWorkHoursChanged(double value)
    {
        Recalculate();
        if (!_isInitializing)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnCountStampedOutTimeAsBreakChanged(bool value)
    {
        Recalculate();
        if (!_isInitializing)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnInfoThresholdMinutesChanged(double value)
    {
        Recalculate();
        if (!_isInitializing)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnWarningThresholdMinutesChanged(double value)
    {
        Recalculate();
        if (!_isInitializing)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnErrorThresholdMinutesChanged(double value)
    {
        Recalculate();
        if (!_isInitializing)
        {
            _ = SaveSettingsAsync();
        }
    }

    private AppSettingsDocument BuildSettingsDocument()
    {
        return new AppSettingsDocument
        {
            ThemeMode = SelectedThemeMode,
            BookingSettings = new BookingSettings
            {
                MaxWorkHours = Math.Max(0.1, MaxWorkHours),
                DesiredWorkHours = Math.Max(0, DesiredWorkHours),
                CountStampedOutTimeAsBreak = CountStampedOutTimeAsBreak,
                InfoThresholdMinutes = Math.Max(0, InfoThresholdMinutes),
                WarningThresholdMinutes = Math.Max(0, WarningThresholdMinutes),
                ErrorThresholdMinutes = Math.Max(0, ErrorThresholdMinutes),
                BreakRules = BreakRules
                    .Select(rule => new BreakRule
                    {
                        MinWorkedHours = Math.Max(0, rule.MinWorkedHours),
                        DeductedBreakHours = Math.Max(0, rule.DeductedBreakHours)
                    })
                    .OrderBy(rule => rule.MinWorkedHours)
                    .ToList()
            }
        };
    }

    private void Recalculate()
    {
        var settings = BuildSettingsDocument().BookingSettings;
        var gross = _calculator.GetGrossDuration(_segments, CurrentTime);
        var deductedBreak = _calculator.GetEffectiveBreakDeduction(gross, settings.BreakRules, _segments, settings.CountStampedOutTimeAsBreak);
        var net = _calculator.GetNetDuration(_segments, settings.BreakRules, CurrentTime, settings.CountStampedOutTimeAsBreak);

        GrossWorkedText = FormatDuration(gross);
        DeductedBreakText = FormatDuration(deductedBreak);
        NetWorkedText = FormatDuration(net);

        var targetReachedAt = _calculator.GetTargetReachedAt(_segments, settings.BreakRules, settings.DesiredWorkHours, CurrentTime, settings.CountStampedOutTimeAsBreak);
        if (settings.DesiredWorkHours <= 0)
        {
            DesiredEndTimeText = "Wunscharbeitszeit deaktiviert";
        }
        else if (net >= TimeSpan.FromHours(settings.DesiredWorkHours))
        {
            DesiredEndTimeText = $"Wunscharbeitszeit bereits erreicht ({targetReachedAt:HH:mm})";
        }
        else
        {
            DesiredEndTimeText = $"Wunscharbeitszeit erreicht um ca. {targetReachedAt:HH:mm}";
        }

        RefreshSegments();

        var alert = _calculator.EvaluateAlert(net, settings);
        AlertLevel = alert.Level;
        AlertMessage = alert.Message;
        ApplyAlertStyle(alert.Level);
    }

    private void RefreshSegments()
    {
        Segments.Clear();
        foreach (var segment in _segments)
        {
            var end = segment.End ?? CurrentTime;
            var duration = end - segment.Start;
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            Segments.Add(new WorkSegmentItemViewModel
            {
                Segment = segment,
                Start = segment.Start.ToString("HH:mm"),
                End = segment.End.HasValue ? segment.End.Value.ToString("HH:mm") : "Laeuft",
                Duration = FormatDuration(duration)
            });
        }
    }

    [RelayCommand]
    private void EditSegment(WorkSegmentItemViewModel? item)
    {
        if (item?.Segment is null)
        {
            return;
        }

        SelectedSegment = item.Segment;
        SelectedSegmentStartText = item.Segment.Start.ToString("HH:mm");
        SelectedSegmentEndText = item.Segment.End?.ToString("HH:mm") ?? string.Empty;
        SegmentEditMessage = "Zeiten anpassen und uebernehmen.";
    }

    [RelayCommand]
    private void CancelSegmentEdit()
    {
        SelectedSegment = null;
        SelectedSegmentStartText = string.Empty;
        SelectedSegmentEndText = string.Empty;
        SegmentEditMessage = "Arbeitsabschnitt auswaehlen, um Zeiten anzupassen.";
    }

    [RelayCommand]
    private void ApplySegmentEdit()
    {
        if (SelectedSegment is null)
        {
            SegmentEditMessage = "Bitte zuerst einen Arbeitsabschnitt auswaehlen.";
            return;
        }

        if (!TimeSpan.TryParse(SelectedSegmentStartText, out var startTime))
        {
            SegmentEditMessage = "Startzeit konnte nicht gelesen werden. Format HH:mm verwenden.";
            return;
        }

        var baseDate = SelectedSegment.Start.Date;
        var updatedStart = new DateTimeOffset(baseDate + startTime, SelectedSegment.Start.Offset);
        DateTimeOffset? updatedEnd = null;

        if (!string.IsNullOrWhiteSpace(SelectedSegmentEndText))
        {
            if (!TimeSpan.TryParse(SelectedSegmentEndText, out var endTime))
            {
                SegmentEditMessage = "Endzeit konnte nicht gelesen werden. Format HH:mm verwenden.";
                return;
            }

            updatedEnd = new DateTimeOffset(baseDate + endTime, SelectedSegment.Start.Offset);
            if (updatedEnd < updatedStart)
            {
                SegmentEditMessage = "Endzeit darf nicht vor der Startzeit liegen.";
                return;
            }
        }
        else if (SelectedSegment != _activeSegment)
        {
            SegmentEditMessage = "Fuer abgeschlossene Abschnitte muss eine Endzeit gesetzt sein.";
            return;
        }

        var candidateEnd = updatedEnd ?? CurrentTime;
        var overlapsExistingSegment = _segments
            .Where(segment => segment != SelectedSegment)
            .Any(segment => updatedStart < (segment.End ?? CurrentTime) && segment.Start < candidateEnd);

        if (overlapsExistingSegment)
        {
            SegmentEditMessage = "Der bearbeitete Abschnitt ueberlappt mit einem anderen Arbeitsabschnitt.";
            return;
        }

        SelectedSegment.Start = updatedStart;
        SelectedSegment.End = updatedEnd;

        _segments.Sort((left, right) => left.Start.CompareTo(right.Start));
        SegmentEditMessage = "Arbeitsabschnitt aktualisiert.";
        Recalculate();
        _ = SaveSettingsAsync();
    }

    private void ApplyAlertStyle(AlertLevel level)
    {
        switch (level)
        {
            case AlertLevel.Info:
                AlertBackground = new SolidColorBrush(Color.Parse("#DAF2E1"));
                AlertForeground = new SolidColorBrush(Color.Parse("#175529"));
                return;
            case AlertLevel.Warning:
                AlertBackground = new SolidColorBrush(Color.Parse("#FBE7C9"));
                AlertForeground = new SolidColorBrush(Color.Parse("#8A4A00"));
                return;
            case AlertLevel.Error:
                AlertBackground = new SolidColorBrush(Color.Parse("#FFDADB"));
                AlertForeground = new SolidColorBrush(Color.Parse("#8D1D1E"));
                return;
            default:
                AlertBackground = new SolidColorBrush(Color.Parse("#EEF2F6"));
                AlertForeground = new SolidColorBrush(Color.Parse("#1B2733"));
                break;
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}";
    }
}
