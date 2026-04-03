using Avalonia;
using Avalonia.Headless;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(HoursBooking.IntegrationTests.AvaloniaTestAppBuilder))]

namespace HoursBooking.IntegrationTests;

public static class AvaloniaTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<HoursBooking.App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .UseSkia();
    }
}
