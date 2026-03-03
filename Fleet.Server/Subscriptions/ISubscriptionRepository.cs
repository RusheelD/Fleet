using Fleet.Server.Models;

namespace Fleet.Server.Subscriptions;

public interface ISubscriptionRepository
{
    Task<SubscriptionDataDto> GetSubscriptionDataAsync();
}
