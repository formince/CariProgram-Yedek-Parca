namespace CariErinc.Models;

/// <summary>
/// Genel indirimin nasıl hesaplanacağı. Hızlı satış ve taslak için saklanır.
/// </summary>
public enum GenelIndirimModu : byte
{
    /// <summary>Satır ara toplamına yüzde uygulanır.</summary>
    Yuzde = 0,

    /// <summary>Kasiyer nihai genel toplamı girer; indirim sunucuda ara − hedef olarak türetilir.</summary>
    ManuelHedefToplam = 1,

    /// <summary>İstemciden gelen sabit indirim tutarı (klasik formlar / geriye dönük).</summary>
    SabitIndirimTutari = 2
}
