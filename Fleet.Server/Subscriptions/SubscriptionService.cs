using Fleet.Server.Models;
using Fleet.Server.Logging;

namespace Fleet.Server.Subscriptions;

public class SubscriptionService(
    ISubscriptionRepository subscriptionRepository,
    ILogger<SubscriptionService> logger) : ISubscriptionService
{
    public async Task<SubscriptionDataDto> GetSubscriptionDataAsync()
    {
        logger.SubscriptionsRetrieving();
        return await subscriptionRepository.GetSubscriptionDataAsync();
    }
}
