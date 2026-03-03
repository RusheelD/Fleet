using Fleet.Server.Models;

namespace Fleet.Server.Subscriptions;

public class SubscriptionService(
    ISubscriptionRepository subscriptionRepository,
    ILogger<SubscriptionService> logger) : ISubscriptionService
{
    public async Task<SubscriptionDataDto> GetSubscriptionDataAsync()
    {
        logger.LogInformation("Retrieving subscription data");
        return await subscriptionRepository.GetSubscriptionDataAsync();
    }
}
