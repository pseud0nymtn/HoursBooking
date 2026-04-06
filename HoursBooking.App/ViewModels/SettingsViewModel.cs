namespace HoursBooking.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel(MainWindowViewModel owner)
    {
        Owner = owner;
    }

    public MainWindowViewModel Owner { get; }
}
