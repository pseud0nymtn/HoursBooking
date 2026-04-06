using Avalonia;
using Avalonia.Controls;
using HoursBooking.App.ViewModels;

namespace HoursBooking.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => ApplyResponsiveLayout();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
        DataContextChanged += (_, _) => ApplyResponsiveLayout();
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (Application.Current is App app && app.IsExitAllowed())
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel || !viewModel.MinimizeToTrayOnClose)
        {
            return;
        }

        if (Application.Current is App trayApp && trayApp.TryMinimizeToTray(this))
        {
            trayApp.ShowTrayMinimizeHintOnce(viewModel);
            e.Cancel = true;
        }
    }

    private void ApplyResponsiveLayout()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.UpdateLayoutMode(Bounds.Width);

            if (!viewModel.ShowSettingsTab && MainTabs.SelectedIndex > 0)
            {
                MainTabs.SelectedIndex = 0;
            }
        }
    }
}