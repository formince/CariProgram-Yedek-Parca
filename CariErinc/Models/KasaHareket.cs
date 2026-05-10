namespace CariErinc.Models;

public enum KasaHareketTipi { Gelir, Gider }

public class KasaHareket
{
    public int Id { get; set; }

    public KasaHareketTipi HareketTipi { get; set; }

    public string Kategori { get; set; } = string.Empty;

    public int? GiderKategoriId { get; set; }
    public GiderKategori? GiderKategori { get; set; }

    public decimal Tutar { get; set; }

    public string? Aciklama { get; set; }

    public DateTime Tarih { get; set; } = DateTime.Now;
}
