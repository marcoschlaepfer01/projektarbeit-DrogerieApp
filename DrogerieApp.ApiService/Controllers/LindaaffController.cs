using DrogerieApp.Backend.Models;
using DrogerieApp.ClassLibrary.ContentModels.Lindaaff;
using Microsoft.AspNetCore.Mvc;

namespace DrogerieApp.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LindaaffController : ControllerBase
{
    private readonly ILogger<LindaaffController> _logger;
    private readonly BaseModel _model;

    public LindaaffController(ILogger<LindaaffController> logger, BaseModel model)
    {
        _logger = logger;
        _model = model;
    }

    [HttpGet("GetSampleLindaaffContent")]
    public IEnumerable<LindaaffContent> Get()
    {
        var sample = new LindaaffContent
        {
            Localization = new List<string> { "Kopf", "Bauch" },
            AdditionalLocalizationNotes = "Zwischen den Augen und im Magen",
            Nature = "Stechender Schmerz",
            Intensity = "5/10",
            Duration = "Seit dem Aufwachen vor 4 Stunden",
            OtherSymptoms = "Übelkeit und Filmriss",
            WorseningFactors = "Bewegung und der Gedanke an Alkohol",
            AlleviatingFactors = "Dunkelheit und Liegen"
        };
        return new List<LindaaffContent> { sample };
    }

    [HttpPost("ProcessLindaaffContent")]
    public async Task<IActionResult> PostAsync([FromBody] LindaaffContent model)
    {

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _model.ProcessAsync(model);
        if (result == null)
        {
            return StatusCode(500, "Processing failed.");
        }
        return Ok(result);
    }
}
