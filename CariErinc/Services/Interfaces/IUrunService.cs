using CariErinc.Models;
using CariErinc.ViewModels;
using CariErinc.Helpers;

namespace CariErinc.Services.Interfaces;

public interface IUrunService
{
    Task<UrunIndexVM> GetPagedListAsync(int page = 1, string? arama = null, string? kategori = null, int? tedarikciId = null, string? stokDurumu = null);
    Task<List<Urun>> GetAllAsync(string? arama = null);
    Task<Urun?> GetByIdAsync(int id);
    Task<ServiceResult> SaveAsync(UrunVM vm);
    Task<ServiceResult> SilAsync(int id);
    Task<Dictionary<int, SonAlisInfoVM>> GetSonAlisBilgileriAsync(List<int> urunIds);
    Task<List<StokHareket>> GetSonStokHareketleriAsync(int urunId, int count);
    Task<List<Urun>> SearchAsync(string query, int limit);
    Task<Urun?> GetByBarkodAsync(string barkod);
    Task<List<Urun>> GetByIdsAsync(List<int> urunIds);
}
