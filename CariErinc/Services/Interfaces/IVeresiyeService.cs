using CariErinc.Models;
using CariErinc.ViewModels;
using CariErinc.Helpers;

namespace CariErinc.Services.Interfaces;

public interface IVeresiyeService
{
    Task<VeresiyeIndexVM> GetPagedListAsync(int page = 1, int? musteriId = null, OdenmeDurumu? durum = null, DateTime? baslangic = null, DateTime? bitis = null);
    Task<List<Veresiye>> GetAllAsync(int? musteriId = null, OdenmeDurumu? durum = null, DateTime? baslangic = null, DateTime? bitis = null);
    Task<Veresiye?> GetByIdAsync(int id);
    Task<ServiceResult> SaveAsync(VeresiyeVM vm);
    Task<ServiceResult> SilAsync(int id);
    Task<ServiceResult> OdemeAlAsync(int veresiyeId, decimal tutar, string? aciklama, string? kullaniciId = null, VeresiyeOdemeTipi odemeTipi = VeresiyeOdemeTipi.Nakit);
    Task<ServiceResult> KompleKapatAsync(List<int> veresiyeIds, decimal odenenTutar, string? kullaniciId = null, VeresiyeOdemeTipi odemeTipi = VeresiyeOdemeTipi.Nakit);
}
