using CariErinc.Models;
using CariErinc.ViewModels;
using CariErinc.Helpers;

namespace CariErinc.Services.Interfaces;

public interface IStokService
{
    Task<StokHareketIndexVM> GetPagedListAsync(int page = 1, int? urunId = null, DateTime? baslangic = null, DateTime? bitis = null);
    Task<List<StokHareket>> GetAllAsync(int? urunId, DateTime? baslangic, DateTime? bitis);
    Task<ServiceResult> SaveAsync(StokHareketVM vm);
    Task<ServiceResult> SilAsync(int id);
    void StokCikisYap(Urun urun, int miktar, string islemAciklamasi);
    void StokGirisYap(Urun urun, int miktar, string islemAciklamasi);
    Task<StokHareketVM?> GetHareketVmAsync(int id);
}
