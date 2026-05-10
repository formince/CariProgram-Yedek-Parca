using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using CariErinc.Models;

namespace CariErinc.ViewModels;

public class AlisVM
{
    /// <summary>Dolu ise mevcut alış güncellenir.</summary>
    public int? AlisId { get; set; }

    [Required(ErrorMessage = "Cari seçiniz")]
    [Display(Name = "Cari (Tedarikçi)")]
    public int TedarikciId { get; set; }

    [Display(Name = "Açıklama")]
    public string? Aciklama { get; set; }

    [Display(Name = "Ödeme Tipi")]
    public AlisOdemeTipi OdemeTipi { get; set; } = AlisOdemeTipi.Nakit;

    [Display(Name = "Vade Tarihi")]
    [DataType(DataType.Date)]
    public DateTime? VadeTarihi { get; set; }
    [Display(Name = "KDV")]
    public int? KdvOrani { get; set; } 

    public List<AlisDetaySatirVM> Satirlar { get; set; } = new() { new AlisDetaySatirVM() };

    public SelectList? TedarikciListesi { get; set; }
    public SelectList? UrunListesi { get; set; }
}

public class AlisDetaySatirVM
{
    [BindNever]
    [Display(Name = "Ürün")]
    public string? UrunAdiGoster { get; set; }

    public string? Barkod { get; set; }

    [Required(ErrorMessage = "Ürün seçiniz")]
    public int UrunId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Miktar en az 1 olmalıdır")]
    public int Miktar { get; set; } = 1;

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Birim fiyat 0'dan büyük olmalıdır")]
    public decimal BirimFiyat { get; set; }

    public decimal Iskonto1 { get; set; } = 0;
    public decimal Iskonto2 { get; set; } = 0;

    public int KdvOrani { get; set; } = 0;
}
