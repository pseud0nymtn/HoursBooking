using System.Globalization;
using System.Text.Json;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using HoursBooking.App.Models;

namespace HoursBooking.App.Services;

public sealed partial class LocalizationService : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, Dictionary<string, string>> _translations;
    private readonly Dictionary<string, LanguageOption> _languages;

    public LocalizationService()
    {
        _languages = LoadLanguages();
        _translations = LoadTranslations(_languages.Keys);
        SetLanguageCode(string.Empty);
    }

    [ObservableProperty]
    private string selectedLanguageCode = string.Empty;

    [ObservableProperty]
    private string currentLanguageCode = "en";

    public event EventHandler? LanguageChanged;

    public IReadOnlyList<LanguageOption> AvailableLanguages => _languages.Values.ToList();

    public CultureInfo CurrentCulture => new(CurrentLanguageCode);

    public string this[string key] => GetString(key);

    public void SetLanguageCode(string? languageCode)
    {
        SelectedLanguageCode = languageCode ?? string.Empty;

        var resolvedCode = ResolveLanguageCode(SelectedLanguageCode);
        if (CurrentLanguageCode == resolvedCode)
        {
            OnPropertyChanged("Item[]");
            return;
        }

        CurrentLanguageCode = resolvedCode;
        OnPropertyChanged("Item[]");
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string key)
    {
        if (_translations.TryGetValue(CurrentLanguageCode, out var activeTranslations)
            && activeTranslations.TryGetValue(key, out var translated))
        {
            return translated;
        }

        if (_translations.TryGetValue("en", out var fallbackTranslations)
            && fallbackTranslations.TryGetValue(key, out var fallback))
        {
            return fallback;
        }

        return key;
    }

    public string Format(string key, params object[] arguments)
    {
        return string.Format(CurrentCulture, GetString(key), arguments);
    }

    private string ResolveLanguageCode(string selectedCode)
    {
        if (!string.IsNullOrWhiteSpace(selectedCode) && _languages.ContainsKey(selectedCode))
        {
            return selectedCode;
        }

        var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (_languages.ContainsKey(systemLanguage))
        {
            return systemLanguage;
        }

        return _languages.ContainsKey("en") ? "en" : _languages.Keys.First();
    }

    private static Dictionary<string, LanguageOption> LoadLanguages()
    {
        var uri = new Uri("avares://HoursBooking.App/Assets/Localization/languages.json");
        using var stream = AssetLoader.Open(uri);
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        var items = JsonSerializer.Deserialize<List<LanguageOption>>(json, JsonOptions) ?? [];
        return items.ToDictionary(item => item.Code, item => item, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, Dictionary<string, string>> LoadTranslations(IEnumerable<string> languageCodes)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var languageCode in languageCodes)
        {
            var uri = new Uri($"avares://HoursBooking.App/Assets/Localization/{languageCode}.json");
            using var stream = AssetLoader.Open(uri);
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? [];
            result[languageCode] = map;
        }

        return result;
    }
}