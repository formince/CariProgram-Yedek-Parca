using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CariErinc.Models;

public class FaturaEslesme
{
    public int Id { get; set; }

    public int TedarikciId { get; set; }
    public Tedarikci Tedarikci { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string FaturaUrunAdi { get; set; } = string.Empty; // Faturada yazan ham metin

    public int SistemUrunId { get; set; }
    public Urun SistemUrun { get; set; } = null!; // Bizim sistemdeki karşılığı

    public int KullaniciId { get; set; }
    public Kullanici Kullanici { get; set; } = null!; // Kim eşleştirdi

    public int EslesmeSkoru { get; set; } // 0-100 arası AI/Benzerlik skoru
    public bool ManuelMi { get; set; } // Kullanıcı elle mi düzeltti?

    public DateTime KayitTarihi { get; set; } = DateTime.UtcNow;
}
