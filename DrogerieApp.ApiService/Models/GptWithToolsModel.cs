using DrogerieApp.Backend.Clients;
using DrogerieApp.ClassLibrary.ContentModels.Compendium;
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

    private static readonly ChatTool GetCompendiumListResponseTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetCompendiumMedicationListAsync),
        functionDescription: "Get a list of medications as json (name and url per item) from the Compendium Website, based on the medication name",
        functionSchemaIsStrict: true,
        functionParameters: BinaryData.FromBytes("""
            {
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "description": "A human readable medication name, such as 'paracetamol'"
                    }
                },
                "required": [ "name" ],
                "additionalProperties": false
            }
            """u8.ToArray())
    );

    private static readonly ChatTool GetCompendiumDetailedResponseTool = ChatTool.CreateFunctionTool(
        functionName: nameof(GetCompendiumDetailedMedicationAsync),
        functionDescription: "Get details about the medication based on a provided URL as json",
        functionSchemaIsStrict: true,
        functionParameters: BinaryData.FromBytes("""
            {
                "type": "object",
                "properties": {
                    "url": {
                        "type": "string",
                        "description": "An url (retrieved from the GetCompendiumMedicationListAsync tool)"
                    }
                },
                "required": [ "url" ],
                "additionalProperties": false
            }
            """u8.ToArray())
    );

    public GptWithToolsModel(ILogger<GptWithToolsModel> logger, IConfiguration config, UmlsClient umlsClient, HttpClient httpClient) : base(logger, config, httpClient: httpClient)
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
            Tools = { GetUmlsResponseTool, GetCompendiumListResponseTool, GetCompendiumDetailedResponseTool },
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
                                case nameof(GetCompendiumMedicationListAsync):
                                    {
                                        using JsonDocument argumentJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                        var hasName = argumentJson.RootElement.TryGetProperty("name", out JsonElement name);
                                        if (!hasName)
                                        {
                                            throw new ArgumentNullException(nameof(name), "The name argument is required");
                                        }
                                        string toolResult = await GetCompendiumMedicationListAsync(name.GetString()!);
                                        messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                        break;
                                    }
                                case nameof(GetCompendiumDetailedMedicationAsync):
                                    {
                                        using JsonDocument argumentJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                        var hasUrl = argumentJson.RootElement.TryGetProperty("url", out JsonElement url);
                                        if (!hasUrl)
                                        {
                                            throw new ArgumentNullException(nameof(url), "The url argument is required");
                                        }
                                        string toolResult = await GetCompendiumDetailedMedicationAsync(url.GetString()!);
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
            require prescriptions in switzerland. Make sure you resolve each medication suggestion the following way:
                Step 1: Use the "GetCompendiumMedicationListAsync" Tool to retrieve a list of medications in the compendium database.
                Step 2: If no elements are retrieved by the "GetCompendiumMedicationListAsync" Tool, reconsider your medication suggestion and start at Step 1.
                Step 3: Select the best fit of the items that you retrieved with "GetCompendiumMedicationListAsync" Tool and use the url for the next step.
                Step 4: Use the retrieved URL for the "GetCompendiumDetailedMedicationListAsync" Tool and get detailed information about the medication.
                Step 5: If no sensable content is retrieved by the "GetCompendiumDetailedMedicationListAsync" Tool, make sure a valid url is selected (restart from Step 3).
                Step 6: Make sure the "NarcoticCode" of the detailed medicament Information is either "D" or "E", meaning no prescription is necessary in switzerland. If this is not the case, reconsider your medication suggestion and restart from step 1.
                Step 7: You successfully selected a medication. Now give a detailed and concise explanation for your decisions for an average person to understand.
            Make sure your final answer (finish reason: STOP) is in german. Use the exact results of the final tool calls to fill the response format provided. Use the "GetUmlsResponseAsync" Tool for all UMLS codes in your response.
            """);
    }

    private async Task<string> GetUmlsResponseAsync(string name)
    {
        var result = await _umlsClient.SearchAsync(name);
        return result;
    }

    private async Task<string> GetCompendiumMedicationListAsync(string name)
    {
        var result = string.Empty;
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Compendium/GetMedicamentList", name);
            response.EnsureSuccessStatusCode();
            result = await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error while submitting name.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while submitting name.");
        }
        return result;
    }

    private async Task<string> GetCompendiumDetailedMedicationAsync(string url)
    {
        var result = string.Empty;
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Compendium/GetMedicamentDetails", url);
            response.EnsureSuccessStatusCode();
            result = await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error while submitting name.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while submitting name.");
        }
        return result;
    }
}