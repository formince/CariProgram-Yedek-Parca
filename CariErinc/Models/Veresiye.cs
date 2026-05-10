namespace CariErinc.Models;

public enum OdenmeDurumu { Bekliyor, KismiOdendi, Odendi, Iptal }

public enum VeresiyeTipi { SatisBagli = 0, Elden = 1 }

public class Veresiye
{
    public int Id { get; set; }
    public int? CariId { get; set; }
    public Cari? Cari { get; set; }

    public int MusteriId { get; set; }
    public Musteri Musteri { get; set; } = null!;

    public int? SatisId { get; set; }
    public Satis? Satis { get; set; }

    public decimal Tutar { get; set; }

    public string? Aciklama { get; set; }

    public DateTime Tarih { get; set; } = DateTime.Now;

    public OdenmeDurumu OdenmeDurumu { get; set; } = OdenmeDurumu.Bekliyor;

    /// <summary>Satış ile ilgili değil, elden verilen borç.</summary>
    public VeresiyeTipi Tip { get; set; } = VeresiyeTipi.SatisBagli;

    public ICollection<VeresiyeOdeme> Odemeler { get; set; } = new List<VeresiyeOdeme>();
}
