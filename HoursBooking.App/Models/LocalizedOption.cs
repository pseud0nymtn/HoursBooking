using CommunityToolkit.Mvvm.ComponentModel;

namespace HoursBooking.App.Models;

public partial class LocalizedOption<T> : ObservableObject
{
    public required T Value { get; init; }

    [ObservableProperty]
    private string displayName = string.Empty;

    public override string ToString() => DisplayName;
}