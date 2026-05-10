namespace CariErinc.ViewModels;

public enum HaraketTip { Veresiye = 0, Satis = 1 }

public class HareketVM
{
    public HaraketTip Tip { get; set; }
    public int Id { get; set; }
    public DateTime Tarih { get; set; }

    // Veresiye alanları
    public decimal? Tutar { get; set; }
    public decimal? KalanBorc { get; set; }
    public string? Durum { get; set; }
    public string? VeresiyeTip { get; set; } // "SatisBagli" veya "Elden"
    public int? SatisId { get; set; }

    // Satış alanları
    public decimal? SatisTutar { get; set; }
    public string? OdemeTipi { get; set; }
    public bool? IptalEdildi { get; set; }

    // Ödeme geçmişi (veresiye için)
    public List<VeresiyeOdemeBilgiVM>? Odemeler { get; set; }
}

public class VeresiyeOdemeBilgiVM
{
    public decimal Tutar { get; set; }
    public DateTime Tarih { get; set; }
    public string? KullaniciId { get; set; }
    public string OdemeTipi { get; set; } = "Nakit";
}

public class MusteriDetayVM
{
    public int Id { get; set; }
    public string AdSoyad { get; set; } = string.Empty;
    public string? Telefon { get; set; }
    public string? Adres { get; set; }
    public decimal ToplamBorc { get; set; }

    // Son satışlar (tüm tipte)
    public List<MusteriSatisOzetVM> SonSatislar { get; set; } = new();

    // Açık veresiyeler
    public List<MusteriVeresiyeOzetVM> AcikVeresiyeler { get; set; } = new();

    // Birleşik hareket listesi (tek tablo için)
    public List<HareketVM> Hareketler { get; set; } = new();

    // İstatistikler
    public int ToplamSatisSayisi { get; set; }
    public decimal ToplamSatisTutari { get; set; }
    public decimal ToplamOdenenVeresiye { get; set; }
}

public class MusteriSatisOzetVM
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; }
    public string OdemeTipi { get; set; } = string.Empty;
    public decimal ToplamTutar { get; set; }
    public bool IptalEdildi { get; set; }
    public int UrunSayisi { get; set; }
}

public class MusteriVeresiyeOzetVM
{
    public int Id { get; set; }
    public DateTime Tarih { get; set; }
    public decimal Tutar { get; set; }
    public decimal OdenenTutar { get; set; }
    public decimal KalanBorc { get; set; }
    public string Durum { get; set; } = string.Empty;
    public int? SatisId { get; set; }
    public string Tip { get; set; } = string.Empty; // "SatisBagli" veya "Elden"
}
