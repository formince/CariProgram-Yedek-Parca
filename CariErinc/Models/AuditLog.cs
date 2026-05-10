using System.ComponentModel.DataAnnotations;

namespace CariErinc.Models;

public class AuditLog
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Tablo { get; set; } = string.Empty;    // "Satis", "Musteri" vb.

    public int KayitId { get; set; }                      // hangi kaydın ID'si

    [Required]
    [MaxLength(50)]
    public string Islem { get; set; } = string.Empty;     // "Eklendi", "Guncellendi", "Silindi"

    public string? EskiDeger { get; set; }                // JSON (sadece Update/Delete)
    public string? YeniDeger { get; set; }                // JSON (sadece Insert/Update)

    [Required]
    [MaxLength(100)]
    public string KullaniciAdi { get; set; } = string.Empty;

    public DateTime Tarih { get; set; } = DateTime.UtcNow;

    public string? Aciklama { get; set; }                 // opsiyonel not
}
