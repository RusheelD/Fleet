using Fleet.Server.Models;

namespace Fleet.Server.Copilot;

public interface IChatService
{
    Task<ChatDataDto> GetChatDataAsync(string projectId);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(string projectId, string sessionId);
    Task<ChatSessionDto> CreateSessionAsync(string projectId, string title);
    Task<ChatMessageDto> SendMessageAsync(string projectId, string sessionId, string content);
}
