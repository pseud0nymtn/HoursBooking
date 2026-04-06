using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using HoursBooking.App.Models;
using HoursBooking.App.Services;
using HoursBooking.App.ViewModels;
using HoursBooking.App.Views;
using HoursBooking.Core.Models;
using HoursBooking.Core.Services;

namespace HoursBooking.IntegrationTests;

public class MainWindowIntegrationTests
{
    [AvaloniaTest]
    public async Task MainWindow_ShouldContainBookingControls()
    {
        var localizationService = new LocalizationService();
        localizationService.SetLanguageCode("de");
        var viewModel = new MainWindowViewModel(new BookingCalculator(), new InMemorySettingsStore(), new NoOpThemeService(), localizationService, new NoOpFileSaveDialogService());
        await viewModel.InitializeAsync();

        var window = new MainWindow
        {
            DataContext = viewModel
        };

        window.Show();

        var clockIn = window.FindControl<Button>("ClockInButton");
        var clockOut = window.FindControl<Button>("ClockOutButton");
        var theme = window.FindControl<ComboBox>("ThemeComboBox");
        var checkBoxes = window.GetLogicalDescendants().OfType<CheckBox>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(clockIn, Is.Not.Null);
            Assert.That(clockOut, Is.Not.Null);
            Assert.That(theme, Is.Not.Null);
            Assert.That(checkBoxes.Count, Is.GreaterThan(0));
        });
    }

    [AvaloniaTest]
    public async Task ClockInAndClockOut_ShouldCreateSegment()
    {
        var localizationService = new LocalizationService();
        localizationService.SetLanguageCode("de");
        var viewModel = new MainWindowViewModel(new BookingCalculator(), new InMemorySettingsStore(), new NoOpThemeService(), localizationService, new NoOpFileSaveDialogService());
        await viewModel.InitializeAsync();

        viewModel.ClockInCommand.Execute(null);
        viewModel.ClockOutCommand.Execute(null);

        Assert.That(viewModel.Segments.Count, Is.EqualTo(1));
    }

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        private AppSettingsDocument _document = new();

        public Task<AppSettingsDocument> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_document);
        }

        public Task SaveAsync(AppSettingsDocument settings, CancellationToken cancellationToken = default)
        {
            _document = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpThemeService : IThemeService
    {
        public void ApplyTheme(ThemeMode themeMode)
        {
        }
    }

    private sealed class NoOpFileSaveDialogService : IFileSaveDialogService
    {
        public Task<string?> PickCsvSavePathAsync(string suggestedFileName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
