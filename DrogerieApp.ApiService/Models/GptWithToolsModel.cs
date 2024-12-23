using DrogerieApp.Backend.Clients;
using DrogerieApp.ClassLibrary.ContentModels.Compendium;
using DrogerieApp.ClassLibrary.ContentModels.Lindaaff;
using OpenAI.Chat;
using System.Text.Json;

namespace DrogerieApp.Backend.Models
{
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
            functionDescription: "Get a list of medications as json (name and url per item) from the CompendiumController Website, based on the medication name",
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
                        "description": "A url (retrieved from the GetCompendiumMedicationListAsync tool)"
                    }
                },
                "required": [ "url" ],
                "additionalProperties": false
            }
            """u8.ToArray())
        );

        public GptWithToolsModel(
            ILogger<GptWithToolsModel> logger,
            IConfiguration config,
            UmlsClient umlsClient,
            HttpClient httpClient
        )
            : base(logger, config, httpClient: httpClient)
        {
            _umlsClient = umlsClient;
        }

        public override async Task<GptResponseContent> ProcessAsync(LindaaffContent lindaaffModel)
        {
            string lastUrl = string.Empty;

            var messages = new List<ChatMessage>
            {
                _systemChatMessage,
                new UserChatMessage(lindaaffModel.ToString())
            };

            var options = new ChatCompletionOptions
            {
                Tools = { GetUmlsResponseTool, GetCompendiumListResponseTool, GetCompendiumDetailedResponseTool },
                ResponseFormat = _responseFormat
            };

            bool requiresAction;
            do
            {
                requiresAction = false;
                var completion = await _client.CompleteChatAsync(messages, options);

                switch (completion.Value.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        {
                            var text = completion.Value.Content[0].Text;
                            _logger.LogInformation("The model generated the following final response {modelResponse}", text);
                            var result = JsonSerializer.Deserialize<GptResponseContent>(text, _jsonSerializerOptions) ?? new();
                            // Ensure we set the final URL to the one actually used
                            if (result.Medication?.MedicationDetails != null && !string.IsNullOrWhiteSpace(lastUrl))
                            {
                                result.Medication.MedicationDetails.Url = lastUrl;
                            }
                            return result;
                        }
                    case ChatFinishReason.ToolCalls:
                        {
                            // Model calls tools
                            messages.Add(new AssistantChatMessage(completion.Value));

                            foreach (var toolCall in completion.Value.ToolCalls)
                            {
                                _logger.LogInformation("The model calls the function {function}", toolCall.FunctionName);

                                switch (toolCall.FunctionName)
                                {
                                    case nameof(GetUmlsResponseAsync):
                                        {
                                            using var argumentJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                            if (!argumentJson.RootElement.TryGetProperty("name", out var name))
                                                throw new ArgumentNullException(nameof(name), "The name argument is required");

                                            var toolResult = await GetUmlsResponseAsync(name.GetString()!);
                                            messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                            break;
                                        }
                                    case nameof(GetCompendiumMedicationListAsync):
                                        {
                                            using var argumentJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                            if (!argumentJson.RootElement.TryGetProperty("name", out var name))
                                                throw new ArgumentNullException(nameof(name), "The name argument is required");

                                            var toolResult = await GetCompendiumMedicationListAsync(name.GetString()!);
                                            messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                            break;
                                        }
                                    case nameof(GetCompendiumDetailedMedicationAsync):
                                        {
                                            using var argumentJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                            if (!argumentJson.RootElement.TryGetProperty("url", out var url))
                                                throw new ArgumentNullException(nameof(url), "The url argument is required");

                                            var toolResult = await GetCompendiumDetailedMedicationAsync(url.GetString()!);
                                            messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                            lastUrl = url.GetString()!;
                                            break;
                                        }
                                    default:
                                        throw new NotImplementedException($"Unknown function '{toolCall.FunctionName}'");
                                }
                            }

                            requiresAction = true;
                            break;
                        }
                    case ChatFinishReason.Length:
                        throw new NotImplementedException(
                            "Incomplete model output due to MaxTokens parameter or token limit exceeded."
                        );
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
            // Updated text to match the actual function names
            return new("""
                You are a pharmacy assistant. ALWAYS FOLLOW THESE STEPS, NO EXCEPTIONS:

                1. If the user mentions or requests a medication or you need medication data, CALL the function "GetCompendiumMedicationListAsync" with the medication name. 
                2. If no items are found, pick a different medication or re-check the user’s condition, and call "GetCompendiumMedicationListAsync" again. 
                3. Once you have a valid medication URL from step 1, CALL the function "GetCompendiumDetailedMedicationAsync" with that URL to retrieve details. 
                4. If the NarcoticCode is not "D" or "E", pick a different medication. 
                5. Provide the final answer in a single JSON block in German, including the chosen medication details from step 3.

                If you answer with final text or JSON without calling the tools when medication or umls is needed, you are violating instructions.
                
                """
            );
        }

        private async Task<string> GetUmlsResponseAsync(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            try
            {
                var result = await _umlsClient.SearchAsync(name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while calling UMLS client.");
                return string.Empty;
            }
        }

        private async Task<string> GetCompendiumMedicationListAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            try
            {
                var response = await _httpClient.GetAsync($"api/Compendium/GetMedicamentList?name={Uri.EscapeDataString(name)}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request error while fetching medication list for name: {Name}", name);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching medication list for name: {Name}", name);
                return string.Empty;
            }
        }

        private async Task<string> GetCompendiumDetailedMedicationAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            try
            {
                var response = await _httpClient.GetAsync($"api/Compendium/GetMedicamentDetails?url={Uri.EscapeDataString(url)}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request error while fetching medication details for url: {Url}", url);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching medication details for url: {Url}", url);
                return string.Empty;
            }
        }
    }
}
