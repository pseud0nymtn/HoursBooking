namespace HoursBooking.Core.Models;

public sealed record AlertState(AlertLevel Level, string MessageKey, params object[] FormatArguments);
