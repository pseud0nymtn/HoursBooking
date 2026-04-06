using HoursBooking.App.Services;

namespace HoursBooking.Tests;

public class AppSingleInstanceManagerTests
{
    [Test]
    public void TryCreatePrimary_SecondInstanceReturnsNull()
    {
        var appId = $"HoursBooking.Test.{Guid.NewGuid():N}";

        using var primary = AppSingleInstanceManager.TryCreatePrimary(appId);
        var secondary = AppSingleInstanceManager.TryCreatePrimary(appId);

        Assert.Multiple(() =>
        {
            Assert.That(primary, Is.Not.Null);
            Assert.That(secondary, Is.Null);
        });
    }

    [Test]
    public void TrySignalPrimaryInstance_TriggersActivationCallback()
    {
        var appId = $"HoursBooking.Signal.{Guid.NewGuid():N}";
        using var primary = AppSingleInstanceManager.TryCreatePrimary(appId);
        Assert.That(primary, Is.Not.Null);

        using var activated = new ManualResetEventSlim(false);
        primary!.StartListening(() => activated.Set());

        // Give the listener a brief moment to start accepting requests.
        Thread.Sleep(100);

        var signaled = AppSingleInstanceManager.TrySignalPrimaryInstance(appId);
        var callbackFired = activated.Wait(TimeSpan.FromSeconds(3));

        Assert.Multiple(() =>
        {
            Assert.That(signaled, Is.True);
            Assert.That(callbackFired, Is.True);
        });
    }
}
