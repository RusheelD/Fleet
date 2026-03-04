using Fleet.Server.Controllers;
using Fleet.Server.Models;
using Fleet.Server.Subscriptions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Fleet.Server.Tests.Controllers;

[TestClass]
public class SubscriptionControllerTests
{
    private Mock<ISubscriptionService> _subscriptionService = null!;
    private SubscriptionController _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _subscriptionService = new Mock<ISubscriptionService>();
        _sut = new SubscriptionController(_subscriptionService.Object);
    }

    [TestMethod]
    public async Task Get_ReturnsOk()
    {
        var data = new SubscriptionDataDto(new CurrentPlanDto("Pro", "Professional plan"), [], []);
        _subscriptionService.Setup(s => s.GetSubscriptionDataAsync()).ReturnsAsync(data);

        var result = await _sut.Get();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreSame(data, ok.Value);
    }
}
