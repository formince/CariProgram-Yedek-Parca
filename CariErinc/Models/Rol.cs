using System.ComponentModel.DataAnnotations;

namespace CariErinc.Models;

public class Rol
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Aciklama { get; set; }

    /// <summary>true olan roller tüm middleware kontrollerini bypass eder.</summary>
    public bool IsAdmin { get; set; }

    public ICollection<KullaniciRol> KullaniciRoller { get; set; } = new List<KullaniciRol>();
    public ICollection<RolYetki> Yetkiler { get; set; } = new List<RolYetki>();
}
