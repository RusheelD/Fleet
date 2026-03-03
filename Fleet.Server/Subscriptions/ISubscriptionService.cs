using Fleet.Server.Models;

namespace Fleet.Server.Subscriptions;

public interface ISubscriptionService
{
    Task<SubscriptionDataDto> GetSubscriptionDataAsync();
}
