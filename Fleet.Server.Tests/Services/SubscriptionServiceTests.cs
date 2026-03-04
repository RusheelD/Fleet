using Fleet.Server.Models;
using Fleet.Server.Subscriptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class SubscriptionServiceTests
{
    private Mock<ISubscriptionRepository> _repo = null!;
    private Mock<ILogger<SubscriptionService>> _logger = null!;
    private SubscriptionService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _repo = new Mock<ISubscriptionRepository>();
        _logger = new Mock<ILogger<SubscriptionService>>();
        _sut = new SubscriptionService(_repo.Object, _logger.Object);
    }

    [TestMethod]
    public async Task GetSubscriptionDataAsync_DelegatesToRepo()
    {
        var expected = new SubscriptionDataDto(
            new CurrentPlanDto("Free", "Free tier"),
            [new UsageMeterDto("API Calls", "100/1000", 0.1, "green", "900 remaining")],
            [new PlanDto("Free", "rocket", "$0", "/month", "Free tier", ["Feature 1"], "Current", true, "subtle")]);

        _repo.Setup(r => r.GetSubscriptionDataAsync()).ReturnsAsync(expected);

        var result = await _sut.GetSubscriptionDataAsync();

        Assert.AreEqual("Free", result.CurrentPlan.Name);
        Assert.AreEqual(1, result.Usage.Length);
        Assert.AreEqual(1, result.Plans.Length);
    }
}
