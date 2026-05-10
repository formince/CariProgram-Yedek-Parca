using CariErinc.Data;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CariErinc.Services;

public class AyarService : IAyarService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(60);

    public AyarService(IServiceScopeFactory scopeFactory, IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
    }

    private string CacheKey =>
        _httpContextAccessor.HttpContext?.Items["TenantInfo"] is TenantInfo t && !string.IsNullOrEmpty(t.Subdomain)
            ? $"isletme_ayarlar_{t.Subdomain}"
            : "isletme_ayarlar_default";

    public async Task<string?> GetAsync(string anahtar)
    {
        var all = await GetAllAsync();
        return all.TryGetValue(anahtar, out var val) ? val : null;
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, string>? cached) && cached is not null)
            return cached;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ayarlar = await db.IsletmeAyarlar.AsNoTracking().ToListAsync();
        var dict = ayarlar.ToDictionary(a => a.Anahtar, a => a.Deger);
        _cache.Set(CacheKey, dict, CacheTtl);
        return dict;
    }

    public async Task SetAsync(string anahtar, string deger)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ayar = await db.IsletmeAyarlar.FirstOrDefaultAsync(a => a.Anahtar == anahtar);
        if (ayar is null)
        {
            db.IsletmeAyarlar.Add(new IsletmeAyar { Anahtar = anahtar, Deger = deger });
        }
        else
        {
            ayar.Deger = deger;
            db.IsletmeAyarlar.Update(ayar);
        }
        await db.SaveChangesAsync();
        InvalidateCache();
    }

    public void InvalidateCache()
    {
        _cache.Remove(CacheKey);
    }

    public async Task<IReadOnlyList<int>> GetKdvOranlariListeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var raw = await GetAsync(KdvOranlariAyarlari.Anahtar);
        var parsed = KdvOranlariAyarlari.ParseLenient(raw);
        if (parsed.Count > 0)
            return parsed;
        var varsayilanKdv = await GetVarsayilanKdvOraniAsync();

        var fb = new List<int> { 0, 1, 8, 10, 20 };
        if (!fb.Contains(varsayilanKdv))
            fb.Add(varsayilanKdv);
        return fb.Distinct().OrderBy(x => x).ToList();
    }

    public async Task<int> GetVarsayilanKdvOraniAsync()
    {
        var val = await GetAsync("VarsayilanKdv");
        return int.TryParse(val, out var kdv) && kdv >= 0 && kdv <= 100 ? kdv : 20;
    }
}
