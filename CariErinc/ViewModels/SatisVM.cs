using System.ComponentModel.DataAnnotations;
using CariErinc.Models;

namespace CariErinc.ViewModels;

public class SatisVM
{
    public int Id { get; set; }
 
    [Display(Name = "Müşteri")]
    public int? MusteriId { get; set; }

    [Required(ErrorMessage = "Ödeme tipi seçiniz")]
    [Display(Name = "Ödeme Tipi")]
    public OdemeTipi OdemeTipi { get; set; }

    [Display(Name = "Açıklama")]
    public string? Aciklama { get; set; }

    [Display(Name = "Genel İndirim (%)")]
    [Range(typeof(decimal), "0", "100", ErrorMessage = "İndirim 0-100 arası olmalıdır")]
    public decimal GenelIndirimOrani { get; set; }

    /// <summary>
    /// Kullanıcı ₺ toplam modunda indirim tutarını doğrudan girer.
    /// 0'dan büyükse GenelIndirimOrani yerine bu tutar kullanılır.
    /// </summary>
    public decimal GenelIndirimTutari { get; set; }

    public GenelIndirimModu GenelIndirimModu { get; set; } = GenelIndirimModu.Yuzde;

    /// <summary>ManuelHedefToplam modunda kasiyerin istediği nihai genel toplam.</summary>
    public decimal HedefToplam { get; set; }

    public List<SatisDetaySatirVM> Satirlar { get; set; } = new() { new SatisDetaySatirVM() };
    public int? TaslakId { get; set; }
}

public class SatisDetaySatirVM
{
    public int UrunId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Miktar en az 1 olmalıdır")]
    public int Miktar { get; set; } = 1;

    [Range(0.01, double.MaxValue, ErrorMessage = "Birim fiyat 0'dan büyük olmalıdır")]
    public decimal BirimFiyat { get; set; }

    [Display(Name = "İndirim %")]
    [Range(typeof(decimal), "0", "100", ErrorMessage = "İndirim 0-100 arası olmalıdır")]
    public decimal IndirimOrani { get; set; }

    /// <summary>
    /// Hızlı satışta "Toplam" sütunundan girilen net satır tutarı. Dolu iken sunucu % yerine bunu esas alır.
    /// </summary>
    public decimal? SatirNetTutarHedef { get; set; }

    public int KdvOrani { get; set; } = 0;  // Urun seçilince JS doldurur
}
