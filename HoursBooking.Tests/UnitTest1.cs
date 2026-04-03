using HoursBooking.Core.Models;
using HoursBooking.Core.Services;

namespace HoursBooking.Tests;

public class BookingCalculatorTests
{
    private readonly BookingCalculator _calculator = new();

    [Test]
    public void GetDeductedBreak_UsesHighestMatchingRule()
    {
        var rules = new List<BreakRule>
        {
            new() { MinWorkedHours = 0, DeductedBreakHours = 0.25 },
            new() { MinWorkedHours = 6, DeductedBreakHours = 0.75 },
            new() { MinWorkedHours = 9, DeductedBreakHours = 1.0 }
        };

        var deducted = _calculator.GetDeductedBreak(TimeSpan.FromHours(7), rules);

        Assert.That(deducted.TotalHours, Is.EqualTo(0.75).Within(0.001));
    }

    [Test]
    public void EvaluateAlert_ReturnsInfoWarningErrorByThreshold()
    {
        var settings = new BookingSettings
        {
            MaxWorkHours = 8,
            InfoThresholdMinutes = 90,
            WarningThresholdMinutes = 60,
            ErrorThresholdMinutes = 20
        };

        var infoAlert = _calculator.EvaluateAlert(TimeSpan.FromHours(6.8), settings);
        var warningAlert = _calculator.EvaluateAlert(TimeSpan.FromHours(7.2), settings);
        var errorAlert = _calculator.EvaluateAlert(TimeSpan.FromHours(7.75), settings);

        Assert.Multiple(() =>
        {
            Assert.That(infoAlert.Level, Is.EqualTo(AlertLevel.Info));
            Assert.That(warningAlert.Level, Is.EqualTo(AlertLevel.Warning));
            Assert.That(errorAlert.Level, Is.EqualTo(AlertLevel.Error));
        });
    }

    [Test]
    public void EvaluateAlert_WhenMaxExceeded_ReturnsErrorWithExceededMessage()
    {
        var settings = new BookingSettings { MaxWorkHours = 8 };

        var alert = _calculator.EvaluateAlert(TimeSpan.FromHours(8.25), settings);

        Assert.Multiple(() =>
        {
            Assert.That(alert.Level, Is.EqualTo(AlertLevel.Error));
            Assert.That(alert.Message, Does.Contain("ueberschritten"));
        });
    }

    [Test]
    public void GetNetDuration_NeverReturnsNegative()
    {
        var now = DateTimeOffset.Now;
        var segments = new List<WorkSegment>
        {
            new() { Start = now.AddMinutes(-10), End = now }
        };
        var rules = new List<BreakRule>
        {
            new() { MinWorkedHours = 0, DeductedBreakHours = 1.0 }
        };

        var net = _calculator.GetNetDuration(segments, rules, now);

        Assert.That(net, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void EvaluateAlert_UsesNetTimeNotGrossTime()
    {
        var now = DateTimeOffset.Now;
        var segments = new List<WorkSegment>
        {
            new() { Start = now.AddHours(-8), End = now }
        };

        var rules = new List<BreakRule>
        {
            new() { MinWorkedHours = 0, DeductedBreakHours = 0.25 },
            new() { MinWorkedHours = 6, DeductedBreakHours = 0.75 }
        };

        var settings = new BookingSettings
        {
            MaxWorkHours = 7.5,
            InfoThresholdMinutes = 30,
            WarningThresholdMinutes = 15,
            ErrorThresholdMinutes = 5,
            BreakRules = rules
        };

        var net = _calculator.GetNetDuration(segments, rules, now);
        var alert = _calculator.EvaluateAlert(net, settings);

        Assert.That(alert.Level, Is.Not.EqualTo(AlertLevel.Error));
    }

    [Test]
    public void GetTargetReachedAt_ProjectsExpectedTimeWithBreakJumps()
    {
        var start = new DateTimeOffset(2026, 4, 3, 8, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 4, 3, 12, 0, 0, TimeSpan.Zero);

        var segments = new List<WorkSegment>
        {
            new() { Start = start, End = now }
        };

        var rules = new List<BreakRule>
        {
            new() { MinWorkedHours = 0, DeductedBreakHours = 0.25 },
            new() { MinWorkedHours = 6, DeductedBreakHours = 0.75 }
        };

        var projected = _calculator.GetTargetReachedAt(segments, rules, desiredNetHours: 6, now);

        Assert.That(projected, Is.EqualTo(new DateTimeOffset(2026, 4, 3, 14, 45, 0, TimeSpan.Zero)));
    }

    [Test]
    public void GetEffectiveBreakDeduction_CanCreditStampedOutGaps()
    {
        var start = new DateTimeOffset(2026, 4, 3, 8, 0, 0, TimeSpan.Zero);
        var segments = new List<WorkSegment>
        {
            new() { Start = start, End = start.AddHours(1) },
            new() { Start = start.AddHours(1.5), End = start.AddHours(7.5) }
        };

        var rules = new List<BreakRule>
        {
            new() { MinWorkedHours = 0, DeductedBreakHours = 0.25 },
            new() { MinWorkedHours = 6, DeductedBreakHours = 0.75 }
        };

        var gross = _calculator.GetGrossDuration(segments, start.AddHours(7.5));
        var creditedDeduction = _calculator.GetEffectiveBreakDeduction(gross, rules, segments, countStampedOutTimeAsBreak: true);
        var plainDeduction = _calculator.GetEffectiveBreakDeduction(gross, rules, segments, countStampedOutTimeAsBreak: false);

        Assert.Multiple(() =>
        {
            Assert.That(plainDeduction, Is.EqualTo(TimeSpan.FromMinutes(45)));
            Assert.That(creditedDeduction, Is.EqualTo(TimeSpan.FromMinutes(15)));
        });
    }

    [Test]
    public void GetNetDuration_WithStampedOutCredit_ReducesAdditionalBreakDeduction()
    {
        var start = new DateTimeOffset(2026, 4, 3, 8, 0, 0, TimeSpan.Zero);
        var now = start.AddHours(7.5);
        var segments = new List<WorkSegment>
        {
            new() { Start = start, End = start.AddHours(1) },
            new() { Start = start.AddHours(1.5), End = now }
        };

        var rules = new List<BreakRule>
        {
            new() { MinWorkedHours = 0, DeductedBreakHours = 0.25 },
            new() { MinWorkedHours = 6, DeductedBreakHours = 0.75 }
        };

        var netWithCredit = _calculator.GetNetDuration(segments, rules, now, countStampedOutTimeAsBreak: true);
        var netWithoutCredit = _calculator.GetNetDuration(segments, rules, now, countStampedOutTimeAsBreak: false);

        Assert.Multiple(() =>
        {
            Assert.That(netWithoutCredit, Is.EqualTo(TimeSpan.FromHours(6.25)));
            Assert.That(netWithCredit, Is.EqualTo(TimeSpan.FromHours(6.75)));
        });
    }
}
