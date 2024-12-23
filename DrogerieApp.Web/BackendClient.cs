using DrogerieApp.ClassLibrary.ContentModels.Lindaaff;
using System.Text.Json;

namespace DrogerieApp.Web;

public class BackendClient(HttpClient _httpClient, ILogger<BackendClient> _logger)
{
    public async Task<GptResponseContent> SubmitFormAsync(LindaaffContent content)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Lindaaff/ProcessLindaaffContent", content).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GptResponseContent>() ?? new();
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error while submitting form.");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error while reading response.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while submitting form.");
            throw;
        }
    }
}
