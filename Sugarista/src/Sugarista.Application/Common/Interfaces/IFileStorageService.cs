namespace Sugarista.Application.Common.Interfaces;

public record StoredFileUpload(Stream Content, string FileName, string ContentType, long Length);

public interface IFileStorageService
{
    Task<string> SaveInventoryDocumentAsync(StoredFileUpload file, CancellationToken cancellationToken);

    Task<string> SaveAssetPhotoAsync(StoredFileUpload file, CancellationToken cancellationToken = default);

    /// <summary>Removes a previously stored asset photo from disk (safe no-op for foreign URLs).</summary>
    Task DeleteAssetPhotoAsync(string imageUrl, CancellationToken cancellationToken = default);
}
