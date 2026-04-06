using CommunityToolkit.Mvvm.ComponentModel;
using HoursBooking.Core.Models;

namespace HoursBooking.App.ViewModels;

public partial class WorkSegmentItemViewModel : ObservableObject
{
    public const int MaxCommentLength = 280;

    public WorkSegment? Segment { get; init; }

    [ObservableProperty]
    private string start = string.Empty;

    [ObservableProperty]
    private string end = string.Empty;

    [ObservableProperty]
    private string duration = string.Empty;

    [ObservableProperty]
    private string comment = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotEditing))]
    private bool isEditing;

    [ObservableProperty]
    private TimeSpan editableStartTime;

    [ObservableProperty]
    private TimeSpan? editableEndTime;

    [ObservableProperty]
    private string editableComment = string.Empty;

    public bool IsNotEditing => !IsEditing;
}
