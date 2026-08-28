using ClimaPanel.Web.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace ClimaPanel.Web.Services;

public sealed class WeatherCacheService
{


    //Samuel Alvarado: caché independiente por ciudad + cancelación real.
    private static string GetCacheKey(FavoriteCity city)
    {
        return $"forecast:{city.LocationId}";
    }

    private readonly IMemoryCache _cache;
    private readonly IWeatherClient _weatherClient;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _locks = new();

    public WeatherCacheService(
        IMemoryCache cache,
        IWeatherClient weatherClient,
        IConfiguration configuration)
    {
        _cache = cache;
        _weatherClient = weatherClient;
        _configuration = configuration;
    }

    // Samuel Alvarado: genera una clave de caché de respaldo por ciudad,
    // utilizada para recuperar el último pronóstico disponible ante fallas de Open-Meteo.
    private static string GetStaleCacheKey(FavoriteCity city)
    {
        return $"forecast-stale:{city.LocationId}";
    }

    // Samuel Alvarado: evita llamadas simultáneas duplicadas a Open-Meteo mediante un bloqueo independiente por ciudad.
    public async Task<WeatherCard> GetAsync(
        FavoriteCity city,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var cacheKey = GetCacheKey(city);

        var staleCacheKey = GetStaleCacheKey(city);

        if (!forceRefresh &&
            _cache.TryGetValue(cacheKey, out WeatherCard? cached) &&
            cached is not null)
        {
            return cached with { Source = "CACHE" };
        }

        var semaphore = _locks.GetOrAdd(
            city.LocationId,
            _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken);

        try
        {
            // Puede ocurrir que otra solicitud haya cargado
            // el clima mientras esta solicitud esperaba.
            if (!forceRefresh &&
                _cache.TryGetValue(cacheKey, out cached) &&
                cached is not null)
            {
                return cached with { Source = "CACHE" };
            }


            WeatherCard response;

            try
            {
                var reading = await _weatherClient.GetForecastAsync(
                    city.Latitude,
                    city.Longitude,
                    city.Timezone,
                    cancellationToken);

                response = new WeatherCard(
                    "LIVE",
                    reading.FetchedAtUtc,
                    reading.TemperatureC,
                    reading.HumidityPercent,
                    reading.PrecipitationMm,
                    reading.WindSpeedKmh,
                    reading.Daily);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                if (_cache.TryGetValue(
                    staleCacheKey,
                    out WeatherCard? stale) &&
                    stale is not null)
                {
                    return stale with { Source = "STALE" };
                }

                throw;
            }

            var cacheSeconds =
                _configuration.GetValue("OpenMeteo:CacheSeconds", 90);

            _cache.Set(
                cacheKey,
                response,
                TimeSpan.FromSeconds(cacheSeconds));

            var staleSeconds =
                _configuration.GetValue("OpenMeteo:StaleCacheSeconds", 600);

            _cache.Set(
                staleCacheKey,
                response,
                TimeSpan.FromSeconds(staleSeconds));

            return response;


        }
        finally
        {
            semaphore.Release();
        }
    }
}
