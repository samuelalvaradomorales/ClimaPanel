using ClimaPanel.Web.Common;
using ClimaPanel.Web.Models.ViewModels;
using ClimaPanel.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPanel.Web.Controllers;

public sealed class FavoritosController : Controller
{
    private readonly FavoriteService _service;
    private readonly WeatherAlertService _alertService;
    private readonly ICurrentUser _currentUser;

    public FavoritosController(
        FavoriteService service,
        WeatherAlertService alertService,
        ICurrentUser currentUser)
    {
        _service = service;
        _alertService = alertService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var user = _currentUser.GetCurrent();
        var model = await _service.ListAsync(
            user.Id,
            search,
            page,
            pageSize,
            cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(
        CreateFavoriteInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "No fue posible agregar la ciudad. Revise los datos recibidos.";
            return RedirectToAction("Index", "Home", new { q = input.Name });
        }

        try
        {
            var user = _currentUser.GetCurrent();
            var entity = await _service.CreateAsync(user.Id, input, cancellationToken);
            TempData["Success"] = $"{entity.Name} fue agregada a sus ciudades.";
            return RedirectToAction(nameof(Detalle), new { id = entity.Id });
        }
        catch (UserMessageException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    // Samuel Alvarado: controla el acceso y los errores del detalle de la ciudad favorita,
    // obtiene el clima y evalúa sus alertas antes de mostrarlas.
    [HttpGet]
    public async Task<IActionResult> Detalle(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = _currentUser.GetCurrent();

            // Obtiene la ciudad validando que pertenezca al usuario actual.
            var city = await _service.GetAsync(
                user.Id,
                id,
                cancellationToken);

            // Obtiene la información meteorológica utilizando el sistema de caché.
            var weather = await _service.GetWeatherAsync(
                user.Id,
                id,
                cancellationToken);

            // Samuel Alvarado: evalúa las alertas activas utilizando
            // las condiciones meteorológicas actuales.
            await _alertService.EvaluateAsync(
                user.Id,
                id,
                weather,
                cancellationToken);

            // Recupera las alertas después de evaluarlas para mostrar
            // en pantalla su estado actualizado.
            var alerts = await _alertService.ListAsync(
                user.Id,
                id,
                cancellationToken);

            return View(new FavoriteDetailsViewModel
            {
                City = city,
                Weather = weather,
                Alerts = alerts
            });
        }
        catch (UserMessageException)
        {
            // Evita revelar información sobre recursos de otros usuarios.
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refrescar(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = _currentUser.GetCurrent();

            var weather = await _service.RefreshAsync(
                user.Id,
                id,
                cancellationToken);

            // Samuel Alvarado: después de obtener información meteorológica nueva,
            // vuelve a evaluar las alertas configuradas para la ciudad.
            await _alertService.EvaluateAsync(
                user.Id,
                id,
                weather,
                cancellationToken);

            TempData["Success"] =
                weather.Source == "STALE"
                    ? "No fue posible obtener información nueva. Se muestran los últimos datos disponibles."
                    : "El pronóstico fue actualizado.";

            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (UserMessageException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = _currentUser.GetCurrent();
            await _service.DeleteAsync(user.Id, id, cancellationToken);
            TempData["Success"] = "La ciudad fue eliminada de sus favoritos.";
        }
        catch (UserMessageException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // Samuel Alvarado: crea una nueva alerta para una ciudad favorita
    // validando que la solicitud corresponda al usuario actual.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearAlerta(
        CreateWeatherAlertInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] =
                "No fue posible crear la alerta. Revise los datos ingresados.";

            return RedirectToAction(
                nameof(Detalle),
                new { id = input.FavoriteId });
        }

        try
        {
            var user = _currentUser.GetCurrent();

            await _alertService.CreateAsync(
                user.Id,
                input,
                cancellationToken);

            TempData["Success"] =
                "La alerta fue creada correctamente.";
        }
        catch (UserMessageException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(
            nameof(Detalle),
            new { id = input.FavoriteId });
    }


    // Samuel Alvarado: activa o desactiva una alerta verificando
    // que corresponda a una ciudad favorita del usuario actual.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAlerta(
        Guid favoriteId,
        Guid alertId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = _currentUser.GetCurrent();

            await _alertService.ToggleAsync(
                user.Id,
                favoriteId,
                alertId,
                cancellationToken);

            TempData["Success"] =
                "El estado de la alerta fue actualizado.";
        }
        catch (UserMessageException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(
            nameof(Detalle),
            new { id = favoriteId });
    }


    // Samuel Alvarado: elimina una alerta verificando
    // que pertenezca a una ciudad favorita del usuario actual.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarAlerta(
        Guid favoriteId,
        Guid alertId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = _currentUser.GetCurrent();

            await _alertService.DeleteAsync(
                user.Id,
                favoriteId,
                alertId,
                cancellationToken);

            TempData["Success"] =
                "La alerta fue eliminada correctamente.";
        }
        catch (UserMessageException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(
            nameof(Detalle),
            new { id = favoriteId });
    }

}
