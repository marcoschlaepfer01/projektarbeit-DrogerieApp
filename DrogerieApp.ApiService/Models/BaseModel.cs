using DrogerieApp.ClassLibrary.ContentModels.Lindaaff;
using System.Text.Json;

namespace DrogerieApp.Backend.Models;

public abstract class BaseModel
{
    protected readonly ILogger _logger;
    protected readonly IConfiguration _config;
    protected readonly JsonSerializerOptions _jsonSerializerOptions;

    public BaseModel(ILogger logger, IConfiguration config, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _logger = logger;
        _config = config;
        _jsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public abstract Task<GptResponseContent> ProcessAsync(LindaaffContent model);
}