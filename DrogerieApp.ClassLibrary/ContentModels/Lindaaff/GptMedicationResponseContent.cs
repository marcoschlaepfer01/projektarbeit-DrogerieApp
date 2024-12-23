using DrogerieApp.ClassLibrary.ContentModels.Compendium;

namespace DrogerieApp.ClassLibrary.ContentModels.Lindaaff;

public class GptMedicationResponseContent
{
    public DetailedMedicationContent MedicationDetails { get; set; } = new();
    public string Url { get; set; } = string.Empty;
    public string UmlsCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
