using DrogerieApp.Backend.Clients;
using DrogerieApp.Backend.Models;
using Microsoft.OpenApi.Models;

var url = "https://localhost:17220";
var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<UmlsClient>();
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
    {
        Timeout = new(0, 2, 0)
    };
});
builder.Services.AddHttpClient<UmlsClient>(client => client.BaseAddress = new Uri(builder.Configuration["Urls:Umls"]!));
builder.Services.AddHttpClient<BaseModel, GptWithToolsModel>(client => client.BaseAddress = new(url));

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add the built-in OpenAPI support
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "Drogerie App Backend";
        document.Info.Version = "0.0.1";
        document.Info.Contact = new OpenApiContact
        {
            Name = "Marco Schl�pfer",
            Email = "marco.schlaepfer@gmail.com"
        };
        document.Servers = [ new OpenApiServer { Url = url } ];
        return Task.CompletedTask;
    });
});

var app = builder.Build();
app.MapOpenApi(); 
app.UseSwaggerUi(settings =>
{
    settings.DocumentPath = "/openapi/v1.json";
    settings.Path = "/openapi";
    settings.DocumentTitle = "Drogerie App Backend UI";
});
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();