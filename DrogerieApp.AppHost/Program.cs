var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.DrogerieApp_ApiService>("apiservice");

builder.AddProject<Projects.DrogerieApp_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
