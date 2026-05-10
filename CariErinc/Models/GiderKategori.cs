using System.ComponentModel.DataAnnotations;

namespace CariErinc.Models;

public class GiderKategori
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Kategori adı gereklidir")]
    [MaxLength(100)]
    public string Ad { get; set; } = string.Empty;

    public KasaHareketTipi Tip { get; set; }  // Gelir mi, Gider mi?

    public bool SilinebilirMi { get; set; } = true;

    public bool AktifMi { get; set; } = true;
}
