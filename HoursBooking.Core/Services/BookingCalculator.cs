using HoursBooking.Core.Models;

namespace HoursBooking.Core.Services;

public sealed class BookingCalculator
{
    public TimeSpan GetGrossDuration(IEnumerable<WorkSegment> segments, DateTimeOffset now)
    {
        return segments
            .Select(segment => (segment.End ?? now) - segment.Start)
            .Where(duration => duration > TimeSpan.Zero)
            .Aggregate(TimeSpan.Zero, (acc, value) => acc + value);
    }

    public TimeSpan GetDeductedBreak(TimeSpan grossDuration, IEnumerable<BreakRule> breakRules)
    {
        var grossHours = grossDuration.TotalHours;
        var effectiveRule = breakRules
            .OrderBy(rule => rule.MinWorkedHours)
            .LastOrDefault(rule => rule.MinWorkedHours <= grossHours);

        if (effectiveRule is null)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromHours(Math.Max(0, effectiveRule.DeductedBreakHours));
    }

    public TimeSpan GetStampedOutDuration(IEnumerable<WorkSegment> segments)
    {
        var orderedSegments = segments
            .Where(segment => segment.End.HasValue)
            .OrderBy(segment => segment.Start)
            .ToList();

        if (orderedSegments.Count < 2)
        {
            return TimeSpan.Zero;
        }

        var total = TimeSpan.Zero;
        for (var index = 1; index < orderedSegments.Count; index++)
        {
            var previousEnd = orderedSegments[index - 1].End!.Value;
            var nextStart = orderedSegments[index].Start;
            if (nextStart > previousEnd)
            {
                total += nextStart - previousEnd;
            }
        }

        return total;
    }

    public TimeSpan GetEffectiveBreakDeduction(
        TimeSpan grossDuration,
        IEnumerable<BreakRule> breakRules,
        IEnumerable<WorkSegment> segments,
        bool countStampedOutTimeAsBreak)
    {
        var requiredBreak = GetDeductedBreak(grossDuration, breakRules);
        if (!countStampedOutTimeAsBreak)
        {
            return requiredBreak;
        }

        var stampedOut = GetStampedOutDuration(segments);
        var remainingBreak = requiredBreak - stampedOut;
        return remainingBreak < TimeSpan.Zero ? TimeSpan.Zero : remainingBreak;
    }

    public TimeSpan GetNetDuration(
        IEnumerable<WorkSegment> segments,
        IEnumerable<BreakRule> breakRules,
        DateTimeOffset now,
        bool countStampedOutTimeAsBreak = false)
    {
        var gross = GetGrossDuration(segments, now);
        var breakDeduction = GetEffectiveBreakDeduction(gross, breakRules, segments, countStampedOutTimeAsBreak);
        var net = gross - breakDeduction;
        return net < TimeSpan.Zero ? TimeSpan.Zero : net;
    }

    public AlertState EvaluateAlert(TimeSpan netDuration, BookingSettings settings)
    {
        var max = TimeSpan.FromHours(Math.Max(0.1, settings.MaxWorkHours));

        if (netDuration >= max)
        {
            var exceeded = netDuration - max;
            if (exceeded > TimeSpan.Zero)
            {
                return new AlertState(
                    AlertLevel.Error,
                    "Alert.MaxExceeded",
                    exceeded.TotalMinutes);
            }

            return new AlertState(AlertLevel.Error, "Alert.MaxReached");
        }

        var remaining = max - netDuration;
        var info = TimeSpan.FromMinutes(Math.Max(0, settings.InfoThresholdMinutes));
        var warning = TimeSpan.FromMinutes(Math.Max(0, settings.WarningThresholdMinutes));
        var error = TimeSpan.FromMinutes(Math.Max(0, settings.ErrorThresholdMinutes));

        if (remaining <= error)
        {
            return new AlertState(AlertLevel.Error, "Alert.ErrorRemaining", remaining.TotalMinutes);
        }

        if (remaining <= warning)
        {
            return new AlertState(AlertLevel.Warning, "Alert.WarningRemaining", remaining.TotalMinutes);
        }

        if (remaining <= info)
        {
            return new AlertState(AlertLevel.Info, "Alert.InfoRemaining", remaining.TotalMinutes);
        }

        return new AlertState(AlertLevel.None, "Alert.NoWarning");
    }

    public DateTimeOffset GetTargetReachedAt(
        IEnumerable<WorkSegment> segments,
        IEnumerable<BreakRule> breakRules,
        double desiredNetHours,
        DateTimeOffset now,
        bool countStampedOutTimeAsBreak = false)
    {
        var targetNet = TimeSpan.FromHours(Math.Max(0, desiredNetHours));
        var currentGross = GetGrossDuration(segments, now);
        var currentNet = currentGross - GetEffectiveBreakDeduction(currentGross, breakRules, segments, countStampedOutTimeAsBreak);
        if (currentNet < TimeSpan.Zero)
        {
            currentNet = TimeSpan.Zero;
        }

        if (currentNet >= targetNet)
        {
            return now;
        }

        // Step through one minute increments because break deduction can jump at configured thresholds.
        for (var minutes = 1; minutes <= 24 * 60; minutes++)
        {
            var projectedGross = currentGross + TimeSpan.FromMinutes(minutes);
            var projectedNet = projectedGross - GetEffectiveBreakDeduction(projectedGross, breakRules, segments, countStampedOutTimeAsBreak);
            if (projectedNet < TimeSpan.Zero)
            {
                projectedNet = TimeSpan.Zero;
            }

            if (projectedNet >= targetNet)
            {
                return now.AddMinutes(minutes);
            }
        }

        return now.AddHours(24);
    }
}
