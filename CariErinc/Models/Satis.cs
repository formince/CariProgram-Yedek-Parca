namespace CariErinc.Models;

public enum OdemeTipi { Pesin, Veresiye }
public enum SatisDurum { Taslak, Tamamlandi, Iptal }

public class Satis
{
    public int Id { get; set; }
    public int? CariId { get; set; }
    public Cari? Cari { get; set; }

    public int? MusteriId { get; set; }
    public Musteri? Musteri { get; set; }

    public DateTime Tarih { get; set; } = DateTime.Now;

    public decimal ToplamTutar { get; set; }

    public decimal GenelIndirimOrani { get; set; } = 0;
    public decimal GenelIndirimTutari { get; set; } = 0;

    public GenelIndirimModu GenelIndirimHesapModu { get; set; } = GenelIndirimModu.Yuzde;

    /// <summary>ManuelHedefToplam modunda hedeflenen genel toplam; aksi halde 0.</summary>
    public decimal GenelIndirimHedefToplam { get; set; }

    public decimal IndirimSonrasiToplam { get; set; } = 0;

    public OdemeTipi OdemeTipi { get; set; }

    public Veresiye? Veresiye { get; set; }

    public string? Aciklama { get; set; }
    public bool IptalEdildi { get; set; } = false;
    public DateTime? IptalTarihi { get; set; }
    public string? IptalNedeni { get; set; }
    public bool KismiIade { get; set; } = false;
    public SatisDurum Durum { get; set; } = SatisDurum.Tamamlandi;

    public ICollection<SatisDetay> SatisDetaylari { get; set; } = new List<SatisDetay>();
    public ICollection<SatisIade> SatisIadeler { get; set; } = new List<SatisIade>();
}
