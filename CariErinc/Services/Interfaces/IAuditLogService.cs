using CariErinc.Helpers;
using CariErinc.Models;

namespace CariErinc.Services.Interfaces;

public interface IAuditLogService
{
    void LogHazirla(string tablo, int kayitId, string islem, 
                    object? eskiDeger = null, object? yeniDeger = null, 
                    string? aciklama = null);

    Task LogEkleAsync(string tablo, int kayitId, string islem, 
                      object? eskiDeger = null, object? yeniDeger = null, 
                      string? aciklama = null);

    Task<List<AuditLog>> GetLogsAsync(string? tablo = null, int? kayitId = null,
                                       DateTime? baslangic = null, DateTime? bitis = null);

    Task<PagedResult<AuditLog>> GetPagedLogsAsync(int page, int pageSize, string? tablo = null, int? kayitId = null,
                                                   DateTime? baslangic = null, DateTime? bitis = null);
}
