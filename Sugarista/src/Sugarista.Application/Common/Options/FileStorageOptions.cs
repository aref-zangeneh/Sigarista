namespace Sugarista.Application.Common.Options;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string UploadRootPath { get; set; } = "uploads";

    public int MaxInventoryDocumentBytes { get; set; } = 5 * 1024 * 1024;
}
