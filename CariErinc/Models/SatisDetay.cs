namespace CariErinc.Models;

public class SatisDetay
{
    public int Id { get; set; }

    public int SatisId { get; set; }
    public Satis Satis { get; set; } = null!;

    public int UrunId { get; set; }
    public Urun Urun { get; set; } = null!;

    public int Miktar { get; set; }

    public decimal BirimFiyat { get; set; }

    public int KdvOrani { get; set; } = 0;       // satış anındaki KDV oranı
    public decimal KdvTutari { get; set; } = 0;  // hesaplanan KDV tutarı

    public decimal IndirimOrani { get; set; } = 0;
    public decimal IndirimTutari { get; set; } = 0;
    public decimal NetTutar { get; set; } = 0;

    public decimal AlisBirimFiyati { get; set; } = 0; // Satış anındaki alış maliyeti (kâr hesabı için)

    public ICollection<SatisIadeDetay> SatisIadeDetaylari { get; set; } = new List<SatisIadeDetay>();
}
