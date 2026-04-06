using Avalonia.Headless.NUnit;
using HoursBooking.App.Services;
using HoursBooking.App.ViewModels;
using HoursBooking.Core.Models;
using HoursBooking.Core.Services;

namespace HoursBooking.IntegrationTests;

public class WeeklyAnalysisViewModelIntegrationTests
{
    [AvaloniaTest]
    public void Update_PopulatesWeeklyMetricsAndTrendData()
    {
        var localizer = new LocalizationService();
        localizer.SetLanguageCode("en");
        var viewModel = new WeeklyAnalysisViewModel(new BookingCalculator(), localizer);

        var monday = new DateTimeOffset(2026, 4, 6, 8, 0, 0, TimeSpan.Zero);
        var segments = new List<WorkSegment>
        {
            new() { Start = monday, End = monday.AddHours(8) },
            new() { Start = monday.AddDays(1), End = monday.AddDays(1).AddHours(7.5) }
        };

        var settings = new BookingSettings
        {
            WeeklyDesiredHours = 37.5,
            BreakRules =
            [
                new BreakRule { MinWorkedHours = 0, DeductedBreakHours = 0.25 },
                new BreakRule { MinWorkedHours = 6, DeductedBreakHours = 0.75 }
            ]
        };

        viewModel.Update(segments, monday.AddDays(2), settings);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.WeeklyNetWorkedText, Is.Not.EqualTo("00:00"));
            Assert.That(viewModel.SelectedWeekRangeText, Is.Not.Empty);
            Assert.That(viewModel.WeeklyDayAnalysisItems.Count, Is.EqualTo(7));
            Assert.That(viewModel.WeeklyTrendPoints.Count, Is.EqualTo(8));
            Assert.That(viewModel.WeeklyTrendPolylinePoints, Is.Not.Empty);
            Assert.That(viewModel.ShowWeeklyNoData, Is.False);
        });
    }

    [AvaloniaTest]
    public void WeekNavigation_ChangesOffsetAndAllowsReturnToCurrentWeek()
    {
        var localizer = new LocalizationService();
        localizer.SetLanguageCode("en");
        var viewModel = new WeeklyAnalysisViewModel(new BookingCalculator(), localizer);

        var monday = new DateTimeOffset(2026, 4, 6, 8, 0, 0, TimeSpan.Zero);
        var segments = new List<WorkSegment>
        {
            new() { Start = monday, End = monday.AddHours(8) }
        };

        viewModel.Update(segments, monday, new BookingSettings());

        viewModel.ShowPreviousWeekCommand.Execute(null);
        var canGoNext = viewModel.ShowNextWeekCommand.CanExecute(null);
        viewModel.ShowNextWeekCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedWeekOffset, Is.EqualTo(0));
            Assert.That(canGoNext, Is.True);
        });
    }
}
