namespace CariErinc.Models;

public enum AlisOdemeTipi { Nakit, Vadeli }

public class Alis
{
    public int Id { get; set; }
    public int? CariId { get; set; }
    public Cari? Cari { get; set; }

    public int TedarikciId { get; set; }
    public Tedarikci Tedarikci { get; set; } = null!;

    public decimal ToplamTutar { get; set; }
    
    public AlisOdemeTipi OdemeTipi { get; set; } = AlisOdemeTipi.Nakit;
    public decimal OdenenTutar { get; set; } = 0;
    public decimal KalanBorc { get; set; } = 0;
    public DateTime? VadeTarihi { get; set; }
    public bool OdenmeDurumu_Odendi { get; set; } = false;

    public DateTime Tarih { get; set; } = DateTime.Now;

    public string? FaturaNo { get; set; }
    public string? Aciklama { get; set; }

    public ICollection<AlisDetay> AlisDetaylari { get; set; } = new List<AlisDetay>();
    public ICollection<AlisOdeme> AlisOdemeleri { get; set; } = new List<AlisOdeme>();
}
