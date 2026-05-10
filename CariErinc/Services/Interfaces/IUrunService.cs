using CariErinc.Models;
using CariErinc.ViewModels;
using CariErinc.Helpers;

namespace CariErinc.Services.Interfaces;

public interface IUrunService
{
    Task<UrunIndexVM> GetPagedListAsync(
        int page = 1,
        string? arama = null,
        string? kategori = null,
        int? tedarikciId = null,
        string? stokDurumu = null,
        string? aracMarkasi = null,
        string? aracModeli = null,
        ParcaTipi? parcaTipi = null,
        string? parcaKoduArama = null);

    Task<List<Urun>> GetAllAsync(string? arama = null);
    Task<Urun?> GetByIdAsync(int id);
    Task<ServiceResult> SaveAsync(UrunVM vm);
    Task<ServiceResult> SilAsync(int id);
    Task<Dictionary<int, SonAlisInfoVM>> GetSonAlisBilgileriAsync(List<int> urunIds);
    Task<List<StokHareket>> GetSonStokHareketleriAsync(int urunId, int count);
    Task<List<Urun>> SearchAsync(string query, int limit);
    Task<Urun?> GetByBarkodAsync(string barkod);
    Task<Urun?> GetByParcaKoduAsync(string kod);
    Task<List<Urun>> GetByIdsAsync(List<int> urunIds);
    Task<List<ParcaKodu>> GetParcaKodlariAsync(int urunId);
    Task<ServiceResult> ParcaKoduEkleAsync(int urunId, ParcaKoduVM vm);
    Task<ServiceResult> ParcaKoduGuncelleAsync(int kodId, ParcaKoduVM vm);
    Task<ServiceResult> ParcaKoduSilAsync(int kodId);
}
