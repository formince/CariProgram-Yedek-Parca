using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CariErinc.Models;

public class UrunFiyatAudit
{
    public int Id { get; set; }

    [Required]
    public int UrunId { get; set; }

    [ForeignKey(nameof(UrunId))]
    public Urun? Urun { get; set; }

    /// <summary>Değişim öncesi fiyat (KDV hariç)</summary>
    public decimal EskiFiyat { get; set; }

    /// <summary>Yeni alış maliyeti fiyatı (KDV hariç)</summary>
    public decimal YeniFiyat { get; set; }

    /// <summary>Değişim sebebi: OtomatikFatura veya ManuelDuzenleme</summary>
    [Required]
    [MaxLength(50)]
    public string Neden { get; set; } = string.Empty;

    /// <summary>Değişim yapan kullanıcı</summary>
    [Required]
    [MaxLength(100)]
    public string KullaniciAdi { get; set; } = string.Empty;

    /// <summary>Hangi alış fişinden geldiyse</summary>
    public int? AlisId { get; set; }

    /// <summary>Değişim tarihi</summary>
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
}

public enum FiyatDegisimNedeni
{
    OtomatikFatura,
    ManuelDuzenleme
}
