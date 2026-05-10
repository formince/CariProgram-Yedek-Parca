using System.ComponentModel.DataAnnotations;

namespace CariErinc.ViewModels;

public class MusteriVM
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ad gereklidir")]
    [MaxLength(100)]
    [Display(Name = "Ad")]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(100)]
    [Display(Name = "Soyad")]
    public string Soyad { get; set; } = string.Empty;

    [Display(Name = "Telefon")]
    public string? Telefon { get; set; }

    [Display(Name = "Adres")]
    public string? Adres { get; set; }
}
