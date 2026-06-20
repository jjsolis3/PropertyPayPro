namespace PropertyPayPro.Services;

public class LocalFileSystemDocumentStorage : IDocumentStorage
{
    private readonly string _root;

    public LocalFileSystemDocumentStorage(IConfiguration config)
    {
        _root = config["DocumentStorage:RootPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct = default)
    {
        var safeFolder = SanitizeSegment(folder);
        var safeName = $"{Guid.NewGuid():N}_{SanitizeSegment(fileName)}";
        var relativeKey = Path.Combine(safeFolder, safeName).Replace('\\', '/');
        var fullPath = Path.Combine(_root, relativeKey);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, ct);
        return relativeKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = ResolveAndValidate(storageKey);
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = ResolveAndValidate(storageKey);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string ResolveAndValidate(string storageKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, storageKey));
        var rootFull = Path.GetFullPath(_root);
        if (!fullPath.StartsWith(rootFull, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Storage key escapes root.");
        }
        return fullPath;
    }

    private static string SanitizeSegment(string segment)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).ToArray();
        var cleaned = new string(segment.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "unnamed" : cleaned;
    }
}
