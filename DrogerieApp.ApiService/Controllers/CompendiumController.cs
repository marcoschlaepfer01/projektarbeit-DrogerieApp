using DrogerieApp.ClassLibrary.ContentModels.Compendium;
using Microsoft.AspNetCore.Mvc;
using PuppeteerSharp;
using System.Text.Json;

namespace DrogerieApp.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompendiumController : ControllerBase
{
    // A static, lazily-initialized browser instance for efficiency
    private static readonly Task<IBrowser> _browserTask = InitializeBrowserAsync();

    private readonly ILogger<CompendiumController> _logger;
    private readonly string _baseAddress;

    public CompendiumController(ILogger<CompendiumController> logger, IConfiguration config)
    {
        _logger = logger;
        _baseAddress = config["Urls:Compendium"] ?? string.Empty;
    }

    /// <summary>
    /// Pre-downloads Chromium and launches it once for all requests.
    /// </summary>
    private static async Task<IBrowser> InitializeBrowserAsync()
    {
        var fetcher = new BrowserFetcher();
        await fetcher.DownloadAsync().ConfigureAwait(false);
        return await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Timeout = 120000,
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Scrapes the compendium.ch site for a given medicament name.
    /// Usage: GET /api/Compedium/GetMedicamentList?name=aspirin
    /// </summary>
    /// <param name="name">Name of the medicament to search for</param>
    /// <returns>List of MedicamentInfo objects</returns>
    [HttpGet("GetMedicamentList")]
    public async Task<IActionResult> GetMedicamentList([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Parameter 'name' is required.");

        var url = $"{_baseAddress}/search?q={name}";

        string jsonResult;
        try
        {
            var browser = await _browserTask.ConfigureAwait(false);
            using var page = await browser.NewPageAsync().ConfigureAwait(false);

            // Navigate and wait for network to be idle
            await page.GoToAsync(url, WaitUntilNavigation.Networkidle0).ConfigureAwait(false);

            // Evaluate JavaScript directly on the page to extract the data we need
            // We look for elements in the '#productsResult' container that match the structure.
            // The JS code returns an array of objects { name: "...", url: "..." }.
            jsonResult = await page.EvaluateFunctionAsync<string>(@"
                    () => {
                        const container = document.querySelector('#productsResult');
                        if(!container) return JSON.stringify([]);

                        const products = container.querySelectorAll('.row.product');
                        if (!products || products.length === 0) return JSON.stringify([]);

                        const results = [];
                        products.forEach(prod => {
                            const link = prod.querySelector('a.linkProduct');
                            const nameNode = link ? link.querySelector('strong[data-testid=""product-description""]') : null;
                            if (link && nameNode) {
                                const productName = nameNode.innerText.trim();
                                const productUrl = link.href;
                                results.push({ Name: productName, Url: productUrl });
                            }
                        });

                        return JSON.stringify(results);
                    }
                ").ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error fetching the page");
            return StatusCode(500, $"Error fetching the page: {ex.Message}");
        }

        // Deserialize the JSON extracted from page evaluation
        var medicaments = JsonSerializer.Deserialize<List<MedicationContent>>(jsonResult) ?? new List<MedicationContent>();

        // Return a JSON result (ASP.NET Core automatically serializes objects returned with Ok(...))
        return Ok(medicaments);
    }

    [HttpGet("GetMedicamentDetails")]
    public async Task<IActionResult> GetMedicamentDetails([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("Parameter 'url' is required."); // minor fix: say 'url'

        string jsonResult;
        try
        {
            var browser = await _browserTask.ConfigureAwait(false);
            using var page = await browser.NewPageAsync().ConfigureAwait(false);

            // Navigate
            await page.GoToAsync(url, WaitUntilNavigation.Networkidle0).ConfigureAwait(false);

            // Evaluate with defensive checks:
            jsonResult = await page.EvaluateFunctionAsync<string>(@"
            () => {
                const container = document.querySelector('div[id=""productDetail""]');
                if (!container) {
                    return JSON.stringify({
                        Name: '',
                        ImageUrl: '',
                        Characteristics: '',
                        Atc: '',
                        Indication: '',
                        Dose: '',
                        ContraIndication: '',
                        NarcoticCode: ''
                    });
                }

                // Optional chaining or null checks
                const nameEl = document.querySelector('span[itemprop=""name""]');
                const imageEl = document.querySelector('img[itemprop=""image""]');
                const characteristicsEl = container.querySelector('span[title=""Product.Characteristics""]')?.closest('.row');
                const atcEl = container.querySelector('span[title=""ATC""]')?.closest('.row');
                const indicationEl = container.querySelector('span[title=""Indikation""]')?.closest('.row');
                const doseEl = container.querySelector('span[title=""Dosierung""]')?.closest('.row');
                const contraEl = container.querySelector('span[title=""Kontraindikation""]')?.closest('.row');
                
                // Narcotic code might be in different spots:
                let narcoticEl = container.querySelector('div[class=""col-6 col-lg-3 text-sm-center ""]');
                if (!narcoticEl) {
                    narcoticEl = container.querySelector('div[class=""col-6 col-lg-3 text-sm-center narcoticCode""]');
                }

                // Now safely extract text or fallback to empty string
                const name = nameEl ? nameEl.innerText.trim() : '';
                const imageUrl = imageEl ? imageEl.src : '';
                const characteristics = characteristicsEl 
                    ? characteristicsEl.querySelector('div[class=""col-lg-18""]')?.innerText.trim() 
                    : '';
                const atc = atcEl 
                    ? atcEl.querySelector('div[class=""col-lg-18""]')?.innerText.trim()
                    : '';
                const indication = indicationEl 
                    ? indicationEl.querySelector('div[class=""col-lg-18""]')?.innerText.trim()
                    : '';
                const dose = doseEl 
                    ? doseEl.querySelector('div[class=""col-lg-18""]')?.innerText.trim()
                    : '';
                const contraIndication = contraEl 
                    ? contraEl.querySelector('div[class=""col-lg-18""]')?.innerText.trim()
                    : '';
                const narcoticCode = narcoticEl ? narcoticEl.innerText.trim() : '';

                const result = {
                    Name: name,
                    ImageUrl: imageUrl,
                    Characteristics: characteristics || '',
                    Atc: atc || '',
                    Indication: indication || '',
                    Dose: dose || '',
                    ContraIndication: contraIndication || '',
                    NarcoticCode: narcoticCode || ''
                };
                
                return JSON.stringify(result);
            }
        ").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching the page");
            return StatusCode(500, $"Error fetching the page: {ex.Message}");
        }

        var result = JsonSerializer.Deserialize<DetailedMedicationContent>(jsonResult) ?? new();
        result.Url = url;
        return Ok(result);
    }

}