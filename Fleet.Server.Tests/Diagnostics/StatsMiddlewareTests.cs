using System.Text.Json;
using Fleet.Server.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Fleet.Server.Tests.Diagnostics;

[TestClass]
public class StatsMiddlewareTests
{
    [TestMethod]
    public async Task InvokeAsync_ReturnsStatsSnapshot_ForStatsEndpoint()
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var stats = new ServiceStats(new TestHostEnvironment());
        var middleware = new StatsMiddleware(_ => Task.CompletedTask, stats);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/_stats";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var payload = await JsonSerializer.DeserializeAsync<ServiceStatsSnapshot>(context.Response.Body, serializerOptions);

        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.AreEqual("application/json;charset=utf-8", context.Response.ContentType);
        Assert.IsNotNull(payload);
        Assert.AreEqual("Fleet.Server", payload.ApplicationName);
        Assert.AreEqual(0, payload.RequestsStarted);
    }

    [TestMethod]
    public async Task InvokeAsync_TracksRequestStats_ForNonStatsRequests()
    {
        var stats = new ServiceStats(new TestHostEnvironment());
        var middleware = new StatsMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }, stats);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/projects";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        var snapshot = stats.CreateSnapshot();
        Assert.AreEqual(1, snapshot.RequestsStarted);
        Assert.AreEqual(1, snapshot.RequestsCompleted);
        Assert.AreEqual(0, snapshot.RequestsFailed);
        Assert.AreEqual(0, snapshot.RequestsInFlight);
        Assert.AreEqual(1L, snapshot.RequestMethods["POST"]);
        Assert.AreEqual(1L, snapshot.StatusCodes["204"]);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Fleet.Server";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
