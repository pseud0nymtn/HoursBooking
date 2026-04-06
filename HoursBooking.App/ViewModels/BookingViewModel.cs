namespace HoursBooking.App.ViewModels;

public sealed class BookingViewModel : ViewModelBase
{
    public BookingViewModel(MainWindowViewModel owner)
    {
        Owner = owner;
    }

    public MainWindowViewModel Owner { get; }
}
