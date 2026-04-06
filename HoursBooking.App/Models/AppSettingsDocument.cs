using HoursBooking.Core.Models;

namespace HoursBooking.App.Models;

public sealed class AppSettingsDocument
{
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

    public string LanguageCode { get; set; } = string.Empty;

    public bool MinimizeToTrayOnClose { get; set; }

    public TrayDisplayMode TrayDisplayMode { get; set; } = TrayDisplayMode.Icon;

    public bool HasSeenTrayMinimizeHint { get; set; }

    public BookingSettings BookingSettings { get; set; } = new();
}
