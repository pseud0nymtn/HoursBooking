namespace HoursBooking.App.Services;

public interface IFileSaveDialogService
{
    Task<string?> PickCsvSavePathAsync(string suggestedFileName, CancellationToken cancellationToken = default);
}
