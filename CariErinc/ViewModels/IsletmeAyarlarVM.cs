using System.ComponentModel.DataAnnotations;

namespace CariErinc.ViewModels;

public class IsletmeAyarlarVM
{
    [Display(Name = "Dükkan / İşletme Adı")]
    [Required]
    [MaxLength(100)]
    public string DukkanAdi { get; set; } = string.Empty;

    [Display(Name = "İşletme Tipi")]
    [MaxLength(50)]
    public string IsletmeTipi { get; set; } = string.Empty;

    [Display(Name = "Adres")]
    [MaxLength(300)]
    public string Adres { get; set; } = string.Empty;

    [Display(Name = "Telefon No")]
    public string? Telefon { get; set; }

    [Display(Name = "Varsayılan KDV Oranı (%)")]
    [Range(0, 100, ErrorMessage = "KDV oranı 0-100 arasında olmalıdır.")]
    public int VarsayilanKdv { get; set; } = 20;

    [Display(Name = "Ürünlerde seçilebilir KDV oranları (%)")]
    public string KdvOranlariMetni { get; set; } = "0,1,8,10,20";
}
