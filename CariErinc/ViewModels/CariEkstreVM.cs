namespace CariErinc.ViewModels;

public class CariEkstreVM
{
    public int CariId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string RolEtiketi { get; set; } = "-";
    public string? Telefon { get; set; }
    public string? Adres { get; set; }

    public decimal AlacakToplam { get; set; }
    public decimal VerecekToplam { get; set; }
    public decimal NetBakiye => AlacakToplam - VerecekToplam;

    public string? Baslangic { get; set; }
    public string? Bitis { get; set; }

    public List<CariEkstreSatirVM> Satirlar { get; set; } = new();

    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 30;
}

public class CariEkstreSatirVM
{
    public DateTime Tarih { get; set; }
    /// <summary>Alacak / Tahsilat / Verecek / Ödeme / Satış</summary>
    public string Taraf { get; set; } = string.Empty;
    public string IslemTipi { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public string? Aciklama { get; set; }

    /// <summary>Satis, Veresiye, VeresiyeOdeme, Alis, AlisOdeme</summary>
    public string Kaynak { get; set; } = string.Empty;
    public int KaynakId { get; set; }

    /// <summary>VeresiyeOdeme satırlarında Veresiye/Detail için.</summary>
    public int? BagliVeresiyeId { get; set; }

    /// <summary>AlisOdeme satırlarında Alis/Detail veya Odeme için.</summary>
    public int? BagliAlisId { get; set; }
}
