using DrogerieApp.Backend.Clients;
using DrogerieApp.Backend.Models;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<UmlsClient>();
builder.Services.AddSingleton<BaseModel, GptWithToolsModel>();
builder.Services.AddHttpClient<UmlsClient>(client => client.BaseAddress = new Uri(builder.Configuration["Urls:Umls"]!));
builder.Services.AddHttpClient<GptWithToolsModel>(client => client.BaseAddress = new("https+http://backend"));

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
            Name = "Marco Schläpfer",
            Email = "marco.schlaepfer@gmail.com"
        };
        document.Servers = [ new OpenApiServer { Url = "https://localhost:17220" } ];
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