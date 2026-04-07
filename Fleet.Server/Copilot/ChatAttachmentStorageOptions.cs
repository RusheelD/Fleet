namespace Fleet.Server.Copilot;

public class ChatAttachmentStorageOptions
{
    public string RootPath { get; set; } = GetDefaultRootPath();

    public static string GetDefaultRootPath()
        => Path.Combine(Path.GetTempPath(), "Fleet", "chat-attachments");

    public static string ResolveRootPath(IConfiguration configuration)
    {
        var configuredRoot = configuration["ChatAttachments:RootPath"];
        if (!string.IsNullOrWhiteSpace(configuredRoot))
            return configuredRoot;

        return GetDefaultRootPath();
    }
}
