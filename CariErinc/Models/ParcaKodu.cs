using System.ComponentModel.DataAnnotations;

namespace CariErinc.Models;

public enum ParcaKoduTipi
{
    OEM = 0,
    Uretici = 1,
    Tedarikci = 2,
    Barkod = 3,
    EskiKod = 4,
    Muadil = 5
}

public class ParcaKodu
{
    public int Id { get; set; }
    public int UrunId { get; set; }
    public Urun Urun { get; set; } = null!;
    public ParcaKoduTipi KodTipi { get; set; }

    [Required]
    [MaxLength(100)]
    public string Kod { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Aciklama { get; set; }

    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;
}
