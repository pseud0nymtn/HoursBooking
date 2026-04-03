using CommunityToolkit.Mvvm.ComponentModel;
using HoursBooking.Core.Models;

namespace HoursBooking.App.ViewModels;

public partial class WorkSegmentItemViewModel : ObservableObject
{
    public WorkSegment? Segment { get; init; }

    [ObservableProperty]
    private string start = string.Empty;

    [ObservableProperty]
    private string end = string.Empty;

    [ObservableProperty]
    private string duration = string.Empty;
}
