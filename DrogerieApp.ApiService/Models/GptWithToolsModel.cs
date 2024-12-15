using DrogerieApp.Backend.Clients;
using DrogerieApp.ClassLibrary.ContentModels.Lindaaff;
using OpenAI.Chat;
using System.Text.Json;

namespace DrogerieApp.Backend.Models;

public class GptWithToolsModel : BaseGptModel
{
    private readonly UmlsClient _umlsClient;

    private static readonly ChatTool GetUmlsResponseTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetUmlsResponseAsync),
        functionDescription: "Get the response as json from the UMLS API",
        functionSchemaIsStrict: true,
        functionParameters: BinaryData.FromBytes("""
            {
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "description": "A human readable term, such as ‘gestational diabetes’, or a code from a source vocabulary, such as 11687002 from SNOMEDCT_US."
                    }
                },
                "required": [ "name" ],
                "additionalProperties": false
            }
            """u8.ToArray())
    );

    public GptWithToolsModel(ILogger<GptWithToolsModel> logger, IConfiguration config, UmlsClient umlsClient) : base(logger, config)
    {
        _umlsClient = umlsClient;
    }

    public override async Task<GptResponseContent> ProcessAsync(LindaaffContent lindaaffModel)
    {
        List<ChatMessage> messages = [
            _systemChatMessage,
            new UserChatMessage(lindaaffModel.ToString())
        ];
        ChatCompletionOptions options = new()
        {
            Tools = { GetUmlsResponseTool },
            ResponseFormat = _responseFormat
        };
        bool requiresAction = false;
        do
        {
            var completion = await _client.CompleteChatAsync(messages, options);
            switch (completion.Value.FinishReason)
            {
                case ChatFinishReason.Stop:
                    {
                        string text = completion.Value.Content[0].Text;
                        _logger.LogInformation("The model generated the following final response {modelResponse}", text);
                        var result = JsonSerializer.Deserialize<GptResponseContent>(text, _jsonSerializerOptions);
                        return result;
                    }
                case ChatFinishReason.ToolCalls:
                    {
                        messages.Add(new AssistantChatMessage(completion.Value));
                        foreach (var toolCall in completion.Value.ToolCalls)
                        {
                            _logger.LogInformation("The model calls the function {function}", toolCall.FunctionName);
                            switch (toolCall.FunctionName)
                            {
                                case nameof(GetUmlsResponseAsync):
                                    {
                                        using JsonDocument argumentJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                        var hasName = argumentJson.RootElement.TryGetProperty("name", out JsonElement name);
                                        if (!hasName)
                                        {
                                            throw new ArgumentNullException(nameof(name), "The name argument is required");
                                        }
                                        string toolResult = await GetUmlsResponseAsync(name.GetString()!);
                                        messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                        break;
                                    }
                                default:
                                    {
                                        throw new NotImplementedException();
                                    }
                            }
                        }
                        requiresAction = true;
                        break;
                    }
                case ChatFinishReason.Length:
                    throw new NotImplementedException("Incomplete model output due to MaxTokens parameter or token limit exceeded.");

                case ChatFinishReason.ContentFilter:
                    throw new NotImplementedException("Omitted content due to a content filter flag.");

                case ChatFinishReason.FunctionCall:
                    throw new NotImplementedException("Deprecated in favor of tool calls.");

                default:
                    throw new NotImplementedException(completion.Value.FinishReason.ToString());
            }
        } while (requiresAction);
        return new();
    }

    protected override SystemChatMessage InitSystemChatMessage()
    {
        return new("""
            You are a smart swiss pharmacy Assistant, that analyzes customer conditions using the LINDAAFF model. You deduct the most likely condition 
            and the recommended medication. You provide both the "humanly-recognized" names, as well as UMLS codes for the conditions and medications.
            Make sure your answer for each schema element is only one thing (e.g. one condition and not multiple possibilities). Only suggest medicines that do not
            require prescriptions in switzerland.
            Ultimately you also should explain your decisions and choices for an average person to understand. Make sure you ALWAYS use the "GetUmlsResponseAsync" Tool
            if you need to provide an umls code. Give your non-tool results in german.
            """);
    }

    private async Task<string> GetUmlsResponseAsync(string name)
    {
        var result = await _umlsClient.SearchAsync(name);
        return result;
    }
}