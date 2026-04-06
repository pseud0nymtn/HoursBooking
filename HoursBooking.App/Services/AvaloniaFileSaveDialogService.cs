using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace HoursBooking.App.Services;

public sealed class AvaloniaFileSaveDialogService(Func<Window?> ownerProvider) : IFileSaveDialogService
{
    public async Task<string?> PickCsvSavePathAsync(string suggestedFileName, CancellationToken cancellationToken = default)
    {
        var owner = ownerProvider();
        if (owner?.StorageProvider is null)
        {
            return null;
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "csv",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType("CSV files")
                {
                    Patterns = ["*.csv"],
                    MimeTypes = ["text/csv"]
                }
            ]
        });

        return file?.TryGetLocalPath();
    }
}
