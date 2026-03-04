namespace Fleet.Server.Models;

public record SendMessageRequest(string Content, bool GenerateWorkItems = false);
