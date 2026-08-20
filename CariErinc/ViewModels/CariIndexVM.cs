namespace CariErinc.ViewModels;

public class CariIndexVM
{
    public string? Arama { get; set; }
    public List<CariSatirVM> Satirlar { get; set; } = new();
    public CariDogrulamaVM Dogrulama { get; set; } = new();

    public decimal ToplamAlacak => Satirlar.Sum(x => x.Alacak);
    public decimal ToplamVerecek => Satirlar.Sum(x => x.Verecek);
    public decimal ToplamAvans => Satirlar.Sum(x => x.Avans);
    public decimal NetBakiye => (ToplamAlacak + ToplamAvans) - ToplamVerecek;
}

public class CariSatirVM
{
    public int CariId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string? Telefon { get; set; }
    public decimal Alacak { get; set; }
    public decimal Verecek { get; set; }
    public decimal Avans { get; set; }
    public string RolEtiketi { get; set; } = string.Empty;
}

public class CariDogrulamaVM
{
    public decimal CariToplamAlacak { get; set; }
    public decimal CariToplamVerecek { get; set; }
    public decimal CariToplamAvans { get; set; }
    public decimal AcikVeresiyeToplam { get; set; }
    public decimal AcikVadeliAlisToplam { get; set; }
    public decimal AcikAvansToplam { get; set; }
    public decimal KasaBakiye { get; set; }

    public bool AlacakTutarlimi => CariToplamAlacak == AcikVeresiyeToplam;
    public bool VerecekTutarlimi => CariToplamVerecek == AcikVadeliAlisToplam;
    public bool AvansTutarlimi => CariToplamAvans == AcikAvansToplam;
}
