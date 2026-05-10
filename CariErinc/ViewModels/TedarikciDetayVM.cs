namespace CariErinc.ViewModels;

public class TedarikciDetayVM
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string? YetkiliKisi { get; set; }
    public string? Telefon { get; set; }
    public string? Adres { get; set; }

    public List<TedarikciAlisOzetVM> SonAlislar { get; set; } = new();

    // İstatistikler
    public int ToplamAlisSayisi { get; set; }
    public decimal ToplamAlisTutari { get; set; }
    public decimal ToplamKalanBorc { get; set; }
    public decimal ToplamOdenen { get; set; }
}

public class TedarikciAlisOzetVM
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; }
    public string OdemeTipi { get; set; } = string.Empty;
    public decimal ToplamTutar { get; set; }
    public decimal KalanBorc { get; set; }
    public DateTime? VadeTarihi { get; set; }
    public int UrunSayisi { get; set; }
}
