namespace CariErinc.Models;

public enum HareketTipi { Giris, Cikis, Iade, Sayim }

public class StokHareket
{
    public int Id { get; set; }

    public int UrunId { get; set; }
    public Urun Urun { get; set; } = null!;

    public HareketTipi HareketTipi { get; set; }

    public int Miktar { get; set; }

    public string? Aciklama { get; set; }

    public DateTime Tarih { get; set; } = DateTime.Now;
}
