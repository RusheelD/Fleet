using System.Text.Json;
using Fleet.Server.Subscriptions;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Returns current subscription/usage data for the user.</summary>
public class GetSubscriptionTool(ISubscriptionService subscriptionService) : IChatTool
{
    public string Name => "get_subscription";

    public string Description =>
        "Get the current user's subscription plan, usage meters, and available plans.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {},
            "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(context.UserId, out var userId))
            return "Error: invalid user ID.";

        var data = await subscriptionService.GetSubscriptionDataAsync(userId);
        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }
}
