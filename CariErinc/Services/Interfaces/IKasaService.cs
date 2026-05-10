using CariErinc.Models;
using CariErinc.ViewModels;
using CariErinc.Helpers;

namespace CariErinc.Services.Interfaces;

public interface IKasaService
{
    Task<KasaIndexVM> GetKasaVerileriAsync(DateTime? baslangic, DateTime? bitis, int page = 1, string? search = null, int? kategoriId = null, KasaHareketTipi? tip = null);
    Task<ServiceResult> SaveAsync(KasaVM vm);
    Task<ServiceResult> SilAsync(int id);
    void KasaGelirEkle(decimal tutar, string kategori, string aciklama);
    void KasaGiderCik(decimal tutar, string kategori, string aciklama, int? giderKategoriId = null);
}
