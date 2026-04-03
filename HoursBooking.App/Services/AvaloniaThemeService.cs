using Avalonia;
using Avalonia.Styling;
using HoursBooking.App.Models;

namespace HoursBooking.App.Services;

public sealed class AvaloniaThemeService : IThemeService
{
    public void ApplyTheme(ThemeMode themeMode)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = themeMode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
