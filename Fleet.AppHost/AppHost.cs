var builder = DistributedApplication.CreateBuilder(args);

var anthropicApiKey =
    builder.Configuration["Secrets:AnthropicApiKey"] ??
    builder.Configuration["LLM:ApiKey"] ??
    Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

var githubClientSecret =
    builder.Configuration["Secrets:GitHubClientSecret"] ??
    builder.Configuration["GitHub:ClientSecret"] ??
    Environment.GetEnvironmentVariable("GITHUB__CLIENTSECRET");

var cache = builder.AddRedis("cache");

var postgres = builder.AddPostgres("postgres");
var fleetDb = postgres.AddDatabase("fleetdb");

var server = builder.AddProject<Projects.Fleet_Server>("server")
    .WithReference(cache)
    .WithReference(fleetDb)
    .WaitFor(cache)
    .WaitFor(fleetDb)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

if (!string.IsNullOrWhiteSpace(anthropicApiKey))
{
    server.WithEnvironment("ANTHROPIC_API_KEY", anthropicApiKey);
}

if (!string.IsNullOrWhiteSpace(githubClientSecret))
{
    server.WithEnvironment("GITHUB__CLIENTSECRET", githubClientSecret);
}

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetPort = 5250;
    })
    .WithReference(server)
    .WaitFor(server);

var website = builder.AddViteApp("website", "../website")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetPort = 5251;
    })
    .WithExternalHttpEndpoints()
    .WaitFor(webfrontend);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
