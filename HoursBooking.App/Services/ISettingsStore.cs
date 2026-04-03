using HoursBooking.App.Models;

namespace HoursBooking.App.Services;

public interface ISettingsStore
{
    Task<AppSettingsDocument> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettingsDocument settings, CancellationToken cancellationToken = default);
}
