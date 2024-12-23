using DrogerieApp.ClassLibrary.ContentModels.Lindaaff;
using System.Text.Json;

namespace DrogerieApp.Backend.Models;

public abstract class BaseModel
{
    protected readonly ILogger _logger;
    protected readonly IConfiguration _config;
    protected readonly JsonSerializerOptions _jsonSerializerOptions;
    protected readonly HttpClient _httpClient;

    public BaseModel(ILogger logger, IConfiguration config, HttpClient httpClient, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _logger = logger;
        _config = config;
        _jsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        _httpClient = httpClient;
    }

    public abstract Task<GptResponseContent> ProcessAsync(LindaaffContent model);
}