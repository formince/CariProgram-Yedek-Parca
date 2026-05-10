using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CariErinc.ViewModels;

public class KullaniciYonetimVM
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
    [MaxLength(50)]
    [Display(Name = "Kullanıcı Adı")]
    public string KullaniciAdi { get; set; } = string.Empty;

    [Display(Name = "Şifre")]
    [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
    public string? Sifre { get; set; }

    [Display(Name = "Aktif")]
    public bool AktifMi { get; set; } = true;

    [Display(Name = "Roller")]
    public List<int> SeciliRolIds { get; set; } = new();

    public List<SelectListItem> RolListesi { get; set; } = new();
}

public class KullaniciIndexVM
{
    public int Id { get; set; }
    public string KullaniciAdi { get; set; } = string.Empty;
    public bool AktifMi { get; set; }
    public List<string> Roller { get; set; } = new();
    public DateTime OlusturulmaTarihi { get; set; }
}
