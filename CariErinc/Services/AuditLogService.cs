using System.Text.Json;
using CariErinc.Data;
using CariErinc.Helpers;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    private string GetKullaniciAdi()
        => _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Sistem";

    public void LogHazirla(string tablo, int kayitId, string islem, 
                           object? eskiDeger = null, object? yeniDeger = null, 
                           string? aciklama = null)
    {
        var options = new JsonSerializerOptions
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            WriteIndented = false
        };

        var log = new AuditLog
        {
            Tablo = tablo,
            KayitId = kayitId,
            Islem = islem,
            EskiDeger = eskiDeger != null ? JsonSerializer.Serialize(eskiDeger, options) : null,
            YeniDeger = yeniDeger != null ? JsonSerializer.Serialize(yeniDeger, options) : null,
            KullaniciAdi = GetKullaniciAdi(),
            Tarih = DateTime.UtcNow,
            Aciklama = aciklama
        };

        _db.AuditLoglari.Add(log);
    }

    public async Task LogEkleAsync(string tablo, int kayitId, string islem, 
                                   object? eskiDeger = null, object? yeniDeger = null, 
                                   string? aciklama = null)
    {
        LogHazirla(tablo, kayitId, islem, eskiDeger, yeniDeger, aciklama);
        await _db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetLogsAsync(string? tablo = null, int? kayitId = null, 
                                                    DateTime? baslangic = null, DateTime? bitis = null)
    {
        var query = GetLogsQuery(tablo, kayitId, baslangic, bitis);
        return await query.OrderByDescending(l => l.Tarih).ToListAsync();
    }

    public async Task<PagedResult<AuditLog>> GetPagedLogsAsync(int page, int pageSize, string? tablo = null, int? kayitId = null,
                                                               DateTime? baslangic = null, DateTime? bitis = null)
    {
        var query = GetLogsQuery(tablo, kayitId, baslangic, bitis);
        return await query.OrderByDescending(l => l.Tarih).ToPagedListAsync(page, pageSize);
    }

    private IQueryable<AuditLog> GetLogsQuery(string? tablo = null, int? kayitId = null, 
                                               DateTime? baslangic = null, DateTime? bitis = null)
    {
        var query = _db.AuditLoglari.AsQueryable();

        if (!string.IsNullOrEmpty(tablo))
            query = query.Where(l => l.Tablo == tablo);

        if (kayitId.HasValue)
            query = query.Where(l => l.KayitId == kayitId.Value);

        if (baslangic.HasValue)
            query = query.Where(l => l.Tarih >= baslangic.Value.Date);

        if (bitis.HasValue)
            query = query.Where(l => l.Tarih < bitis.Value.Date.AddDays(1));

        return query;
    }
}
