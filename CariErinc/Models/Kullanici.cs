using System.ComponentModel.DataAnnotations;

namespace CariErinc.Models;

public class Kullanici
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string KullaniciAdi { get; set; } = string.Empty;

    [Required]
    public string SifreHash { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;

    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;

    public ICollection<KullaniciRol> KullaniciRoller { get; set; } = new List<KullaniciRol>();
}
