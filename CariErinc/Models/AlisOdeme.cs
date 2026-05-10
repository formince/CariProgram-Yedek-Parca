namespace CariErinc.Models;

public class AlisOdeme
{
    public int Id { get; set; }
    public int AlisId { get; set; }
    public Alis Alis { get; set; } = null!;
    public decimal OdemeTutari { get; set; }
    public DateTime OdemeTarihi { get; set; } = DateTime.Now;
    public string? Aciklama { get; set; }
}
