var builder = DistributedApplication.CreateBuilder(args);

var azureOpenAiApiKey =
    builder.Configuration["Secrets:AzureOpenAiApiKey"] ??
    builder.Configuration["LLM:ApiKey"] ??
    Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

var azureOpenAiEndpoint =
    builder.Configuration["Secrets:AzureOpenAiEndpoint"] ??
    builder.Configuration["LLM:Endpoint"] ??
    Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");

var azureOpenAiModel =
    builder.Configuration["Secrets:AzureOpenAiModel"] ??
    builder.Configuration["LLM:Model"];

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

if (!string.IsNullOrWhiteSpace(azureOpenAiApiKey))
{
    server.WithEnvironment("AZURE_OPENAI_API_KEY", azureOpenAiApiKey);
    server.WithEnvironment("LLM__ApiKey", azureOpenAiApiKey);
}

if (!string.IsNullOrWhiteSpace(azureOpenAiEndpoint))
{
    server.WithEnvironment("AZURE_OPENAI_ENDPOINT", azureOpenAiEndpoint);
    server.WithEnvironment("LLM__Endpoint", azureOpenAiEndpoint);
}

if (!string.IsNullOrWhiteSpace(azureOpenAiModel))
{
    server.WithEnvironment("LLM__Model", azureOpenAiModel);
    server.WithEnvironment("LLM__GenerateModel", azureOpenAiModel);
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
