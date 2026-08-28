using System.Diagnostics;
using ClimaPanel.Web.Models;
using ClimaPanel.Web.Models.ViewModels;
using ClimaPanel.Web.Services;
//using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPanel.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly IWeatherClient _weatherClient;

    public HomeController(IWeatherClient weatherClient)
    {
        _weatherClient = weatherClient;
    }



    //Samuel Alvarado:  Ahora se manejan casos de errores
    [HttpGet]
    public async Task<IActionResult> Index(
    string? q,
    CancellationToken cancellationToken)
    {
        var query = q?.Trim() ?? string.Empty;

        IReadOnlyList<LocationOption> results = Array.Empty<LocationOption>();

        if (!string.IsNullOrWhiteSpace(query))
        {
            try
            {
                results = await _weatherClient.SearchAsync(query, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (TaskCanceledException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "El servicio meteorológico tardó demasiado en responder.");
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "No fue posible consultar el servicio meteorológico.");
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "El servicio meteorológico entregó una respuesta inválida.");
            }
        }

        return View(new HomeViewModel
        {
            Query = query,
            Results = results
        });
    }

    //SAmuel Alvarado: Ahora no se expone detalle técnico de errores al usuario final, solo un mensaje genérico.
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {

        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            TechnicalDetail = "Ocurrió un error inesperado al procesar la solicitud."
        });
    }
}
