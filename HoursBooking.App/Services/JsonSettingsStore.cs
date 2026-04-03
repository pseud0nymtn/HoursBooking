using System.Text.Json;
using HoursBooking.App.Models;

namespace HoursBooking.App.Services;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsFilePath;

    public JsonSettingsStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDirectory = Path.Combine(appData, "HoursBooking");
        _settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    }

    public async Task<AppSettingsDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new AppSettingsDocument();
        }

        await using var stream = File.OpenRead(_settingsFilePath);
        var loaded = await JsonSerializer.DeserializeAsync<AppSettingsDocument>(stream, JsonOptions, cancellationToken);
        return loaded ?? new AppSettingsDocument();
    }

    public async Task SaveAsync(AppSettingsDocument settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
    }
}
