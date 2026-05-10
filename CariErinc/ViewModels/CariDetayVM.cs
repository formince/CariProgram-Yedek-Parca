namespace CariErinc.ViewModels;

public class CariDetayVM
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string? Telefon { get; set; }
    public string? Adres { get; set; }
    public string RolEtiketi { get; set; } = "-";

    public int? MusteriId { get; set; }
    public int? TedarikciId { get; set; }
    public bool MusteriRoluVar => MusteriId.HasValue;
    public bool TedarikciRoluVar => TedarikciId.HasValue;

    public decimal AlacakToplam { get; set; }
    public decimal VerecekToplam { get; set; }
    public decimal NetBakiye => AlacakToplam - VerecekToplam;

    public int ToplamSatisSayisi { get; set; }
    public decimal ToplamSatisTutari { get; set; }
    public int ToplamAlisSayisi { get; set; }
    public decimal ToplamAlisTutari { get; set; }
    public decimal ToplamOdenenVeresiye { get; set; }
    public decimal ToplamOdenenAlis { get; set; }

    public List<CariVeresiyeSatirVM> AcikVeresiyeler { get; set; } = new();
    public List<CariAlisSatirVM> AcikVadeliAlislar { get; set; } = new();
    public List<CariHareketSatirVM> Hareketler { get; set; } = new();
}

public class CariVeresiyeSatirVM
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; }
    public decimal Tutar { get; set; }
    public decimal OdenenTutar { get; set; }
    public decimal KalanBorc { get; set; }
    public string Durum { get; set; } = string.Empty;
}

public class CariAlisSatirVM
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; }
    public decimal ToplamTutar { get; set; }
    public decimal OdenenTutar { get; set; }
    public decimal KalanBorc { get; set; }
    public string OdemeTipi { get; set; } = string.Empty;
}

public class CariHareketSatirVM
{
    public string Tip { get; set; } = string.Empty;
    public int Id { get; set; }
    public DateTime Tarih { get; set; }
    public decimal Tutar { get; set; }
    public decimal Kalan { get; set; }
    public string Durum { get; set; } = string.Empty;
}
