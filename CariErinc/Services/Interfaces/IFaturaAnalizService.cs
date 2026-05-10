using CariErinc.ViewModels;
using Microsoft.AspNetCore.Http;

namespace CariErinc.Services.Interfaces;

public interface IFaturaAnalizService
{
    /// <summary>
    /// Yüklenen faturayı (XML veya Resim) analiz eder ve verileri VM olarak döner.
    /// </summary>
    Task<FaturaAnalizSonucVM> AnalizEtAsync(IFormFile dosya);

    /// <summary>
    /// Ham satır verilerini sistemdeki mevcut ürünlerle eşleştirir.
    /// </summary>
    Task<List<FaturaSatirAnalizVM>> UrunleriEsletAsync(List<FaturaSatirAnalizVM> satirlar, int tedarikciId);
}
