namespace PropertyPayPro.Services;

public interface IDocumentStorage
{
    Task<string> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
