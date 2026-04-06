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
    private readonly LocalizationService _localizer;
    private readonly ISettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly DispatcherTimer _timer;
    private readonly List<WorkSegment> _segments = [];
    private readonly LanguageOption _systemLanguageOption;
    private WorkSegment? _activeSegment;
    private bool _isInitializing;
    private object[] _segmentEditMessageArguments = [];
    private string _segmentEditMessageKey = "SegmentEdit.SelectToAdjust";

    public MainWindowViewModel(BookingCalculator calculator, ISettingsStore settingsStore, IThemeService themeService, LocalizationService localizationService)
    {
        _calculator = calculator;
        _localizer = localizationService;
        _settingsStore = settingsStore;
        _themeService = themeService;
        _localizer.LanguageChanged += OnLanguageChanged;

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
        LanguageOptions = new ObservableCollection<LanguageOption>();
        Segments = new ObservableCollection<WorkSegmentItemViewModel>();
        ThemeOptions = new ObservableCollection<LocalizedOption<ThemeMode>>
        {
            new() { Value = ThemeMode.System },
            new() { Value = ThemeMode.Light },
            new() { Value = ThemeMode.Dark }
        };
        TrayDisplayOptions = new ObservableCollection<LocalizedOption<TrayDisplayMode>>
        {
            new() { Value = TrayDisplayMode.Icon },
            new() { Value = TrayDisplayMode.NetWorkedTime }
        };

        _systemLanguageOption = new LanguageOption { Code = string.Empty, DisplayName = string.Empty };
        LanguageOptions.Add(_systemLanguageOption);
        foreach (var language in _localizer.AvailableLanguages)
        {
            LanguageOptions.Add(new LanguageOption
            {
                Code = language.Code,
                DisplayName = language.DisplayName
            });
        }

        RefreshLocalizedOptionDisplayNames();
        AlertMessage = _localizer["Alert.NoWarning"];
        DesiredEndTimeText = _localizer["DesiredEnd.NotStarted"];
        SegmentEditMessage = _localizer["SegmentEdit.SelectToAdjust"];
    }

    public ObservableCollection<BreakRuleViewModel> BreakRules { get; }

    public ObservableCollection<LanguageOption> LanguageOptions { get; }

    public ObservableCollection<WorkSegmentItemViewModel> Segments { get; }

    public ObservableCollection<LocalizedOption<ThemeMode>> ThemeOptions { get; }

    public ObservableCollection<LocalizedOption<TrayDisplayMode>> TrayDisplayOptions { get; }

    public LocalizationService Localizer => _localizer;

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
    private string alertMessage = string.Empty;

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
    private LocalizedOption<ThemeMode>? selectedThemeOption;

    [ObservableProperty]
    private bool minimizeToTrayOnClose;

    [ObservableProperty]
    private TrayDisplayMode trayDisplayMode;

    [ObservableProperty]
    private LocalizedOption<TrayDisplayMode>? selectedTrayDisplayOption;

    [ObservableProperty]
    private LanguageOption? selectedLanguageOption;

    [ObservableProperty]
    private bool hasSeenTrayMinimizeHint;

    [ObservableProperty]
    private string desiredEndTimeText = string.Empty;

    [ObservableProperty]
    private string segmentEditMessage = string.Empty;

    [ObservableProperty]
    private bool showWindowHeader = true;

    [ObservableProperty]
    private bool showHeaderDate = true;

    [ObservableProperty]
    private bool showHeaderClock = true;

    [ObservableProperty]
    private bool showSettingsTab = true;

    [ObservableProperty]
    private bool showSegmentsSection = true;

    [ObservableProperty]
    private bool showDetailedMetrics = true;

    [ObservableProperty]
    private bool showAlertPanel = true;

    [ObservableProperty]
    private bool showClockInSummary;

    [ObservableProperty]
    private bool showBookingTitle = true;

    [ObservableProperty]
    private bool showMinimalClockMode;

    [ObservableProperty]
    private bool showStandardBookingCard = true;

    [ObservableProperty]
    private bool showSegmentTableLayout = true;

    [ObservableProperty]
    private bool showSegmentCardLayout;

    [ObservableProperty]
    private string activeClockInText = string.Empty;

    public string CurrentDayLabel => CurrentTime.ToString("dddd, dd.MM.yyyy", _localizer.CurrentCulture);

    public string CurrentTimeLabel => CurrentTime.ToString("HH:mm:ss");

    public bool HasAlert => AlertLevel != AlertLevel.None;

    public string WindowTitle => _localizer["Window.Title"];

    public string HeaderTitle => _localizer["Header.Title"];

    public string BookingTabHeader => _localizer["Tabs.Booking"];

    public string SettingsTabHeader => _localizer["Tabs.Settings"];

    public string CompactStartLabel => _localizer["Compact.Start"];

    public string CompactNetLabel => _localizer["Compact.Net"];

    public string ClockSectionTitle => _localizer["ClockSection.Title"];

    public string ClockInText => _localizer["Actions.ClockIn"];

    public string ClockOutText => _localizer["Actions.ClockOut"];

    public string EditText => _localizer["Actions.Edit"];

    public string ApplyText => _localizer["Actions.Apply"];

    public string CancelText => _localizer["Actions.Cancel"];

    public string RemoveText => _localizer["Actions.Remove"];

    public string AddRuleText => _localizer["Actions.AddRule"];

    public string SummaryClockedInLabel => _localizer["Summary.ClockedIn"];

    public string SummaryNetLabel => _localizer["Summary.Net"];

    public string GrossLabel => _localizer["Metrics.Gross"];

    public string BreakDeductionLabel => _localizer["Metrics.BreakDeduction"];

    public string NetLabel => _localizer["Metrics.Net"];

    public string WorkSegmentsTitle => _localizer["Sections.WorkSegments"];

    public string StartLabel => _localizer["Grid.Start"];

    public string EndLabel => _localizer["Grid.End"];

    public string DurationLabel => _localizer["Grid.Duration"];

    public string ActionLabel => _localizer["Grid.Action"];

    public string CardDurationLabel => _localizer["Cards.Duration"];

    public string DisplaySettingsTitle => _localizer["Settings.Display"];

    public string ThemeLabel => _localizer["Settings.Theme"];

    public string LanguageLabel => _localizer["Settings.Language"];

    public string MinimizeToTrayOnCloseLabel => _localizer["Settings.MinimizeToTrayOnClose"];

    public string TrayDisplayLabel => _localizer["Settings.TrayDisplay"];

    public string TraySupportHintText => _localizer["Settings.TraySupportHint"];

    public string AlertLevelsTitle => _localizer["Settings.AlertLevels"];

    public string MaxWorkHoursLabel => _localizer["Settings.MaxWorkHours"];

    public string DesiredWorkHoursLabel => _localizer["Settings.DesiredWorkHours"];

    public string CountStampedOutBreakLabel => _localizer["Settings.CountStampedOutBreak"];

    public string InfoThresholdLabel => _localizer["Settings.InfoThreshold"];

    public string WarningThresholdLabel => _localizer["Settings.WarningThreshold"];

    public string ErrorThresholdLabel => _localizer["Settings.ErrorThreshold"];

    public string BreakRulesTitle => _localizer["Settings.BreakRules"];

    public string BreakRulesFormatText => _localizer["Settings.BreakRulesFormat"];

    public string SaveSettingsText => _localizer["Settings.SaveNow"];

    public async Task InitializeAsync()
    {
        _isInitializing = true;

        var document = await _settingsStore.LoadAsync();
        var settings = document.BookingSettings;

        _localizer.SetLanguageCode(document.LanguageCode);
        SelectedThemeMode = document.ThemeMode;
        SelectedThemeOption = ThemeOptions.FirstOrDefault(option => option.Value == SelectedThemeMode);
        MinimizeToTrayOnClose = document.MinimizeToTrayOnClose;
        TrayDisplayMode = TrayDisplayMode.Icon;
        HasSeenTrayMinimizeHint = document.HasSeenTrayMinimizeHint;
        SelectedLanguageOption = LanguageOptions.FirstOrDefault(option => option.Code == document.LanguageCode) ?? _systemLanguageOption;
        SelectedTrayDisplayOption = TrayDisplayOptions.First(option => option.Value == TrayDisplayMode);
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
        SelectedThemeOption = ThemeOptions.FirstOrDefault(option => option.Value == value);
        _themeService.ApplyTheme(value);
        if (!_isInitializing)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnSelectedThemeOptionChanged(LocalizedOption<ThemeMode>? value)
    {
        if (value is not null && value.Value != SelectedThemeMode)
        {
            SelectedThemeMode = value.Value;
        }
    }

    partial void OnMinimizeToTrayOnCloseChanged(bool value)
    {
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
            LanguageCode = SelectedLanguageOption?.Code ?? string.Empty,
            MinimizeToTrayOnClose = MinimizeToTrayOnClose,
            TrayDisplayMode = TrayDisplayMode.Icon,
            HasSeenTrayMinimizeHint = HasSeenTrayMinimizeHint,
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
        ActiveClockInText = _activeSegment is not null
            ? _activeSegment.Start.ToString("HH:mm")
            : _segments.Count == 0
                ? _localizer["ClockStatus.NotStarted"]
                : _segments.MinBy(segment => segment.Start)?.Start.ToString("HH:mm") ?? _localizer["ClockStatus.NotStarted"];

        var targetReachedAt = _calculator.GetTargetReachedAt(_segments, settings.BreakRules, settings.DesiredWorkHours, CurrentTime, settings.CountStampedOutTimeAsBreak);
        if (settings.DesiredWorkHours <= 0)
        {
            DesiredEndTimeText = _localizer["DesiredEnd.Disabled"];
        }
        else if (net >= TimeSpan.FromHours(settings.DesiredWorkHours))
        {
            DesiredEndTimeText = _localizer.Format("DesiredEnd.AlreadyReached", targetReachedAt);
        }
        else
        {
            DesiredEndTimeText = _localizer.Format("DesiredEnd.ReachesAt", targetReachedAt);
        }

        RefreshSegments();

        var alert = _calculator.EvaluateAlert(net, settings);
        AlertLevel = alert.Level;
        AlertMessage = _localizer.Format(alert.MessageKey, alert.FormatArguments);
        ApplyAlertStyle(alert.Level);
    }

    private void RefreshSegments()
    {
        var existingRows = Segments
            .Where(item => item.Segment is not null)
            .ToDictionary(item => item.Segment!, item => item);
        var desiredRows = new List<WorkSegmentItemViewModel>(_segments.Count);

        foreach (var segment in _segments)
        {
            var end = segment.End ?? CurrentTime;
            var duration = end - segment.Start;
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            if (!existingRows.TryGetValue(segment, out var row))
            {
                row = new WorkSegmentItemViewModel
                {
                    Segment = segment
                };
            }

            if (!row.IsEditing)
            {
                row.Start = segment.Start.ToString("HH:mm");
                row.End = segment.End.HasValue ? segment.End.Value.ToString("HH:mm") : _localizer["SegmentStatus.Running"];
            }

            row.Duration = FormatDuration(duration);
            desiredRows.Add(row);
        }

        for (var index = 0; index < desiredRows.Count; index++)
        {
            var desiredRow = desiredRows[index];

            if (index >= Segments.Count)
            {
                Segments.Add(desiredRow);
                continue;
            }

            if (ReferenceEquals(Segments[index], desiredRow))
            {
                continue;
            }

            var existingIndex = Segments.IndexOf(desiredRow);
            if (existingIndex >= 0)
            {
                Segments.Move(existingIndex, index);
            }
            else
            {
                Segments.Insert(index, desiredRow);
            }
        }

        while (Segments.Count > desiredRows.Count)
        {
            Segments.RemoveAt(Segments.Count - 1);
        }
    }

    [RelayCommand]
    private void EditSegment(WorkSegmentItemViewModel? item)
    {
        if (item?.Segment is null)
        {
            return;
        }

        foreach (var segmentItem in Segments)
        {
            segmentItem.IsEditing = false;
        }

        item.EditableStartTime = item.Segment.Start.TimeOfDay;
        item.EditableEndTime = item.Segment.End?.TimeOfDay;
        item.IsEditing = true;
        SetSegmentEditMessage("SegmentEdit.AdjustAndApply");
    }

    [RelayCommand]
    private void CancelSegmentEdit(WorkSegmentItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        item.IsEditing = false;
        if (item.Segment is not null)
        {
            item.Start = item.Segment.Start.ToString("HH:mm");
            item.End = item.Segment.End.HasValue ? item.Segment.End.Value.ToString("HH:mm") : _localizer["SegmentStatus.Running"];
        }

        SetSegmentEditMessage("SegmentEdit.SelectToAdjust");
    }

    [RelayCommand]
    private void ApplySegmentEdit(WorkSegmentItemViewModel? item)
    {
        if (item?.Segment is null)
        {
            SetSegmentEditMessage("SegmentEdit.SelectFirst");
            return;
        }

        var startTime = item.EditableStartTime;

        var editedSegment = item.Segment;
        var baseDate = editedSegment.Start.Date;
        var updatedStart = new DateTimeOffset(baseDate + startTime, editedSegment.Start.Offset);
        DateTimeOffset? updatedEnd = null;

        if (item.EditableEndTime.HasValue)
        {
            var endTime = item.EditableEndTime.Value;
            updatedEnd = new DateTimeOffset(baseDate + endTime, editedSegment.Start.Offset);
            if (updatedEnd < updatedStart)
            {
                SetSegmentEditMessage("SegmentEdit.EndBeforeStart");
                return;
            }
        }
        else if (editedSegment != _activeSegment)
        {
            SetSegmentEditMessage("SegmentEdit.CompletedNeedsEnd");
            return;
        }

        var candidateEnd = updatedEnd ?? CurrentTime;
        var overlapsExistingSegment = _segments
            .Where(segment => segment != editedSegment)
            .Any(segment => updatedStart < (segment.End ?? CurrentTime) && segment.Start < candidateEnd);

        if (overlapsExistingSegment)
        {
            SetSegmentEditMessage("SegmentEdit.Overlap");
            return;
        }

        editedSegment.Start = updatedStart;
        editedSegment.End = updatedEnd;

        _segments.Sort((left, right) => left.Start.CompareTo(right.Start));
        item.IsEditing = false;
        SetSegmentEditMessage("SegmentEdit.Updated");
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

    public void UpdateLayoutMode(double windowWidth)
    {
        ShowMinimalClockMode = windowWidth < 620;
        ShowStandardBookingCard = !ShowMinimalClockMode;
        ShowSettingsTab = windowWidth >= 620;
        ShowSegmentsSection = windowWidth >= 780;
        ShowSegmentTableLayout = windowWidth >= 1100;
        ShowSegmentCardLayout = ShowSegmentsSection && !ShowSegmentTableLayout;
        ShowDetailedMetrics = windowWidth >= 980;
        ShowAlertPanel = windowWidth >= 860;
        ShowClockInSummary = windowWidth < 980 && !ShowMinimalClockMode;
        ShowWindowHeader = windowWidth >= 700;
        ShowHeaderDate = windowWidth >= 820;
        ShowHeaderClock = windowWidth >= 1040;
        ShowBookingTitle = windowWidth >= 760;
    }

    partial void OnSelectedLanguageOptionChanged(LanguageOption? value)
    {
        _localizer.SetLanguageCode(value?.Code);
        if (!_isInitializing)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnTrayDisplayModeChanged(TrayDisplayMode value)
    {
        SelectedTrayDisplayOption = TrayDisplayOptions.FirstOrDefault(option => EqualityComparer<TrayDisplayMode>.Default.Equals(option.Value, value));
        if (!_isInitializing)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnSelectedTrayDisplayOptionChanged(LocalizedOption<TrayDisplayMode>? value)
    {
        if (value is not null && !EqualityComparer<TrayDisplayMode>.Default.Equals(TrayDisplayMode, value.Value))
        {
            TrayDisplayMode = value.Value;
        }
    }

    public void MarkTrayMinimizeHintAsSeen()
    {
        if (HasSeenTrayMinimizeHint)
        {
            return;
        }

        HasSeenTrayMinimizeHint = true;
        if (!_isInitializing)
        {
            _ = SaveSettingsAsync();
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RefreshLocalizedOptionDisplayNames();
        RaiseLocalizedPropertyChanges();
        SetSegmentEditMessage(_segmentEditMessageKey, _segmentEditMessageArguments);
        Recalculate();
    }

    private void RefreshLocalizedOptionDisplayNames()
    {
        _systemLanguageOption.DisplayName = _localizer["Language.System"];

        foreach (var option in ThemeOptions)
        {
            option.DisplayName = option.Value switch
            {
                ThemeMode.System => _localizer["Theme.System"],
                ThemeMode.Light => _localizer["Theme.Light"],
                ThemeMode.Dark => _localizer["Theme.Dark"],
                _ => option.Value.ToString() ?? string.Empty
            };
        }

        foreach (var option in TrayDisplayOptions)
        {
            option.DisplayName = option.Value switch
            {
                TrayDisplayMode.Icon => _localizer["TrayDisplay.Icon"],
                TrayDisplayMode.NetWorkedTime => _localizer["TrayDisplay.NetWorkedTime"],
                _ => option.Value.ToString() ?? string.Empty
            };
        }
    }

    private void RaiseLocalizedPropertyChanges()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(BookingTabHeader));
        OnPropertyChanged(nameof(SettingsTabHeader));
        OnPropertyChanged(nameof(CompactStartLabel));
        OnPropertyChanged(nameof(CompactNetLabel));
        OnPropertyChanged(nameof(ClockSectionTitle));
        OnPropertyChanged(nameof(ClockInText));
        OnPropertyChanged(nameof(ClockOutText));
        OnPropertyChanged(nameof(EditText));
        OnPropertyChanged(nameof(ApplyText));
        OnPropertyChanged(nameof(CancelText));
        OnPropertyChanged(nameof(RemoveText));
        OnPropertyChanged(nameof(AddRuleText));
        OnPropertyChanged(nameof(SummaryClockedInLabel));
        OnPropertyChanged(nameof(SummaryNetLabel));
        OnPropertyChanged(nameof(GrossLabel));
        OnPropertyChanged(nameof(BreakDeductionLabel));
        OnPropertyChanged(nameof(NetLabel));
        OnPropertyChanged(nameof(WorkSegmentsTitle));
        OnPropertyChanged(nameof(StartLabel));
        OnPropertyChanged(nameof(EndLabel));
        OnPropertyChanged(nameof(DurationLabel));
        OnPropertyChanged(nameof(ActionLabel));
        OnPropertyChanged(nameof(CardDurationLabel));
        OnPropertyChanged(nameof(DisplaySettingsTitle));
        OnPropertyChanged(nameof(ThemeLabel));
        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(MinimizeToTrayOnCloseLabel));
        OnPropertyChanged(nameof(TrayDisplayLabel));
        OnPropertyChanged(nameof(TraySupportHintText));
        OnPropertyChanged(nameof(AlertLevelsTitle));
        OnPropertyChanged(nameof(MaxWorkHoursLabel));
        OnPropertyChanged(nameof(DesiredWorkHoursLabel));
        OnPropertyChanged(nameof(CountStampedOutBreakLabel));
        OnPropertyChanged(nameof(InfoThresholdLabel));
        OnPropertyChanged(nameof(WarningThresholdLabel));
        OnPropertyChanged(nameof(ErrorThresholdLabel));
        OnPropertyChanged(nameof(BreakRulesTitle));
        OnPropertyChanged(nameof(BreakRulesFormatText));
        OnPropertyChanged(nameof(SaveSettingsText));
        OnPropertyChanged(nameof(CurrentDayLabel));
    }

    private void SetSegmentEditMessage(string key, params object[] arguments)
    {
        _segmentEditMessageKey = key;
        _segmentEditMessageArguments = arguments;
        SegmentEditMessage = _localizer.Format(key, arguments);
    }
}
