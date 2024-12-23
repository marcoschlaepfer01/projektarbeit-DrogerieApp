var builder = DistributedApplication.CreateBuilder(args);

var backendService = builder.AddProject<Projects.DrogerieApp_Backend>("backend")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.DrogerieApp_Web>("frontend")
    .WithExternalHttpEndpoints()
    .WithReference(backendService);

builder.Build().Run();
