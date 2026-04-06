using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Media;
using System.ComponentModel;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using HoursBooking.App.Models;
using HoursBooking.App.Services;
using HoursBooking.App.ViewModels;
using HoursBooking.App.Views;
using HoursBooking.Core.Services;

namespace HoursBooking.App;

public partial class App : Application
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private LocalizationService? _localizer;
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _mainWindowViewModel;
    private NativeMenuItem? _trayNetWorkedTimeItem;
    private NativeMenuItem? _trayOpenItem;
    private NativeMenuItem? _trayExitItem;
    private TrayIcon? _trayIcon;
    private Window? _trayHintWindow;
    private WindowIcon? _lightTrayIcon;
    private WindowIcon? _darkTrayIcon;
    private bool _allowClose;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;

            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            var calculator = new BookingCalculator();
            _localizer = new LocalizationService();
            _localizer.LanguageChanged += OnLanguageChanged;
            var settingsStore = new JsonSettingsStore();
            var themeService = new AvaloniaThemeService();
            _mainWindowViewModel = new MainWindowViewModel(calculator, settingsStore, themeService, _localizer);
            _mainWindowViewModel.PropertyChanged += OnMainWindowViewModelPropertyChanged;

            _mainWindow = new MainWindow
            {
                DataContext = _mainWindowViewModel,
            };
            desktop.MainWindow = _mainWindow;

            _ = _mainWindowViewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    public bool TryMinimizeToTray(MainWindow window)
    {
        if (_desktop is null || _mainWindow is null || !ReferenceEquals(window, _mainWindow))
        {
            return false;
        }

        EnsureTrayIcon();
        if (_trayIcon is null)
        {
            return false;
        }

        UpdateTrayPresentation();
        _trayIcon.IsVisible = true;
        _mainWindow.ShowInTaskbar = false;
        _mainWindow.Hide();
        return true;
    }

    public void ShowTrayMinimizeHintOnce(MainWindowViewModel viewModel)
    {
        if (viewModel.HasSeenTrayMinimizeHint)
        {
            return;
        }

        viewModel.MarkTrayMinimizeHintAsSeen();

        _trayHintWindow?.Close();
        _trayHintWindow = CreateTrayHintWindow();
        _trayHintWindow.Show();

        DispatcherTimer.RunOnce(() =>
        {
            _trayHintWindow?.Close();
        }, TimeSpan.FromSeconds(4));
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        var trayMenu = new NativeMenu();

        _trayNetWorkedTimeItem = new NativeMenuItem(string.Empty)
        {
            IsEnabled = false
        };
        trayMenu.Items.Add(_trayNetWorkedTimeItem);

        trayMenu.Items.Add(new NativeMenuItemSeparator());

        _trayOpenItem = new NativeMenuItem(Localize("Tray.Open"));
        _trayOpenItem.Click += (_, _) => RestoreFromTray();
        trayMenu.Items.Add(_trayOpenItem);

        trayMenu.Items.Add(new NativeMenuItemSeparator());

        _trayExitItem = new NativeMenuItem(Localize("Tray.Exit"));
        _trayExitItem.Click += (_, _) => ExitFromTray();
        trayMenu.Items.Add(_trayExitItem);

        var lightIconUri = new Uri("avares://HoursBooking.App/Assets/hoursbooking-icon.ico");
        var darkIconUri = new Uri("avares://HoursBooking.App/Assets/hoursbooking-icon-dark.ico");
        _lightTrayIcon = new WindowIcon(AssetLoader.Open(lightIconUri));
        _darkTrayIcon = new WindowIcon(AssetLoader.Open(darkIconUri));
        _trayIcon = new TrayIcon
        {
            Icon = GetThemeAwareTrayIcon(),
            ToolTipText = Localize("Tray.TooltipDefault"),
            Menu = trayMenu,
            IsVisible = false
        };

        _trayIcon.Clicked += (_, _) => RestoreFromTray();
    }

    private void RestoreFromTray()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _trayHintWindow?.Close();
        _trayIcon?.SetCurrentValue(TrayIcon.IsVisibleProperty, false);
        _mainWindow.ShowInTaskbar = true;
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    private void ExitFromTray()
    {
        if (_desktop is null)
        {
            return;
        }

        _allowClose = true;
        _trayHintWindow?.Close();
        if (_mainWindow is not null)
        {
            _mainWindow.ShowInTaskbar = true;
            _mainWindow.Close();
        }

        _trayIcon?.SetCurrentValue(TrayIcon.IsVisibleProperty, false);
        _desktop.Shutdown();
    }

    public bool IsExitAllowed() => _allowClose;

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_trayOpenItem is not null)
        {
            _trayOpenItem.Header = Localize("Tray.Open");
        }

        if (_trayNetWorkedTimeItem is not null && _mainWindowViewModel is not null && _localizer is not null)
        {
            _trayNetWorkedTimeItem.Header = _localizer.Format("Tray.ContextNetWorkedTime", _mainWindowViewModel.NetWorkedText);
        }

        if (_trayExitItem is not null)
        {
            _trayExitItem.Header = Localize("Tray.Exit");
        }

        UpdateTrayPresentation();
    }

    private void OnMainWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_trayIcon is null || _mainWindowViewModel is null)
        {
            return;
        }

        if (e.PropertyName is nameof(MainWindowViewModel.NetWorkedText)
            or nameof(MainWindowViewModel.SelectedThemeMode)
            or nameof(MainWindowViewModel.SelectedLanguageOption))
        {
            UpdateTrayPresentation();
        }
    }

    private void UpdateTrayPresentation()
    {
        if (_trayIcon is null || _mainWindowViewModel is null || _localizer is null)
        {
            return;
        }

        _trayIcon.ToolTipText = _localizer["Tray.TooltipDefault"];
        _trayIcon.Icon = GetThemeAwareTrayIcon();

        if (_trayNetWorkedTimeItem is not null)
        {
            _trayNetWorkedTimeItem.Header = _localizer.Format("Tray.ContextNetWorkedTime", _mainWindowViewModel.NetWorkedText);
        }
    }

    private Window CreateTrayHintWindow()
    {
        var cardBrush = TryGetBrush("CardBackgroundBrush") ?? new SolidColorBrush(Color.Parse("#FFFFFF"));
        var borderBrush = TryGetBrush("HairlineBrush") ?? new SolidColorBrush(Color.Parse("#D6D6DC"));
        var foregroundBrush = TryGetBrush("WindowForegroundBrush") ?? new SolidColorBrush(Color.Parse("#1C1C1E"));
        var subtleBrush = TryGetBrush("SubtleTextBrush") ?? new SolidColorBrush(Color.Parse("#6E6E73"));

        var window = new Window
        {
            Width = 340,
            Height = 110,
            CanResize = false,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            SystemDecorations = SystemDecorations.None,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Brushes.Transparent,
            Content = new Border
            {
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(16, 14),
                Background = cardBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                BoxShadow = BoxShadows.Parse("0 12 30 0 #22000000"),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = Localize("Tray.HintTitle"),
                            FontSize = 16,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = foregroundBrush
                        },
                        new TextBlock
                        {
                            Text = Localize("Tray.HintBody"),
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = subtleBrush
                        }
                    }
                }
            }
        };

        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_trayHintWindow, window))
            {
                _trayHintWindow = null;
            }
        };

        return window;
    }

    private IBrush? TryGetBrush(string resourceKey)
    {
        if (!TryGetResource(resourceKey, ActualThemeVariant, out var resource))
        {
            return null;
        }

        return resource as IBrush;
    }

    private WindowIcon? GetThemeAwareTrayIcon()
    {
        var isDarkTheme = _mainWindowViewModel?.SelectedThemeMode == ThemeMode.Dark
            || (_mainWindowViewModel?.SelectedThemeMode == ThemeMode.System && _mainWindow?.ActualThemeVariant == ThemeVariant.Dark);

        if (isDarkTheme)
        {
            return _darkTrayIcon ?? _lightTrayIcon;
        }

        return _lightTrayIcon ?? _darkTrayIcon;
    }

    private string Localize(string key)
    {
        return _localizer?[key] ?? key;
    }
}