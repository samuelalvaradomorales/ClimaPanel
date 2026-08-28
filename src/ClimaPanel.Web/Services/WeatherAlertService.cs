using ClimaPanel.Web.Data;
using ClimaPanel.Web.Models;
using ClimaPanel.Web.Models.ViewModels;
using ClimaPanel.Web.Common;
using Microsoft.EntityFrameworkCore;

namespace ClimaPanel.Web.Services;

/// <summary>
/// Contrato inicial de la funcionalidad nueva. Debe implementar el flujo
/// completo sin cambiar estas firmas públicas.
/// </summary>
public sealed class WeatherAlertService
{
    private readonly AppDbContext _db;

    public WeatherAlertService(AppDbContext db)
    {
        _db = db;
    }

    // Samuel Alvarado: lista alertas solo si la ciudad favorita pertenece
    // al usuario actual, evitando acceso a alertas de otros usuarios.
    public async Task<IReadOnlyList<WeatherAlertItem>> ListAsync(
        string userId,
        Guid favoriteId,
        CancellationToken cancellationToken)
    {
        var favoriteExists = await _db.FavoriteCities
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == favoriteId && x.UserId == userId,
                cancellationToken);

        if (!favoriteExists)
        {
            throw new UserMessageException(
                "No se encontró la ciudad solicitada.");
        }

        var alerts = await _db.WeatherAlerts
            .AsNoTracking()
            .Where(x => x.FavoriteId == favoriteId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return alerts
            .Select(x => new WeatherAlertItem(
                x.Id,
                x.FavoriteId,
                x.Metric,
                x.Operator,
                x.Threshold,
                x.IsEnabled,
                x.IsTriggered,
                x.CreatedAtUtc,
                x.LastEvaluatedAtUtc,
                x.LastTriggeredAtUtc))
            .ToArray();
    }

    // Samuel Alvarado: valida operadores y rangos permitidos
    // para los distintos tipos de alerta meteorológica.
    private static void ValidateThreshold(
        WeatherMetric metric,
        ThresholdOperator thresholdOperator,
        double threshold)
    {
        if (!Enum.IsDefined(metric))
        {
            throw new UserMessageException(
                "La métrica seleccionada no es válida.");
        }

        if (!Enum.IsDefined(thresholdOperator))
        {
            throw new UserMessageException(
                "El operador seleccionado no es válido.");
        }

        var isValid = metric switch
        {
            WeatherMetric.TemperatureC =>
                threshold >= -80 && threshold <= 80,

            WeatherMetric.HumidityPercent =>
                threshold >= 0 && threshold <= 100,

            WeatherMetric.PrecipitationMm =>
                threshold >= 0 && threshold <= 500,

            WeatherMetric.WindSpeedKmh =>
                threshold >= 0 && threshold <= 300,

            _ => false
        };

        if (!isValid)
        {
            throw new UserMessageException(
                "El umbral ingresado está fuera del rango permitido.");
        }
    }

    // Samuel Alvarado: crea alertas validando propiedad del favorito,
    // límite de alertas activas y rangos permitidos para cada métrica.
    public async Task<WeatherAlertItem> CreateAsync(
        string userId,
        CreateWeatherAlertInput input,
        CancellationToken cancellationToken)
    {
        if (input.FavoriteId == Guid.Empty)
        {
            throw new UserMessageException(
                "La ciudad favorita no es válida.");
        }

        var favoriteExists = await _db.FavoriteCities
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == input.FavoriteId &&
                     x.UserId == userId,
                cancellationToken);

        if (!favoriteExists)
        {
            throw new UserMessageException(
                "No se encontró la ciudad solicitada.");
        }

        var activeAlerts = await _db.WeatherAlerts
            .CountAsync(
                x => x.FavoriteId == input.FavoriteId &&
                     x.IsEnabled,
                cancellationToken);

        if (activeAlerts >= 5)
        {
            throw new UserMessageException(
                "La ciudad ya tiene el máximo de 5 alertas activas.");
        }

        ValidateThreshold(input.Metric, input.Operator, input.Threshold);

        var entity = new WeatherAlert
        {
            FavoriteId = input.FavoriteId,
            Metric = input.Metric,
            Operator = input.Operator,
            Threshold = input.Threshold,
            IsEnabled = true,
            IsTriggered = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.WeatherAlerts.Add(entity);

        await _db.SaveChangesAsync(cancellationToken);

        return new WeatherAlertItem(
            entity.Id,
            entity.FavoriteId,
            entity.Metric,
            entity.Operator,
            entity.Threshold,
            entity.IsEnabled,
            entity.IsTriggered,
            entity.CreatedAtUtc,
            entity.LastEvaluatedAtUtc,
            entity.LastTriggeredAtUtc);
    }

    // Samuel Alvarado: activa o desactiva una alerta verificando primero
    // que la ciudad favorita pertenezca al usuario actual.
    public async Task ToggleAsync(
        string userId,
        Guid favoriteId,
        Guid alertId,
        CancellationToken cancellationToken)
    {
        // Validamos que el usuario sea propietario de la ciudad favorita.
        var favoriteExists = await _db.FavoriteCities
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == favoriteId &&
                     x.UserId == userId,
                cancellationToken);

