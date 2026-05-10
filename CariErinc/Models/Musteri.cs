using System.ComponentModel.DataAnnotations;

namespace CariErinc.Models;

public class Musteri
{
    public int Id { get; set; }
    public int? CariId { get; set; }
    public Cari? Cari { get; set; }

    [Required]
    [MaxLength(100)]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Soyad { get; set; } = string.Empty;

    public string? Telefon { get; set; }

    public string? Adres { get; set; }

    public DateTime OlusturulmaTarihi { get; set; } = DateTime.Now;

    public ICollection<Veresiye> Veresiyeler { get; set; } = new List<Veresiye>();
}
