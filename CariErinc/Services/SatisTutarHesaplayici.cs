using CariErinc.Models;

namespace CariErinc.Services;

/// <summary>
/// Satış satırı ve genel indirim — SatisService ile aynı yuvarlama kuralları.
/// </summary>
public static class SatisTutarHesaplayici
{
    /// <param name="satirNetTutarHedef">Doluysa (manuel satır toplamı), net tutar buna göre; indirim tutarı türetilir — % yuvarlama sapması olmaz.</param>
    /// <returns>indirimOraniKayit: veritabanına yazılacak oran (çoğu zaman 6 hane).</returns>
    public static (decimal brutTutar, decimal indirimTutari, decimal netTutar, decimal kdvTutari, decimal indirimOraniKayit) SatirHesapla(
        decimal miktar,
        decimal birimFiyat,
        decimal indirimOrani,
        int kdvOrani,
        decimal? satirNetTutarHedef = null)
    {
        var brutTutar = miktar * birimFiyat;
        decimal indirimTutari;
        decimal netTutar;
        if (satirNetTutarHedef is decimal hedef)
        {
            var brutR = Math.Round(brutTutar, 2);
            netTutar = Math.Clamp(Math.Round(hedef, 2), 0m, brutR);
            indirimTutari = Math.Round(brutTutar - netTutar, 2);
        }
        else
        {
            indirimTutari = Math.Round(brutTutar * indirimOrani / 100m, 2);
            netTutar = Math.Round(brutTutar - indirimTutari, 2);
        }

        var kdvTutari = Math.Round(netTutar * kdvOrani / 100m, 2);
        var indirimOraniKayit = brutTutar > 0m
            ? Math.Round(indirimTutari / brutTutar * 100m, 6)
            : 0m;
        return (brutTutar, indirimTutari, netTutar, kdvTutari, indirimOraniKayit);
    }

    /// <summary>
    /// Yuzde modunda iken istemci sabit tutar gönderdiyse Sabit moda yükseltilir.
    /// </summary>
    public static GenelIndirimModu CozumleGenelIndirimModu(GenelIndirimModu mod, decimal genelIndirimTutari)
    {
        if (mod is GenelIndirimModu.ManuelHedefToplam or GenelIndirimModu.SabitIndirimTutari)
            return mod;
        if (genelIndirimTutari > 0.005m)
            return GenelIndirimModu.SabitIndirimTutari;
        return GenelIndirimModu.Yuzde;
    }

    public static (decimal genelIndirimTutari, decimal genelIndirimOrani) GenelIndirimHesapla(
        decimal araToplam,
        GenelIndirimModu mod,
        decimal genelIndirimOrani,
        decimal hedefToplam,
        decimal clientGenelIndirimTutari)
    {
        araToplam = Math.Round(araToplam, 2);
        mod = CozumleGenelIndirimModu(mod, clientGenelIndirimTutari);

        switch (mod)
        {
            case GenelIndirimModu.ManuelHedefToplam:
            {
                var hedef = Math.Clamp(Math.Round(hedefToplam, 2), 0m, araToplam);
                var tut = Math.Round(araToplam - hedef, 2);
                var pct = araToplam > 0 ? Math.Round(tut / araToplam * 100m, 6) : 0m;
                return (tut, pct);
            }
            case GenelIndirimModu.SabitIndirimTutari:
            {
                var t = Math.Min(Math.Round(clientGenelIndirimTutari, 2), araToplam);
                var p = araToplam > 0 ? Math.Round(t / araToplam * 100m, 6) : 0m;
                return (t, p);
            }
            case GenelIndirimModu.Yuzde:
            default:
            {
                var oran = genelIndirimOrani;
                if (oran < 0) oran = 0;
                if (oran > 100) oran = 100;
                var t3 = Math.Round(araToplam * oran / 100m, 2);
                return (t3, oran);
            }
        }
    }
}
