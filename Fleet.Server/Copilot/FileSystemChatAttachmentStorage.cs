using System.Text;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace Fleet.Server.Copilot;

public class FileSystemChatAttachmentStorage(
    IOptions<ChatAttachmentStorageOptions> options,
    ILogger<FileSystemChatAttachmentStorage> logger) : IChatAttachmentStorage
{
    private const int MaxExtractedTextLength = 64_000;
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".markdown",
        ".txt",
        ".json",
        ".yaml",
        ".yml",
        ".xml",
        ".html",
        ".css",
        ".scss",
        ".sass",
        ".less",
        ".js",
        ".jsx",
        ".mjs",
        ".cjs",
        ".ts",
        ".tsx",
        ".cs",
        ".csproj",
        ".sln",
        ".sql",
        ".py",
        ".java",
        ".kt",
        ".kts",
        ".swift",
        ".go",
        ".rs",
        ".rb",
        ".php",
        ".sh",
        ".ps1",
        ".bat",
        ".cmd",
        ".ini",
        ".cfg",
        ".conf",
        ".toml",
        ".env",
        ".gitignore",
        ".editorconfig",
        ".dockerfile",
    };

    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private readonly string _rootPath = EnsureRoot(options.Value.RootPath);

    public async Task<StoredChatAttachment> SaveAsync(
        string attachmentId,
        string fileName,
        string? contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentId);
        var safeFileName = SanitizeFileName(fileName);
        var resolvedContentType = ResolveContentType(safeFileName, contentType);
        var relativeDirectory = Path.Combine(attachmentId[..Math.Min(2, attachmentId.Length)], attachmentId);
        var directoryPath = ResolveStorageDirectory(relativeDirectory);
        Directory.CreateDirectory(directoryPath);

        var relativeStoragePath = Path.Combine(relativeDirectory, safeFileName);
        var fullStoragePath = ResolveStoragePath(relativeStoragePath);
        await File.WriteAllBytesAsync(fullStoragePath, content, cancellationToken);

        var extractedText = ExtractTextContent(content, safeFileName, resolvedContentType);
        logger.LogInformation(
            "Stored chat attachment {AttachmentId} at {StoragePath} ({ContentType}, {ContentLength} bytes)",
            attachmentId,
            relativeStoragePath,
            resolvedContentType,
            content.Length);

        return new StoredChatAttachment(relativeStoragePath, content.Length, resolvedContentType, extractedText);
    }

    public async Task<byte[]?> ReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            return null;

        var fullStoragePath = ResolveStoragePath(storagePath);
        if (!File.Exists(fullStoragePath))
            return null;

        return await File.ReadAllBytesAsync(fullStoragePath, cancellationToken);
    }

    public Task DeleteAsync(string? storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            return Task.CompletedTask;

        try
        {
            var fullStoragePath = ResolveStoragePath(storagePath);
            if (File.Exists(fullStoragePath))
                File.Delete(fullStoragePath);

            var directory = Path.GetDirectoryName(fullStoragePath);
            while (!string.IsNullOrWhiteSpace(directory) &&
                   directory.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(directory, _rootPath, StringComparison.OrdinalIgnoreCase) &&
                   Directory.Exists(directory) &&
                   !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory, recursive: false);
                directory = Path.GetDirectoryName(directory);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete chat attachment storage path {StoragePath}", storagePath);
        }

        return Task.CompletedTask;
    }

    private static string EnsureRoot(string rootPath)
    {
        var resolvedRoot = string.IsNullOrWhiteSpace(rootPath)
            ? ChatAttachmentStorageOptions.GetDefaultRootPath()
            : rootPath;
        Directory.CreateDirectory(resolvedRoot);
        return Path.GetFullPath(resolvedRoot);
    }

    private string ResolveStorageDirectory(string relativeDirectory)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativeDirectory));
        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved chat attachment directory escaped the configured root path.");

        return fullPath;
    }

    private string ResolveStoragePath(string relativeStoragePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativeStoragePath));
        if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved chat attachment storage path escaped the configured root path.");

        return fullPath;
    }

    private static string ResolveContentType(string fileName, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            !string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return contentType.Trim();
        }

        return ContentTypeProvider.TryGetContentType(fileName, out var inferredContentType)
            ? inferredContentType
            : "application/octet-stream";
    }

    private static string SanitizeFileName(string fileName)
    {
        var leafName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(leafName))
            leafName = "attachment.bin";

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(leafName.Select(ch => invalidCharacters.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "attachment.bin" : sanitized;
    }

    private static string ExtractTextContent(byte[] content, string fileName, string contentType)
    {
        if (content.Length == 0 || !ShouldExtractText(fileName, contentType))
            return string.Empty;

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = reader.ReadToEnd();
            if (text.Length <= MaxExtractedTextLength)
                return text;

            return text[..MaxExtractedTextLength];
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool ShouldExtractText(string fileName, string contentType)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return false;

        if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return true;

        return TextExtensions.Contains(Path.GetExtension(fileName));
    }
}
