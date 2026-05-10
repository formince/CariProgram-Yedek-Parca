using System.ComponentModel.DataAnnotations;

namespace CariErinc.ViewModels;

public class TedarikciVM
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ad gereklidir")]
    [MaxLength(200)]
    [Display(Name = "Ad")]
    public string Ad { get; set; } = string.Empty;

    [Display(Name = "Yetkili Kişi")]
    public string? YetkiliKisi { get; set; }

    [Display(Name = "Telefon")]
    public string? Telefon { get; set; }

    [Display(Name = "Adres")]
    public string? Adres { get; set; }
}
