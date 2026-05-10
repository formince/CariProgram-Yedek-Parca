namespace CariErinc.Models;

public class SatisIade
{
    public int Id { get; set; }

    public int SatisId { get; set; }
    public Satis Satis { get; set; } = null!;

    public DateTime IadeTarihi { get; set; } = DateTime.Now;
    public string? Neden { get; set; }

    public ICollection<SatisIadeDetay> IadeDetaylari { get; set; } = new List<SatisIadeDetay>();
}

public class SatisIadeDetay
{
    public int Id { get; set; }

    public int SatisIadeId { get; set; }
    public SatisIade SatisIade { get; set; } = null!;

    public int SatisDetayId { get; set; }
    public SatisDetay SatisDetay { get; set; } = null!;

    public int IadeMiktar { get; set; }
    public decimal IadeTutari { get; set; }
}
