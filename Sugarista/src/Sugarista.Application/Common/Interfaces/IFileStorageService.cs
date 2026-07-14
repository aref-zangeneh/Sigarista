namespace Sugarista.Application.Common.Interfaces;

public record StoredFileUpload(Stream Content, string FileName, string ContentType, long Length);

public interface IFileStorageService
{
    Task<string> SaveInventoryDocumentAsync(StoredFileUpload file, CancellationToken cancellationToken);
}
