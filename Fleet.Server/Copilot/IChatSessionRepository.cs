using Fleet.Server.Models;

namespace Fleet.Server.Copilot;

public interface IChatSessionRepository
{
    Task<IReadOnlyList<ChatSessionDto>> GetSessionsByProjectIdAsync(string projectId);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesBySessionIdAsync(string projectId, string sessionId);
    Task<string[]> GetSuggestionsAsync(string projectId);
    Task<ChatSessionDto> CreateSessionAsync(string projectId, string title);
    Task<ChatMessageDto> AddMessageAsync(string projectId, string sessionId, string role, string content);
}
