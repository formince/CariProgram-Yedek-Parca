namespace CariErinc.Models;

/// <summary>Kullanıcı ↔ Rol M:N bağlantı tablosu.</summary>
public class KullaniciRol
{
    public int KullaniciId { get; set; }
    public Kullanici Kullanici { get; set; } = null!;

    public int RolId { get; set; }
    public Rol Rol { get; set; } = null!;
}
