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