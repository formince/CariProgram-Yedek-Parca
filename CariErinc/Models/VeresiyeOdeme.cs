namespace CariErinc.Models;

public enum VeresiyeOdemeTipi { Nakit = 0, Hesap = 1 }

public class VeresiyeOdeme
{
    public int Id { get; set; }

    public int VeresiyeId { get; set; }
    public Veresiye Veresiye { get; set; } = null!;

    public decimal OdemeTutari { get; set; }

    public DateTime OdemeTarihi { get; set; } = DateTime.Now;

    public string? Aciklama { get; set; }

    /// <summary>Ödemeyi alan kullanıcı</summary>
    public string? KullaniciId { get; set; }

    /// <summary>Ödeme tipi</summary>
    public VeresiyeOdemeTipi OdemeTipi { get; set; } = VeresiyeOdemeTipi.Nakit;
}
