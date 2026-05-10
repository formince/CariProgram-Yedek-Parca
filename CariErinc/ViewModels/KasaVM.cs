using System.ComponentModel.DataAnnotations;
using CariErinc.Models;

namespace CariErinc.ViewModels;

public class KasaVM
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Hareket türü seçiniz")]
    [Display(Name = "Hareket Türü")]
    public KasaHareketTipi HareketTipi { get; set; }

    [Required(ErrorMessage = "Kategori seçiniz")]
    [Display(Name = "Kategori")]
    public int GiderKategoriId { get; set; }

    public string? Kategori { get; set; }

    public Microsoft.AspNetCore.Mvc.Rendering.SelectList? KategoriListesi { get; set; }

    [Required(ErrorMessage = "Tutar gereklidir")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalıdır")]
    [Display(Name = "Tutar")]
    public decimal Tutar { get; set; }

    [Display(Name = "Açıklama")]
    public string? Aciklama { get; set; }

    [Display(Name = "Tarih")]
    [DataType(DataType.Date)]
    public DateTime Tarih { get; set; } = DateTime.Today;
}
