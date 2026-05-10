namespace CariErinc.ViewModels;

public class SatisIptalVM
{
    public int SatisId { get; set; }
    public string? Neden { get; set; }
}

public class SatisIadeVM
{
    public int SatisId { get; set; }
    public string? Neden { get; set; }
    public List<SatisIadeDetaySatirVM> Satirlar { get; set; } = new();
}

public class SatisIadeDetaySatirVM
{
    public int SatisDetayId { get; set; }
    public string UrunAd { get; set; } = "";
    public int SatilanMiktar { get; set; }
    public int OncekiIadeMiktar { get; set; }
    public int IadeMiktar { get; set; }
    public decimal BirimFiyat { get; set; }
}
