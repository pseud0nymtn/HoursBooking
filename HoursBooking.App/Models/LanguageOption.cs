namespace HoursBooking.App.Models;

public sealed class LanguageOption
{
    public required string Code { get; init; }

    public string DisplayName { get; set; } = string.Empty;

    public override string ToString() => DisplayName;
}