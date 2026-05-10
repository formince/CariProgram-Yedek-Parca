using System.ComponentModel.DataAnnotations;
using CariErinc.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CariErinc.ViewModels;

public class StokHareketVM
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ürün seçiniz")]
    [Display(Name = "Ürün")]
    public int UrunId { get; set; }

    [Required(ErrorMessage = "Hareket tipi seçiniz")]
    [Display(Name = "Hareket Tipi")]
    public HareketTipi HareketTipi { get; set; }

    [Required(ErrorMessage = "Miktar gereklidir")]
    [Range(0, int.MaxValue, ErrorMessage = "Miktar 0 veya pozitif olmalıdır")]
    [Display(Name = "Miktar")]
    public int Miktar { get; set; }

    [Display(Name = "Açıklama")]
    public string? Aciklama { get; set; }

    /// <summary>Yeni kayıtta boş; düzenlemede zorunlu. Kayıtta UTC.</summary>
    [Display(Name = "Tarih")]
    public DateTime? Tarih { get; set; }

    public SelectList? UrunListesi { get; set; }
}
