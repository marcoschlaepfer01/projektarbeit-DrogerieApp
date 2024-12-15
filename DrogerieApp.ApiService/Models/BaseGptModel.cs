using OpenAI.Chat;
using System.Text.Json;

namespace DrogerieApp.Backend.Models;

public abstract class BaseGptModel : BaseModel
{
    protected readonly ChatClient _client;
    protected readonly ChatResponseFormat _responseFormat;
    protected readonly SystemChatMessage _systemChatMessage;

    public BaseGptModel(ILogger logger, IConfiguration config, JsonSerializerOptions? jsonSerializerOptions = null)
        : base(logger, config, jsonSerializerOptions)
    {
        _client = new ChatClient(model: "gpt-4o", _config.GetValue<string>("OPENAI_API_KEY"));
        _responseFormat = InitChatResponseFormat();
        _systemChatMessage = InitSystemChatMessage();
    }

    protected virtual SystemChatMessage InitSystemChatMessage()
    {
        return new("""
            You are a smart swiss pharmacy Assistant, that analyzes customer conditions using the LINDAAFF model. You deduct the most likely condition 
            and the recommended medication. You provide both the "humanly-recognized" names, as well as UMLS codes for the conditions and medications. 
            Ultimately you also should explain your decisions and choices for an average person to understand. Make sure your output is in german.
            """);
    }

    protected virtual ChatResponseFormat InitChatResponseFormat()
    {
        return ChatResponseFormat.CreateJsonSchemaFormat(
        jsonSchemaFormatName: "lindaaff_response",
        jsonSchema: BinaryData.FromBytes("""
            {
                "type": "object",
                "properties": {
                    "condition": {
                        "type": "object",
                        "properties": {
                            "name": { "type": "string" },
                            "umlsCode": { "type": "string" },
                            "description": { "type": "string" }
                        },
                        "required": ["name", "umlsCode", "description"],
                        "additionalProperties": false
                    },
                    "medication": {
                        "type": "object",
                        "properties": {
                            "name": { "type": "string" },
                            "umlsCode": { "type": "string" },
                            "description": { "type": "string" }
                        },
                        "required": ["name", "umlsCode", "description"],
                        "additionalProperties": false
                    },
                    "explanation": { "type": "string" }
                },
                "required": [ "condition", "medication", "explanation" ],
                "additionalProperties": false
            }
            """u8.ToArray()),
        jsonSchemaIsStrict: true
    );
    }
}
