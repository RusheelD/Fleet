using Fleet.Server.Models;

namespace Fleet.Server.Copilot;

public class ChatService(
    IChatSessionRepository chatSessionRepository,
    ILogger<ChatService> logger) : IChatService
{
    public async Task<ChatDataDto> GetChatDataAsync(string projectId)
    {
        logger.LogInformation("Retrieving chat data for project {ProjectId}", projectId);
        var sessions = await chatSessionRepository.GetSessionsByProjectIdAsync(projectId);
        var activeSession = sessions.FirstOrDefault(s => s.IsActive);
        var messages = activeSession is not null
            ? await chatSessionRepository.GetMessagesBySessionIdAsync(projectId, activeSession.Id)
            : [];
        var suggestions = await chatSessionRepository.GetSuggestionsAsync(projectId);

        logger.LogInformation("Retrieved {SessionCount} sessions for project {ProjectId}", sessions.Count, projectId);
        return new ChatDataDto([.. sessions], [.. messages], suggestions);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(string projectId, string sessionId)
    {
        logger.LogInformation("Retrieving messages for session {SessionId} in project {ProjectId}", sessionId, projectId);
        return await chatSessionRepository.GetMessagesBySessionIdAsync(projectId, sessionId);
    }

    public async Task<ChatSessionDto> CreateSessionAsync(string projectId, string title)
    {
        logger.LogInformation("Creating chat session in project {ProjectId} with title: {Title}", projectId, title);
        return await chatSessionRepository.CreateSessionAsync(projectId, title);
    }

    public async Task<ChatMessageDto> SendMessageAsync(string projectId, string sessionId, string content)
    {
        logger.LogInformation("Sending message in session {SessionId} for project {ProjectId}", sessionId, projectId);

        // Save the user's message
        var userMessage = await chatSessionRepository.AddMessageAsync(projectId, sessionId, "user", content);

        // Generate a simple AI response (no real AI in this version)
        var aiResponse = $"I received your message about: \"{content}\". This is a placeholder response — real AI integration is coming soon.";
        await chatSessionRepository.AddMessageAsync(projectId, sessionId, "assistant", aiResponse);

        logger.LogInformation("Message sent and AI response generated for session {SessionId}", sessionId);
        return userMessage;
    }
}
