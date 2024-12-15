using System.Text.Json;
using System.Text;

namespace DrogerieApp.Backend.Clients;

public class UmlsClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<UmlsClient> _logger;
    private readonly string _apiKey;
    public UmlsClient(HttpClient httpClient, IConfiguration config, ILogger<UmlsClient> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _apiKey = config.GetValue<string>("UMLS_API_KEY") ?? "";
    }

    public async Task<string> SearchAsync(
            string queryString,
            string inputType = "atom",
            bool includeObsolete = false,
            bool includeSuppressible = false,
            string returnIdType = "concept",
            IEnumerable<string>? sabs = null,
            string searchType = "words",
            bool partialSearch = false,
            int pageNumber = 1,
            int pageSize = 10
        )
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            throw new ArgumentException("Query string cannot be null or empty.", nameof(queryString));
        }
        var queryParams = new Dictionary<string, string>
            {
                { "apiKey", _apiKey },
                { "string", queryString },
                { "inputType", inputType },
                { "includeObsolete", includeObsolete.ToString().ToLower() },
                { "includeSuppressible", includeSuppressible.ToString().ToLower() },
                { "returnIdType", returnIdType },
                { "searchType", searchType },
                { "pageNumber", pageNumber.ToString() },
                { "pageSize", pageSize.ToString() },
                { "sabs", sabs != null ? string.Join(",", sabs) : string.Empty },
                { "partialSearch", partialSearch.ToString().ToLower() }
            };
        var queryStringBuilder = new StringBuilder();
        foreach (var param in queryParams)
        {
            if (!string.IsNullOrEmpty(param.Value))
            {
                queryStringBuilder.Append($"{param.Key}={Uri.EscapeDataString(param.Value)}&");
            }
        }

        string requestUri = $"/search/current?{queryStringBuilder.ToString().TrimEnd('&')}";

        try
        {
            _logger.LogInformation("Sending UMLS search request to: {RequestUrl}", requestUri);
            var response = await _httpClient.GetFromJsonAsync<dynamic>(requestUri);
            if (response is null)
            {
                throw new HttpRequestException("The response is null.");
            }
            return JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error occurred while performing UMLS search.");
            throw;
        }
    }
}