using CariErinc.Models;

namespace CariErinc.ViewModels;

public class GunlukSatisRaporVM
{
    public DateTime Tarih { get; set; }
    public List<KasaHareket> KasaHareketler { get; set; } = new();
    public decimal ToplamGelir { get; set; }
    public decimal ToplamGider { get; set; }
    public decimal NetKar { get; set; }
}

public class GunlukOzetVM
{
    public DateTime Tarih { get; set; }
    public decimal Gelir { get; set; }
    public decimal Gider { get; set; }
}

public class AylikRaporVM
{
    public int Yil { get; set; }
    public int Ay { get; set; }
    public decimal AylikGelir { get; set; }
    public decimal AylikGider { get; set; }
    public decimal NetBakiye { get; set; }
    public List<GunlukOzetVM> GunlukOzetler { get; set; } = new();
}

public class StokUyariRaporVM
{
    public List<Urun> KritikUrunler { get; set; } = new();
}

public class VeresiyeRaporVM
{
    public List<Veresiye> AcikVeresiyeler { get; set; } = new();
    public decimal ToplamAcikBorc { get; set; }
}
