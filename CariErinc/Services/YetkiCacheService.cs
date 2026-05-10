using CariErinc.Data;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CariErinc.Services;

public class YetkiCacheService : IYetkiCacheService
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    private const string AllKeyPrefix = "yetki_rol_";

    public YetkiCacheService(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public async Task<HashSet<(string Controller, string Action)>> GetYetkilerAsync(IEnumerable<int> rolIds)
    {
        var ids = rolIds.ToArray();
        var cacheKey = AllKeyPrefix + string.Join("_", ids.OrderBy(x => x));

        if (_cache.TryGetValue(cacheKey, out HashSet<(string, string)>? cached) && cached is not null)
            return cached;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var yetkiler = await db.RolYetkiler
            .Where(ry => ids.Contains(ry.RolId))
            .Select(ry => new { ry.ControllerAdi, ry.ActionAdi })
            .AsNoTracking()
            .ToListAsync();

        var set = yetkiler.Select(y => (y.ControllerAdi, y.ActionAdi)).ToHashSet();
        _cache.Set(cacheKey, set, CacheTtl);
        return set;
    }

    public async Task<List<RolYetki>> GetSidebarLinksAsync(IEnumerable<int> rolIds)
    {
        var ids = rolIds.ToArray();
        var cacheKey = AllKeyPrefix + "sidebar_" + string.Join("_", ids.OrderBy(x => x));

        if (_cache.TryGetValue(cacheKey, out List<RolYetki>? cached) && cached is not null)
            return cached;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var links = await db.RolYetkiler
            .Where(ry => ids.Contains(ry.RolId) && ry.SidebarGoruntuAdi != null)
            .AsNoTracking()
            .ToListAsync();

        // Aynı Controller+Action için tekilleştir (birden fazla rol aynı linki paylaşabilir)
        var distinctLinks = links
            .GroupBy(l => (l.ControllerAdi, l.ActionAdi))
            .Select(g => g.First())
            .ToList();

        _cache.Set(cacheKey, distinctLinks, CacheTtl);
        return distinctLinks;
    }

    public void InvalidateRol(int rolId)
    {
        // Prefix'e göre temizleme için tüm yetki cache'ini flush et
        InvalidateAll();
    }

    public void InvalidateAll()
    {
        if (_cache is MemoryCache mc)
            mc.Clear();
    }
}
