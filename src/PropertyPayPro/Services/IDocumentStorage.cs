namespace PropertyPayPro.Services;

public interface IDocumentStorage
{
    Task<string> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct = default);

    /// <summary>
    /// Opens a stored file. Throws <see cref="StoredFileMissingException"/> when the
    /// database still points at a file that is no longer in storage.
    /// </summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);

    Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

/// <summary>
/// A document row exists but its file is gone from storage (deleted by hand, or
/// lost when the server or its storage volume moved).
/// </summary>
public class StoredFileMissingException : FileNotFoundException
{
    public StoredFileMissingException(string storageKey)
        : base($"The stored file '{storageKey}' is missing from document storage.", storageKey)
    {
        StorageKey = storageKey;
    }

    public string StorageKey { get; }
}
