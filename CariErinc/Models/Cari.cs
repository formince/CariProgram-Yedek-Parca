using System.ComponentModel.DataAnnotations;

namespace CariErinc.Models;

[Flags]
public enum CariRol
{
    Yok = 0,
    Musteri = 1,
    Tedarikci = 2
}

public class Cari
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? YetkiliKisi { get; set; }

    public string? Telefon { get; set; }
    public string? Adres { get; set; }

    public CariRol Rol { get; set; } = CariRol.Yok;
    public bool AktifMi { get; set; } = true;
    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;
}
