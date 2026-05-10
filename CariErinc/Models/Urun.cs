using System.ComponentModel.DataAnnotations;

namespace CariErinc.Models;

public class Urun
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Barkod { get; set; }

    [MaxLength(100)]
    public string Kategori { get; set; } = string.Empty;

    public decimal BirimFiyat { get; set; }

    public int KdvOrani { get; set; } = 18; // %0, %1, %8, %18

    /// <summary>Son alıştan gelen birim maliyet: satır iskontoları sonrası, KDV hariç (satış neti ile aynı matrah).</summary>
    public decimal AlisFiyati { get; set; } = 0;
    public DateTime? SonAlisTarihi { get; set; }

    public int StokAdedi { get; set; } = 0;

    public int MinStokUyari { get; set; } = 5;

    public int? TedarikciId { get; set; }
    public Tedarikci? Tedarikci { get; set; }

    public int? CariId { get; set; }
    public Cari? Cari { get; set; }

    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;
    public DateTime GuncellenmeTarihi { get; set; }

    public void StokGiris(int miktar)
    {
        if (miktar <= 0) return;
        this.StokAdedi += miktar;
        this.GuncellenmeTarihi = DateTime.UtcNow;
    }

    public void StokCikis(int miktar)
    {
        if (miktar <= 0) return;
        this.StokAdedi -= miktar;
        this.GuncellenmeTarihi = DateTime.UtcNow;
    }
}
