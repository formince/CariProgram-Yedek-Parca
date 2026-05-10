using CariErinc.ViewModels;

namespace CariErinc.Services.Interfaces;

public interface IRaporService
{
    Task<GunlukSatisRaporVM> GetGunlukSatisAsync(DateTime? tarih = null);
    Task<AylikRaporVM> GetAylikRaporAsync(int? yil = null, int? ay = null);
    Task<StokUyariRaporVM> GetStokUyariAsync();
    Task<VeresiyeRaporVM> GetVeresiyeRaporAsync();
    Task<KarZararRaporVM> GetKarZararAsync(DateTime baslangic, DateTime bitis);
}
