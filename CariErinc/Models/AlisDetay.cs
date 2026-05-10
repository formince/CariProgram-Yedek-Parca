namespace CariErinc.Models;

public class AlisDetay
{
    public int Id { get; set; }

    public int AlisId { get; set; }
    public Alis Alis { get; set; } = null!;

    public int UrunId { get; set; }
    public Urun Urun { get; set; } = null!;

    public int Miktar { get; set; }

    public decimal BirimFiyat { get; set; }
    public decimal Iskonto1 { get; set; } = 0;
    public decimal Iskonto2 { get; set; } = 0;

    public int KdvOrani { get; set; } = 0;
    public decimal KdvTutari { get; set; } = 0;
}
