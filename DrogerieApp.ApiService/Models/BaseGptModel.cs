using OpenAI.Chat;
using System.Text.Json;

namespace DrogerieApp.Backend.Models;

public abstract class BaseGptModel : BaseModel
{
    protected readonly ChatClient _client;
    protected readonly ChatResponseFormat _responseFormat;
    protected readonly SystemChatMessage _systemChatMessage;

    public BaseGptModel(ILogger logger, IConfiguration config, JsonSerializerOptions? jsonSerializerOptions = null, HttpClient httpClient = null)
        : base(logger, config, jsonSerializerOptions, httpClient)
    {
        _client = new ChatClient(model: "gpt-4o", _config.GetValue<string>("OPENAI_API_KEY"));
        _responseFormat = InitChatResponseFormat();
        _systemChatMessage = InitSystemChatMessage();
    }

    protected virtual SystemChatMessage InitSystemChatMessage()
    {
        return new("""
            You are a smart swiss pharmacy Assistant, that analyzes customer conditions using the LINDAAFF model. You deduct the most likely condition 
            and the recommended medication. You provide both the "humanly-recognized" names, as well as detailed information about the medication. 
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
                        "medication": {
                            "type": "object",
                            "properties": {
                                "MedicationDetails": {
                                    "type": "object",
                                    "properties": {
                                        "Name": { "type": "string" },
                                        "Characteristics": { "type": "string" },
                                        "Atc": { "type": "string" },
                                        "Dose": { "type": "string" },
                                        "Indication": { "type": "string" },
                                        "ContraIndication": { "type": "string" },
                                        "NarcoticCode": { "type": "string" },
                                        "ImageUrl": { "type": "string" }
                                    },
                                    "required": ["Name", "Characteristics", "Atc", "Dose", "Indication", "ContraIndication", "NarcoticCode", "ImageUrl"],
                                    "additionalProperties": false
                                },
                                "Url": { "type": "string" },
                                "UmlsCode": { "type": "string" },
                                "Description": { "type": "string" }
                            },
                            "required": ["MedicationDetails", "Url", "UmlsCode", "Description"],
                            "additionalProperties": false
                        },
                        "condition": {
                            "type": "object",
                            "properties": {
                                "Name": { "type": "string" },
                                "UmlsCode": { "type": "string" },
                                "Description": { "type": "string" }
                            },
                            "required": ["Name", "Description"],
                            "additionalProperties": false
                        },
                        "explanation": { "type": "string" }
                    },
                    "required": ["medication", "condition", "explanation"],
                    "additionalProperties": false
                }
                """u8.ToArray()),
            jsonSchemaIsStrict: true
        );
    }
}
