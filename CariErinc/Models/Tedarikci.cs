using System.ComponentModel.DataAnnotations;

namespace CariErinc.Models;

public class Tedarikci
{
    public int Id { get; set; }
    public int? CariId { get; set; }
    public Cari? Cari { get; set; }

    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    public string? YetkiliKisi { get; set; }

    public string? Telefon { get; set; }

    public string? Adres { get; set; }

    public DateTime OlusturulmaTarihi { get; set; } = DateTime.Now;

    public ICollection<Urun> Urunler { get; set; } = new List<Urun>();
    public ICollection<Alis> Alislar { get; set; } = new List<Alis>();
}
