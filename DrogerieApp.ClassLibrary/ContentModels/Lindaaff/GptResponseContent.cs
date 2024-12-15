namespace DrogerieApp.ClassLibrary.ContentModels.Lindaaff;

public class GptResponseContent
{
    public GptConditionResponseContent Condition { get; set; } = new();
    public GptMedicationResponseContent Medication { get; set; } = new();
    public string Explanation { get; set; } = string.Empty;
}
