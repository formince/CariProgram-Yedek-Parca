using System.ComponentModel.DataAnnotations;

namespace CariErinc.ViewModels;

public class UrunVM
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ürün adı gereklidir")]
    [MaxLength(200)]
    [Display(Name = "Ad")]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(50)]
    [Display(Name = "Barkod")]
    public string? Barkod { get; set; }

    [MaxLength(100)]
    [Display(Name = "Marka")]
    public string? Kategori { get; set; }

    [Required(ErrorMessage = "Birim fiyat gereklidir")]
    [Range(0, double.MaxValue, ErrorMessage = "Birim fiyat 0'dan küçük olamaz")]
    [Display(Name = "Birim Fiyat")]
    public decimal BirimFiyat { get; set; }

    [Display(Name = "KDV Oranı (%)")]
    public int KdvOrani { get; set; }

    [Display(Name = "Alış Fiyatı (Son)")]
    public decimal AlisFiyati { get; set; }

    [Required(ErrorMessage = "Stok adedi gereklidir")]
    [Range(0, int.MaxValue, ErrorMessage = "Stok adedi 0'dan küçük olamaz")]
    [Display(Name = "Stok Adedi")]
    public int StokAdedi { get; set; }

    [Display(Name = "Min Stok Uyarı")]
    public int MinStokUyari { get; set; } = 5;

    [Display(Name = "Tedarikçi")]
    public int? TedarikciId { get; set; }
}
