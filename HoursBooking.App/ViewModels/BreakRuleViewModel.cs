using CommunityToolkit.Mvvm.ComponentModel;

namespace HoursBooking.App.ViewModels;

public partial class BreakRuleViewModel : ObservableObject
{
    private readonly Action _onChanged;

    public BreakRuleViewModel(double minWorkedHours, double deductedBreakHours, Action onChanged)
    {
        _onChanged = onChanged;
        this.minWorkedHours = minWorkedHours;
        this.deductedBreakHours = deductedBreakHours;
    }

    [ObservableProperty]
    private double minWorkedHours;

    [ObservableProperty]
    private double deductedBreakHours;

    partial void OnMinWorkedHoursChanged(double value)
    {
        _onChanged();
    }

    partial void OnDeductedBreakHoursChanged(double value)
    {
        _onChanged();
    }
}
