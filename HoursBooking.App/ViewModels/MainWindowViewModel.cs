using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
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
    private readonly IFileSaveDialogService _fileSaveDialogService;
    private readonly ISettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly DispatcherTimer _timer;
    private readonly List<WorkSegment> _segments = [];
    private readonly LanguageOption _systemLanguageOption;
    private WorkSegment? _activeSegment;
    private bool _isInitializing;
    private object[] _segmentEditMessageArguments = [];
    private string _segmentEditMessageKey = "SegmentEdit.SelectToAdjust";

    public MainWindowViewModel(
        BookingCalculator calculator,
        ISettingsStore settingsStore,
        IThemeService themeService,
        LocalizationService localizationService,
        IFileSaveDialogService fileSaveDialogService)
    {
        _calculator = calculator;
        _localizer = localizationService;
        _fileSaveDialogService = fileSaveDialogService;
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
        WeeklyDayAnalysisItems = new ObservableCollection<WeeklyDayAnalysisItemViewModel>();
        WeeklyTrendPoints = new ObservableCollection<WeeklyTrendPointItemViewModel>();
        Booking = new BookingViewModel(this);
        WeeklyAnalysis = new WeeklyAnalysisViewModel(calculator, localizationService);
        Settings = new SettingsViewModel(this);
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

    public ObservableCollection<WeeklyDayAnalysisItemViewModel> WeeklyDayAnalysisItems { get; }

    public ObservableCollection<WeeklyTrendPointItemViewModel> WeeklyTrendPoints { get; }

    public BookingViewModel Booking { get; }

    public WeeklyAnalysisViewModel WeeklyAnalysis { get; }

    public SettingsViewModel Settings { get; }

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
    private string weeklyGrossWorkedText = "00:00";

    [ObservableProperty]
    private string weeklyNetWorkedText = "00:00";

    [ObservableProperty]
    private string weeklyDesiredText = "00:00";

    [ObservableProperty]
    private string weeklyDifferenceText = "00:00";

    [ObservableProperty]
    private string weeklyDifferenceStatus = string.Empty;

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
    [NotifyCanExecuteChangedFor(nameof(ShowNextWeekCommand))]
    private int selectedWeekOffset;

    [ObservableProperty]
    private string selectedWeekRangeText = string.Empty;

    [ObservableProperty]
    private string weeklyTrendPolylinePoints = string.Empty;

    [ObservableProperty]
    private double maxWorkHours = 8.0;

    [ObservableProperty]
    private double desiredWorkHours = 7.5;

    [ObservableProperty]
    private double weeklyDesiredHours = 37.5;

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
    private string pendingComment = string.Empty;

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

    public string CommentLabel => _localizer["Grid.Comment"];

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

    public string WeeklyDesiredHoursLabel => _localizer["Settings.WeeklyDesiredHours"];

    public string CountStampedOutBreakLabel => _localizer["Settings.CountStampedOutBreak"];

    public string InfoThresholdLabel => _localizer["Settings.InfoThreshold"];

    public string WarningThresholdLabel => _localizer["Settings.WarningThreshold"];

    public string ErrorThresholdLabel => _localizer["Settings.ErrorThreshold"];

    public string BreakRulesTitle => _localizer["Settings.BreakRules"];

    public string BreakRulesFormatText => _localizer["Settings.BreakRulesFormat"];

    public string SaveSettingsText => _localizer["Settings.SaveNow"];

    public string PendingCommentLabel => _localizer["Segments.PendingCommentLabel"];

    public string PendingCommentHint => _localizer["Segments.PendingCommentHint"];

    public string ExportSegmentsText => _localizer["Segments.Export"];

    public string ClearSegmentsText => _localizer["Segments.ClearAll"];

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
        WeeklyDesiredHours = settings.WeeklyDesiredHours;
        CountStampedOutTimeAsBreak = settings.CountStampedOutTimeAsBreak;
        InfoThresholdMinutes = settings.InfoThresholdMinutes;
        WarningThresholdMinutes = settings.WarningThresholdMinutes;
        ErrorThresholdMinutes = settings.ErrorThresholdMinutes;

        _segments.Clear();
        foreach (var segment in document.WorkSegments.OrderBy(segment => segment.Start))
        {
            _segments.Add(new WorkSegment
            {
                Start = segment.Start,
                End = segment.End,
                Comment = NormalizeComment(segment.Comment)
            });
        }

        _activeSegment = _segments.LastOrDefault(segment => !segment.End.HasValue);
        IsClockedIn = _activeSegment is not null;

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

        _activeSegment = new WorkSegment
        {
            Start = DateTimeOffset.Now,
            Comment = NormalizeComment(PendingComment)
        };
        _segments.Add(_activeSegment);
        PendingComment = string.Empty;
        IsClockedIn = true;
        Recalculate();
        _ = SaveSettingsAsync();
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
        _ = SaveSettingsAsync();
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

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var document = BuildSettingsDocument();
        await _settingsStore.SaveAsync(document);
    }

    private bool CanClockIn() => !IsClockedIn;

    private bool CanClockOut() => IsClockedIn;

    private bool CanShowNextWeek() => SelectedWeekOffset < 0;

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

    partial void OnWeeklyDesiredHoursChanged(double value)
    {
        Recalculate();
        if (!_isInitializing)
        {
            _ = SaveSettingsAsync();
        }
    }

    partial void OnSelectedWeekOffsetChanged(int value)
    {
        Recalculate();
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

    partial void OnPendingCommentChanged(string value)
    {
        var normalized = NormalizeComment(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            PendingComment = normalized;
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
            WorkSegments = _segments
                .Select(segment => new WorkSegment
                {
                    Start = segment.Start,
                    End = segment.End,
                    Comment = NormalizeComment(segment.Comment)
                })
                .ToList(),
            BookingSettings = CreateCurrentBookingSettings()
        };
    }

    private BookingSettings CreateCurrentBookingSettings()
    {
        return new BookingSettings
        {
            MaxWorkHours = Math.Max(0.1, MaxWorkHours),
            DesiredWorkHours = Math.Max(0, DesiredWorkHours),
            WeeklyDesiredHours = Math.Max(0, WeeklyDesiredHours),
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
        };
    }

    private void Recalculate()
    {
        var settings = CreateCurrentBookingSettings();
        var daySegments = GetCurrentDaySegments();
        var gross = _calculator.GetGrossDuration(daySegments, CurrentTime);
        var deductedBreak = _calculator.GetEffectiveBreakDeduction(gross, settings.BreakRules, daySegments, settings.CountStampedOutTimeAsBreak);
        var net = _calculator.GetNetDuration(daySegments, settings.BreakRules, CurrentTime, settings.CountStampedOutTimeAsBreak);

        GrossWorkedText = FormatDuration(gross);
        DeductedBreakText = FormatDuration(deductedBreak);
        NetWorkedText = FormatDuration(net);
        ActiveClockInText = _activeSegment is not null
            ? _activeSegment.Start.ToString("HH:mm")
            : daySegments.Count == 0
                ? _localizer["ClockStatus.NotStarted"]
                : daySegments.MinBy(segment => segment.Start)?.Start.ToString("HH:mm") ?? _localizer["ClockStatus.NotStarted"];

        var targetReachedAt = _calculator.GetTargetReachedAt(daySegments, settings.BreakRules, settings.DesiredWorkHours, CurrentTime, settings.CountStampedOutTimeAsBreak);
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

        WeeklyAnalysis.Update(_segments, CurrentTime, settings);
        RefreshSegments(_segments);

        var alert = _calculator.EvaluateAlert(net, settings);
        AlertLevel = alert.Level;
        AlertMessage = _localizer.Format(alert.MessageKey, alert.FormatArguments);
        ApplyAlertStyle(alert.Level);
    }

    private void RefreshSegments(IEnumerable<WorkSegment> sourceSegments)
    {
        var existingRows = Segments
            .Where(item => item.Segment is not null)
            .ToDictionary(item => item.Segment!, item => item);
        var orderedSegments = sourceSegments.OrderBy(segment => segment.Start).ToList();
        var desiredRows = new List<WorkSegmentItemViewModel>(orderedSegments.Count);

        foreach (var segment in orderedSegments)
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
                row.Start = FormatSegmentDateTime(segment.Start);
                row.End = segment.End.HasValue ? FormatSegmentDateTime(segment.End.Value) : _localizer["SegmentStatus.Running"];
                row.Comment = segment.Comment;
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
        item.EditableComment = item.Segment.Comment;
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
            item.Start = FormatSegmentDateTime(item.Segment.Start);
            item.End = item.Segment.End.HasValue ? FormatSegmentDateTime(item.Segment.End.Value) : _localizer["SegmentStatus.Running"];
            item.Comment = item.Segment.Comment;
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
        editedSegment.Comment = NormalizeComment(item.EditableComment);

        _segments.Sort((left, right) => left.Start.CompareTo(right.Start));
        item.IsEditing = false;
        SetSegmentEditMessage("SegmentEdit.Updated");
        Recalculate();
        _ = SaveSettingsAsync();
    }

    [RelayCommand]
    private void RemoveSegment(WorkSegmentItemViewModel? item)
    {
        if (item?.Segment is null)
        {
            return;
        }

        var removedActive = ReferenceEquals(_activeSegment, item.Segment);
        _segments.Remove(item.Segment);
        if (removedActive)
        {
            _activeSegment = null;
            IsClockedIn = false;
        }

        SetSegmentEditMessage("SegmentEdit.Removed");
        Recalculate();
        _ = SaveSettingsAsync();
    }

    [RelayCommand]
    private void ClearAllSegments()
    {
        if (_segments.Count == 0)
        {
            SetSegmentEditMessage("SegmentList.AlreadyEmpty");
            return;
        }

        _segments.Clear();
        _activeSegment = null;
        IsClockedIn = false;
        SetSegmentEditMessage("SegmentList.Cleared");
        Recalculate();
        _ = SaveSettingsAsync();
    }

    [RelayCommand]
    private async Task ExportSegments()
    {
        if (_segments.Count == 0)
        {
            SetSegmentEditMessage("SegmentList.ExportEmpty");
            return;
        }

        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var exportFilePath = await _fileSaveDialogService.PickCsvSavePathAsync($"segments-{timestamp}.csv");
        if (string.IsNullOrWhiteSpace(exportFilePath))
        {
            SetSegmentEditMessage("SegmentList.ExportCanceled");
            return;
        }

        if (!string.Equals(Path.GetExtension(exportFilePath), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            exportFilePath += ".csv";
        }

        var csv = new StringBuilder();
        csv.AppendLine("start,end,duration_minutes,comment");

        foreach (var segment in _segments.OrderBy(segment => segment.Start))
        {
            var end = segment.End;
            var duration = (end ?? CurrentTime) - segment.Start;
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            csv.Append(EscapeCsv(segment.Start.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)));
            csv.Append(',');
            csv.Append(EscapeCsv(end?.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) ?? string.Empty));
            csv.Append(',');
            csv.Append(((int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture));
            csv.Append(',');
            csv.Append(EscapeCsv(segment.Comment));
            csv.AppendLine();
        }

        File.WriteAllText(exportFilePath, csv.ToString(), Encoding.UTF8);
        SetSegmentEditMessage("SegmentList.ExportSaved", exportFilePath);
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

    private List<WorkSegment> GetCurrentDaySegments()
    {
        var currentDay = CurrentTime.LocalDateTime.Date;
        return _segments
            .Where(segment => segment.Start.ToLocalTime().Date == currentDay)
            .OrderBy(segment => segment.Start)
            .ToList();
    }

    private List<WorkSegment> GetCurrentWeekSegments()
    {
        var weekStart = GetWeekStartForOffset(SelectedWeekOffset);
        return GetWeekSegments(weekStart);
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
        var currentDate = CurrentTime.LocalDateTime.Date;
        var dayOfWeek = currentDate.DayOfWeek;
        var daysUntilMonday = dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
        return currentDate.AddDays(-daysUntilMonday);
    }

    private DateTime GetWeekStartForOffset(int weekOffset)
    {
        return GetCurrentWeekStart().AddDays(weekOffset * 7);
    }

    private void RecalculateWeeklyMetrics(BookingSettings settings)
    {
        var weekSegments = GetCurrentWeekSegments();
        var weeklyGross = _calculator.GetGrossDuration(weekSegments, CurrentTime);
        var weekStart = GetWeekStartForOffset(SelectedWeekOffset);
        var weekEnd = weekStart.AddDays(6);

        var weeklyNet = TimeSpan.Zero;
        var dailySegments = new Dictionary<DateTime, List<WorkSegment>>();

        // Group segments by day
        foreach (var segment in weekSegments)
        {
            var date = segment.Start.ToLocalTime().Date;
            if (!dailySegments.ContainsKey(date))
            {
                dailySegments[date] = [];
            }
            dailySegments[date].Add(segment);
        }

        // Calculate net for each day and sum up
        foreach (var daySegments in dailySegments.Values)
        {
            var dayNet = _calculator.GetNetDuration(daySegments, settings.BreakRules, CurrentTime, settings.CountStampedOutTimeAsBreak);
            weeklyNet += dayNet;
        }

        var weeklyDesired = TimeSpan.FromHours(Math.Max(0, settings.WeeklyDesiredHours));
        var difference = weeklyNet - weeklyDesired;

        WeeklyGrossWorkedText = FormatDuration(weeklyGross);
        WeeklyNetWorkedText = FormatDuration(weeklyNet);
        WeeklyDesiredText = FormatDuration(weeklyDesired);
        WeeklyDifferenceText = FormatDuration(TimeSpan.FromTicks(Math.Abs(difference.Ticks)));
        SelectedWeekRangeText = _localizer.Format("Analytics.WeekRange", weekStart, weekEnd);

        // Set status: positive = surplus, negative = deficit
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

        RecalculateWeeklyAnalytics(settings, weekStart, dailySegments);
        RecalculateWeeklyTrend(settings);
    }

    private void RecalculateWeeklyTrend(BookingSettings settings)
    {
        WeeklyTrendPoints.Clear();

        const int trendWeeks = 8;
        var startOffset = SelectedWeekOffset - (trendWeeks - 1);
        var totals = new List<double>(trendWeeks);

        for (var offset = startOffset; offset <= SelectedWeekOffset; offset++)
        {
            var weekStart = GetWeekStartForOffset(offset);
            var weekSegments = GetWeekSegments(weekStart);
            var weekTotal = CalculateWeeklyNetDuration(weekSegments, settings);
            totals.Add(weekTotal.TotalMinutes);

            WeeklyTrendPoints.Add(new WeeklyTrendPointItemViewModel
            {
                WeekLabel = _localizer.Format("Analytics.WeekShort", ISOWeek.GetWeekOfYear(weekStart)),
                RangeLabel = weekStart.ToString("dd.MM", _localizer.CurrentCulture),
                NetWorkedText = FormatDuration(weekTotal)
            });
        }

        WeeklyTrendPolylinePoints = BuildPolylinePoints(totals, Math.Max(60, settings.WeeklyDesiredHours * 60));
    }

    private TimeSpan CalculateWeeklyNetDuration(IEnumerable<WorkSegment> weekSegments, BookingSettings settings)
    {
        var dayGroups = weekSegments
            .GroupBy(segment => segment.Start.ToLocalTime().Date)
            .Select(group => _calculator.GetNetDuration(group, settings.BreakRules, CurrentTime, settings.CountStampedOutTimeAsBreak));

        return dayGroups.Aggregate(TimeSpan.Zero, (current, value) => current + value);
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

    private void RecalculateWeeklyAnalytics(
        BookingSettings settings,
        DateTime weekStart,
        IReadOnlyDictionary<DateTime, List<WorkSegment>> dailySegments)
    {
        WeeklyDayAnalysisItems.Clear();

        var dayTargetMinutes = Math.Max(0, settings.WeeklyDesiredHours) / 5d * 60d;
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

            var dayNet = _calculator.GetNetDuration(daySegmentsForDate, settings.BreakRules, CurrentTime, settings.CountStampedOutTimeAsBreak);
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

    private static string NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return string.Empty;
        }

        var trimmed = comment.Trim();
        return trimmed.Length <= WorkSegmentItemViewModel.MaxCommentLength
            ? trimmed
            : trimmed[..WorkSegmentItemViewModel.MaxCommentLength];
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private string FormatSegmentDateTime(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("dd.MM HH:mm", _localizer.CurrentCulture);
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
        OnPropertyChanged(nameof(CommentLabel));
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
        OnPropertyChanged(nameof(WeeklyDesiredHoursLabel));
        OnPropertyChanged(nameof(CountStampedOutBreakLabel));
        OnPropertyChanged(nameof(InfoThresholdLabel));
        OnPropertyChanged(nameof(WarningThresholdLabel));
        OnPropertyChanged(nameof(ErrorThresholdLabel));
        OnPropertyChanged(nameof(BreakRulesTitle));
        OnPropertyChanged(nameof(BreakRulesFormatText));
        OnPropertyChanged(nameof(SaveSettingsText));
        OnPropertyChanged(nameof(PendingCommentLabel));
        OnPropertyChanged(nameof(PendingCommentHint));
        OnPropertyChanged(nameof(ExportSegmentsText));
        OnPropertyChanged(nameof(ClearSegmentsText));
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
        OnPropertyChanged(nameof(CurrentDayLabel));
    }

    private void SetSegmentEditMessage(string key, params object[] arguments)
    {
        _segmentEditMessageKey = key;
        _segmentEditMessageArguments = arguments;
        SegmentEditMessage = _localizer.Format(key, arguments);
    }
}
