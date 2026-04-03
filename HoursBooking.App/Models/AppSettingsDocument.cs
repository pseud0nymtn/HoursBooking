using HoursBooking.Core.Models;

namespace HoursBooking.App.Models;

public sealed class AppSettingsDocument
{
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

    public BookingSettings BookingSettings { get; set; } = new();
}
