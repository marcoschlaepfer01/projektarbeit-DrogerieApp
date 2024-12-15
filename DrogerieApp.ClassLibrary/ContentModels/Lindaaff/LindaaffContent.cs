using System.Text.Json;

namespace DrogerieApp.ClassLibrary.ContentModels.Lindaaff;

/// <summary>
/// Represents the LINDAAFF content used for assessing patient complaints in Swiss pharmacies.
/// </summary>
public class LindaaffContent
{
    /// <summary>
    /// Gets or sets the localization of the complaint.
    /// </summary>
    public IEnumerable<string> Localization { get; set; } = [];

    /// <summary>
    /// Gets or sets additional notes regarding the localization.
    /// </summary>
    public string AdditionalLocalizationNotes { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the intensity of the complaint.
    /// </summary>
    public string Intensity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the nature or type of the complaint.
    /// </summary>
    public string Nature { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration of the complaint.
    /// </summary>
    public string Duration { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets other accompanying symptoms.
    /// </summary>
    public string OtherSymptoms { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets factors that worsen the complaint.
    /// </summary>
    public string WorseningFactors { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets factors that alleviate the complaint.
    /// </summary>
    public string AlleviatingFactors { get; set; } = string.Empty;

    /// <summary>
    /// Returns a JSON-formatted string representation of the LindaaffContent with an indentation of 4 spaces.
    /// </summary>
    /// <returns>A JSON string containing all properties and their values, formatted with 4-space indentation.</returns>
    public override string ToString()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return JsonSerializer.Serialize(this, options);
    }
}