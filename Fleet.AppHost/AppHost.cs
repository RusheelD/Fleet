var builder = DistributedApplication.CreateBuilder(args);

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

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetPort = 5250;
        endpoint.IsProxied = false;
    })
    .WithReference(server)
    .WaitFor(server);

var website = builder.AddViteApp("website", "../website")
    .WithEndpoint("http", endpoint =>
    {
        endpoint.TargetPort = 5251;
        endpoint.IsProxied = false;
    })
    .WithExternalHttpEndpoints();

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
