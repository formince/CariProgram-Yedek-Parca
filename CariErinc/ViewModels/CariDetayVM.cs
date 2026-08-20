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
    public decimal AvansToplam { get; set; }
    public decimal NetBakiye => (AlacakToplam + AvansToplam) - VerecekToplam;

    // Tahsilat özeti — kümülatif (tüm zamanlar)
    public decimal ToplamBorcOdemesi { get; set; }   // VeresiyeOdeme (Veresiye.Tip != Avans).Sum
    public decimal ToplamAvansYatirma { get; set; } // Veresiye (Tip == Avans).Sum(Tutar)
    public decimal ToplamAvansKullanimi { get; set; } // VeresiyeOdeme (Veresiye.Tip == Avans).Sum
    public decimal ToplamMusteriYatirilan => ToplamBorcOdemesi + ToplamAvansYatirma;

    // Tarih filtresi
    public DateTime? Baslangic { get; set; }
    public DateTime? Bitis { get; set; }
    public string TarihFiltreAraligi => (Baslangic, Bitis) switch
    {
        (null, null) => "Tüm zamanlar",
        (DateTime b, null) => $"{b.ToString("dd.MM.yyyy")} ve sonrası",
        (null, DateTime e) => $"{e.ToString("dd.MM.yyyy")} ve öncesi",
        (DateTime b, DateTime e) when b.Date == e.Date => b.ToString("dd.MM.yyyy"),
        (DateTime b, DateTime e) => $"{b.ToString("dd.MM.yyyy")} - {e.ToString("dd.MM.yyyy")}"
    };

    public int ToplamSatisSayisi { get; set; }
    public decimal ToplamSatisTutari { get; set; }
    public int ToplamAlisSayisi { get; set; }
    public decimal ToplamAlisTutari { get; set; }
    public decimal ToplamOdenenVeresiye { get; set; }
    public decimal ToplamOdenenAlis { get; set; }

    // Yeni: tüm veresiyeleri tip'e göre ayrılmış
    public List<CariVeresiyeSatirVM> Borclar { get; set; } = new();        // Tip != Avans
    public List<CariVeresiyeSatirVM> Avanslar { get; set; } = new();       // Tip == Avans
    public List<CariOdemeSatirVM> Odemeler { get; set; } = new();          // Tüm VeresiyeOdeme kayıtları

    // Eski alanlar (geriye uyumluluk — şimdilik Borclar ile aynı veriyi taşır)
    public List<CariVeresiyeSatirVM> AcikVeresiyeler => Borclar;
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
    public string Tip { get; set; } = "Borc";
}

public class CariOdemeSatirVM
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; }
    public decimal Tutar { get; set; }
    public string? KullaniciId { get; set; }
    public string OdemeTipi { get; set; } = "Nakit";
    public string? Aciklama { get; set; }
    public int? VeresiyeId { get; set; }
    public string VeresiyeTip { get; set; } = "Borc";
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
