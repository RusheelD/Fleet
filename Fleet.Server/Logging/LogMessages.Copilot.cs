using Microsoft.Extensions.Logging;

namespace Fleet.Server.Logging;

public static partial class LogMessages
{
    [LoggerMessage(EventId = 99100, Level = LogLevel.Information, Message = "Retrieving chat data. projectId={projectId}")]
    public static partial void CopilotChatDataRetrieving(this ILogger logger, string projectId);

    [LoggerMessage(EventId = 99101, Level = LogLevel.Information, Message = "Retrieved chat sessions. projectId={projectId} sessionCount={sessionCount}")]
    public static partial void CopilotChatDataRetrieved(this ILogger logger, string projectId, int sessionCount);

    [LoggerMessage(EventId = 99102, Level = LogLevel.Information, Message = "Retrieving chat messages. projectId={projectId} sessionId={sessionId}")]
    public static partial void CopilotMessagesRetrieving(this ILogger logger, string projectId, string sessionId);

    [LoggerMessage(EventId = 99103, Level = LogLevel.Information, Message = "Creating chat session. projectId={projectId} title={title}")]
    public static partial void CopilotSessionCreating(this ILogger logger, string projectId, string title);

    [LoggerMessage(EventId = 99104, Level = LogLevel.Information, Message = "Deleting chat session. projectId={projectId} sessionId={sessionId}")]
    public static partial void CopilotSessionDeleting(this ILogger logger, string projectId, string sessionId);

    [LoggerMessage(EventId = 99119, Level = LogLevel.Information, Message = "Renaming chat session. projectId={projectId} sessionId={sessionId} title={title}")]
    public static partial void CopilotSessionRenaming(this ILogger logger, string projectId, string sessionId, string title);

    [LoggerMessage(EventId = 99105, Level = LogLevel.Information, Message = "Sending chat message. projectId={projectId} sessionId={sessionId} generateWorkItems={generateWorkItems}")]
    public static partial void CopilotMessageSending(this ILogger logger, string projectId, string sessionId, bool generateWorkItems);

    [LoggerMessage(EventId = 99106, Level = LogLevel.Warning, Message = "LLM request timed out. timeoutSeconds={timeoutSeconds}")]
    public static partial void CopilotLlmTimeout(this ILogger logger, int timeoutSeconds);

    [LoggerMessage(EventId = 99107, Level = LogLevel.Error, Message = "LLM request failed")]
    public static partial void CopilotLlmFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 99108, Level = LogLevel.Warning, Message = "Max total tool calls exceeded. maxTotalToolCalls={maxTotalToolCalls}")]
    public static partial void CopilotMaxToolCallsExceeded(this ILogger logger, int maxTotalToolCalls);

    [LoggerMessage(EventId = 99109, Level = LogLevel.Information, Message = "AI response generated. sessionId={sessionId} loops={loops} tools={tools}")]
    public static partial void CopilotAiResponseGenerated(this ILogger logger, string sessionId, int loops, int tools);

    [LoggerMessage(EventId = 99110, Level = LogLevel.Warning, Message = "Tool loop exhausted. sessionId={sessionId} maxToolLoops={maxToolLoops}")]
    public static partial void CopilotToolLoopExhausted(this ILogger logger, string sessionId, int maxToolLoops);

    [LoggerMessage(EventId = 99111, Level = LogLevel.Warning, Message = "Unknown tool requested. tool={tool}")]
    public static partial void CopilotUnknownTool(this ILogger logger, string tool);

    [LoggerMessage(EventId = 99112, Level = LogLevel.Information, Message = "Executing tool. tool={tool} args={args}")]
    public static partial void CopilotExecutingTool(this ILogger logger, string tool, string args);

    [LoggerMessage(EventId = 99113, Level = LogLevel.Error, Message = "Tool execution failed. tool={tool}")]
    public static partial void CopilotToolExecutionFailed(this ILogger logger, Exception exception, string tool);

    [LoggerMessage(EventId = 99116, Level = LogLevel.Information, Message = "LLM responded. loop={loop} hasToolCalls={hasToolCalls} toolCallCount={toolCallCount} textLength={textLength}")]
    public static partial void CopilotLlmResponseReceived(this ILogger logger, int loop, bool hasToolCalls, int toolCallCount, int textLength);

    [LoggerMessage(EventId = 99117, Level = LogLevel.Information, Message = "Executing tool batch. count={count} totalSoFar={totalSoFar}")]
    public static partial void CopilotToolBatchStarting(this ILogger logger, int count, int totalSoFar);

    [LoggerMessage(EventId = 99118, Level = LogLevel.Information, Message = "Tool completed. tool={tool} resultLength={resultLength} elapsedMs={elapsedMs}")]
    public static partial void CopilotToolExecutionCompleted(this ILogger logger, string tool, int resultLength, long elapsedMs);

    [LoggerMessage(EventId = 99114, Level = LogLevel.Information, Message = "Uploading attachment. sessionId={sessionId} fileName={fileName} length={length}")]
    public static partial void CopilotAttachmentUploading(this ILogger logger, string sessionId, string fileName, int length);

    [LoggerMessage(EventId = 99115, Level = LogLevel.Information, Message = "Deleting attachment. attachmentId={attachmentId}")]
    public static partial void CopilotAttachmentDeleting(this ILogger logger, string attachmentId);
}
