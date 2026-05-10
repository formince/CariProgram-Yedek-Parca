using System.ComponentModel.DataAnnotations;

namespace CariErinc.ViewModels;

public class CariFormVM
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Cari adı zorunludur.")]
    [MaxLength(200)]
    [Display(Name = "Cari Adı")]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(100)]
    [Display(Name = "Yetkili Kişi")]
    public string? YetkiliKisi { get; set; }

    [Display(Name = "Telefon")]
    public string? Telefon { get; set; }

    [Display(Name = "Adres")]
    public string? Adres { get; set; }

    [Display(Name = "Müşteri")]
    public bool MusteriRol { get; set; }

    [Display(Name = "Tedarikçi")]
    public bool TedarikciRol { get; set; }

    public bool AktifMi { get; set; } = true;
}
