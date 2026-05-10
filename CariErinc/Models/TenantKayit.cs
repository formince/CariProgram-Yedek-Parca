using System.ComponentModel.DataAnnotations;

namespace CariErinc.Models;

public class TenantKayit
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Subdomain { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DukkanAdi { get; set; } = string.Empty;

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;

    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;
}