        if (!favoriteExists)
        {
            throw new UserMessageException(
                "No se encontró la ciudad solicitada.");
        }

        // Buscamos la alerta únicamente dentro de la ciudad indicada.
        var alert = await _db.WeatherAlerts
            .FirstOrDefaultAsync(
                x => x.Id == alertId &&
                     x.FavoriteId == favoriteId,
                cancellationToken);

        if (alert is null)
        {
            throw new UserMessageException(
                "No se encontró la alerta solicitada.");
        }

        // Si la alerta estaba desactivada y se quiere volver a activar,
        // verificamos que no se supere el máximo de 5 alertas activas.
        if (!alert.IsEnabled)
        {
            var activeAlerts = await _db.WeatherAlerts
                .CountAsync(
                    x => x.FavoriteId == favoriteId &&
                         x.IsEnabled,
                    cancellationToken);

            if (activeAlerts >= 5)
            {
                throw new UserMessageException(
                    "La ciudad ya tiene el máximo de 5 alertas activas.");
            }
        }

        // Cambiamos el estado actual de la alerta.
        alert.IsEnabled = !alert.IsEnabled;

        // Una alerta desactivada no debe permanecer marcada como disparada.
        if (!alert.IsEnabled)
        {
            alert.IsTriggered = false;
        }

        // Persistimos el nuevo estado en SQLite.
        await _db.SaveChangesAsync(cancellationToken);
    }

    // Samuel Alvarado: elimina una alerta verificando que la ciudad favorita
    // pertenezca al usuario actual y que la alerta corresponda a esa ciudad.
    public async Task DeleteAsync(
        string userId,
        Guid favoriteId,
        Guid alertId,
        CancellationToken cancellationToken)
    {
        // Validamos que el usuario actual sea propietario de la ciudad favorita.
        var favoriteExists = await _db.FavoriteCities
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == favoriteId &&
                     x.UserId == userId,
                cancellationToken);

        if (!favoriteExists)
        {
            throw new UserMessageException(
                "No se encontró la ciudad solicitada.");
        }

        // Buscamos la alerta únicamente dentro de la ciudad favorita indicada.
        var alert = await _db.WeatherAlerts
            .FirstOrDefaultAsync(
                x => x.Id == alertId &&
                     x.FavoriteId == favoriteId,
                cancellationToken);

        if (alert is null)
        {
            throw new UserMessageException(
                "No se encontró la alerta solicitada.");
        }

        // Eliminamos la alerta y persistimos el cambio en SQLite.
        _db.WeatherAlerts.Remove(alert);

        await _db.SaveChangesAsync(cancellationToken);
    }
    // Samuel Alvarado: evalúa todas las alertas activas de una ciudad
    // utilizando las condiciones meteorológicas actuales.
    public async Task<AlertEvaluationResult> EvaluateAsync(
        string userId,
        Guid favoriteId,
        WeatherCard weather,
        CancellationToken cancellationToken)
    {
        // Validamos que la ciudad favorita pertenezca al usuario actual.
        var favoriteExists = await _db.FavoriteCities
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == favoriteId &&
                     x.UserId == userId,
                cancellationToken);

        if (!favoriteExists)
        {
            throw new UserMessageException(
                "No se encontró la ciudad solicitada.");
        }

        // Solo evaluamos las alertas que se encuentran activas.
        var alerts = await _db.WeatherAlerts
            .Where(x => x.FavoriteId == favoriteId &&
                        x.IsEnabled)
            .ToListAsync(cancellationToken);

        var evaluated = 0;
        var triggered = 0;
        var evaluatedAtUtc = DateTime.UtcNow;

        foreach (var alert in alerts)
        {
            // Obtenemos el valor meteorológico correspondiente
            // al tipo de métrica configurado en la alerta.
            var currentValue = alert.Metric switch
            {
                WeatherMetric.TemperatureC => weather.TemperatureC,
                WeatherMetric.HumidityPercent => weather.HumidityPercent,
                WeatherMetric.PrecipitationMm => weather.PrecipitationMm,
                WeatherMetric.WindSpeedKmh => weather.WindSpeedKmh,
                _ => throw new UserMessageException(
                    "La métrica de la alerta no es válida.")
            };

            // Evaluamos el valor actual contra el umbral configurado.
            var isTriggered = alert.Operator switch
            {
                ThresholdOperator.GreaterThanOrEqual =>
                    currentValue >= alert.Threshold,

                ThresholdOperator.LessThanOrEqual =>
                    currentValue <= alert.Threshold,

                _ => throw new UserMessageException(
                    "El operador de la alerta no es válido.")
            };

            alert.IsTriggered = isTriggered;
            alert.LastEvaluatedAtUtc = evaluatedAtUtc;

            // Si se cumple la condición, registramos cuándo
            // fue disparada por última vez.
            if (isTriggered)
            {
                alert.LastTriggeredAtUtc = evaluatedAtUtc;
                triggered++;
            }

            evaluated++;
        }

        // Persistimos el estado y las fechas de evaluación de las alertas.
        if (evaluated > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new AlertEvaluationResult(
            evaluated,
            triggered);
    }
}
