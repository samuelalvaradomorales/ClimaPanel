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
    private readonly ILogger<HomeController> _logger;


    public HomeController(
    IWeatherClient weatherClient,
    ILogger<HomeController> logger)
    {
        _weatherClient = weatherClient;
        _logger = logger;
    }


    // Samuel Alvarado: manejo seguro de errores en consultas a Open-Meteo.
    // Se registran detalles técnicos en logs y se muestran mensajes amigables al usuario.
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
                results = await _weatherClient.SearchAsync(
                    query,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "La búsqueda meteorológica fue cancelada por el cliente. Consulta: {Query}",
                    query);

                return StatusCode(499);
            }
            catch (TaskCanceledException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Timeout al consultar Open-Meteo. Consulta: {Query}",
                    query);

                ModelState.AddModelError(
                    string.Empty,
                    "El servicio meteorológico tardó demasiado en responder.");
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Error HTTP al consultar Open-Meteo. Consulta: {Query}",
                    query);

                ModelState.AddModelError(
                    string.Empty,
                    "No fue posible consultar el servicio meteorológico.");
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Respuesta inválida de Open-Meteo. Consulta: {Query}",
                    query);

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
