using ClimaPanel.Web.Common;
using ClimaPanel.Web.Data;
using ClimaPanel.Web.Models;
using ClimaPanel.Web.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ClimaPanel.Web.Services;

public sealed class FavoriteService
{
    private readonly AppDbContext _db;
    private readonly WeatherCacheService _weatherCache;

    public FavoriteService(AppDbContext db, WeatherCacheService weatherCache)
    {
        _db = db;
        _weatherCache = weatherCache;
    }

    public async Task<FavoriteCity> CreateAsync(
        string userId,
        CreateFavoriteInput input,
        CancellationToken cancellationToken)
    {
        var alreadyExists = await _db.FavoriteCities.AnyAsync(
            x => x.UserId == userId && x.LocationId == input.LocationId,
            cancellationToken);

        if (alreadyExists)
        {
            throw new UserMessageException("La ciudad ya se encuentra en sus favoritos.");
        }

        var entity = new FavoriteCity
        {
            UserId = userId,
            LocationId = input.LocationId,
            Name = input.Name.Trim(),
            Country = input.Country.Trim(),
            CountryCode = input.CountryCode.Trim().ToUpperInvariant(),
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            Timezone = input.Timezone.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.FavoriteCities.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<FavoriteListViewModel> ListAsync(
        string userId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var searchTerm = search?.Trim();

        IQueryable<FavoriteCity> query = _db.FavoriteCities
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                EF.Functions.Like(x.Name, $"%{searchTerm}%") ||
                EF.Functions.Like(x.Country, $"%{searchTerm}%"));
        }

        query = query.OrderBy(x => x.Name);

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(ToListItem)
            .ToArray();

        return new FavoriteListViewModel
        {
            Items = items,
            Search = searchTerm ?? string.Empty,
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = Math.Max(
                1,
                (int)Math.Ceiling(total / (double)pageSize))
        };
    }

    //Samuel Alvarado:  Ahora se considera userId para obtener la ciudad favorita, para evitar que un usuario pueda acceder a la ciudad favorita de otro usuario.
    public async Task<FavoriteCity> GetAsync(
        string userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _db.FavoriteCities
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id && x.UserId == userId,
                cancellationToken)
            ?? throw NotFound();
    }

    public async Task DeleteAsync(
        string userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await _db.FavoriteCities
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound();

        _db.FavoriteCities.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    //Samuel Alvarado: Se considera userId para obtener la ciudad favorita y su clima, para evitar que un usuario pueda acceder a la ciudad favorita de otro usuario.
    public async Task<WeatherCard> GetWeatherAsync(
        string userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var city = await _db.FavoriteCities
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id && x.UserId == userId,
                cancellationToken)
            ?? throw NotFound();

        return await _weatherCache.GetAsync(city, false, cancellationToken);
    }

    public Task<WeatherCard> RefreshAsync(
        string userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException("La actualización manual todavía no está implementada.");
    }

    private static FavoriteListItem ToListItem(FavoriteCity entity) => new(
        entity.Id,
        entity.Name,
        entity.Country,
        entity.CountryCode,
        entity.Timezone,
        entity.CreatedAtUtc);

    private static UserMessageException NotFound() =>
        new("No se encontró la ciudad solicitada.");
}
