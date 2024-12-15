using DrogerieApp.ClassLibrary.ContentModels.Lindaaff;
using OpenAI.Chat;
using System.Text.Json;

namespace DrogerieApp.Backend.Models;

public class SimpleGptModel : BaseGptModel
{
    public SimpleGptModel(ILogger<SimpleGptModel> logger, IConfiguration config) : base(logger, config) { }

    public async override Task<GptResponseContent> ProcessAsync(LindaaffContent lindaaffModel)
    {
        List<ChatMessage> chatMessages = [
            _systemChatMessage,
            new UserChatMessage(lindaaffModel.ToString())
        ];
        var options = new ChatCompletionOptions() { ResponseFormat = _responseFormat };
        _logger.LogDebug("Start model inference");
        var completion = await _client.CompleteChatAsync(chatMessages, options);
        string text = completion.Value.Content[0].Text;
        _logger.LogDebug("Completed model inference");
        try
        {
            var result = JsonSerializer.Deserialize<GptResponseContent>(text, _jsonSerializerOptions);
            return result ?? new();
        }
        catch (JsonException ex)
        {
            _logger.LogError("Error deserializing JSON: {Message}", ex.Message);
            throw;
        }
    }
}
