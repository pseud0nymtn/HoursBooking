using Avalonia;
using System;
using HoursBooking.App.Services;

namespace HoursBooking.App;

sealed class Program
{
    private const string SingleInstanceAppId = "HoursBooking.App";

    internal static AppSingleInstanceManager? SingleInstanceManager { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        SingleInstanceManager = AppSingleInstanceManager.TryCreatePrimary(SingleInstanceAppId);
        if (SingleInstanceManager is null)
        {
            AppSingleInstanceManager.TrySignalPrimaryInstance(SingleInstanceAppId);
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            SingleInstanceManager.Dispose();
            SingleInstanceManager = null;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
