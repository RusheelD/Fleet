using System.Net;
using System.Net.Http;
using Fleet.Server.LLM;

namespace Fleet.Server.Tests.LLM;

[TestClass]
public class IdleTimeoutHandlerTests
{
    [TestMethod]
    public void GetResponseHeadersTimeout_UsesThreeMinuteDefaultWithoutStretchingShortCustomTimeouts()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(180), IdleTimeoutHandler.GetResponseHeadersTimeout());
        Assert.AreEqual(TimeSpan.FromMilliseconds(60), IdleTimeoutHandler.GetResponseHeadersTimeout(TimeSpan.FromMilliseconds(60)));
        Assert.AreEqual(TimeSpan.FromSeconds(180), IdleTimeoutHandler.GetResponseHeadersTimeout(TimeSpan.FromSeconds(300)));
    }

    [TestMethod]
    public async Task SendWithIdleTimeoutAsync_WhenHeadersNeverArrive_FailsFastWithoutBufferedRetry()
    {
        var handler = new DelayedResponseHandler(TimeSpan.FromMilliseconds(250));
        using var httpClient = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/responses");

        try
        {
            _ = await IdleTimeoutHandler.SendWithIdleTimeoutAsync(
                httpClient,
                request,
                CancellationToken.None,
                TimeSpan.FromMilliseconds(60));
            Assert.Fail("Expected a timeout when response headers never arrive.");
        }
        catch (TimeoutException ex)
        {
            StringAssert.Contains(ex.Message, "headers were not received");
        }

        Assert.AreEqual(1, handler.CallCount);
    }

    private sealed class DelayedResponseHandler(TimeSpan delay) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            await Task.Delay(delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"output_text\":\"ok\"}"),
            };
        }
    }
}
