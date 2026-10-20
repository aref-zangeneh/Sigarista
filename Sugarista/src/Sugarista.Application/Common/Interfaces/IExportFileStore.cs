namespace Sugarista.Application.Common.Interfaces;

/// <summary>
/// Raw file store for generated export files (server local storage).
/// </summary>
public interface IExportFileStore
{
    /// <summary>Writes the file and returns the local path it was saved under.</summary>
    Task<string> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>Opens the saved file for streaming download.</summary>
    Stream OpenRead(string storagePath);

    /// <summary>Deletes a stored file (no-op if it no longer exists).</summary>
    void Delete(string storagePath);
}
