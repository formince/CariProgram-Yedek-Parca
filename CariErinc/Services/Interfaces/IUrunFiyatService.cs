using CariErinc.Models;
using CariErinc.ViewModels;
using CariErinc.Helpers;

namespace CariErinc.Services.Interfaces;

public interface IUrunFiyatService
{
    /// <summary>
    /// Ürünün alış fiyatını güncelle ve audit trail oluştur
    /// </summary>
    /// <param name="urunId">Ürün ID</param>
    /// <param name="yeniFiyat">Yeni maliyet fiyatı (KDV hariç)</param>
    /// <param name="neden">Değişim sebebi (OtomatikFatura, ManuelDuzenleme vb)</param>
    /// <param name="kullanici">Değişim yapan kullanıcı</param>
    /// <param name="alisId">İlişkili alış fiş ID (isteğe bağlı)</param>
    /// <returns>Gerçek değişim olup olmadığı + eski/yeni fiyat bilgilerini içeren ServiceResult</returns>
    Task<ServiceResult<UrunFiyatUpdateResult>> UpdateAlisFiyatiAsync(
        int urunId,
        decimal yeniFiyat,
        string neden,
        string kullanici,
        int? alisId = null
    );

    /// <summary>
    /// Ürünün fiyat değişim geçmişini getir
    /// </summary>
    Task<List<UrunFiyatAudit>> GetFiyatGecmisiAsync(int urunId);

    /// <summary>
    /// Son fiyat değişim kaydını getir
    /// </summary>
    Task<UrunFiyatAudit?> GetSonFiyatDegisimAsync(int urunId);
}
