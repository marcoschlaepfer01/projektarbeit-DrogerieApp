using DrogerieApp.Web;
using DrogerieApp.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();
builder.Services.AddHttpClient<BackendClient>(client => client.BaseAddress = new("https+http://backend"));
var app = builder.Build();

app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseHsts();

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();
app.UseOutputCache();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();
app.Run();
