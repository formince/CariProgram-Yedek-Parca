using CariErinc.Models;

namespace CariErinc.Helpers;

public static class AlisBorcHesaplayici
{
    public static decimal CalculateKalanBorc(Alis alis)
    {
        if (alis.OdemeTipi != AlisOdemeTipi.Vadeli)
            return 0;

        return Math.Max(0, alis.ToplamTutar - alis.OdenenTutar);
    }

    public static bool IsTamOdendi(Alis alis)
    {
        return CalculateKalanBorc(alis) == 0;
    }

    public static void RecalculateDurum(Alis alis)
    {
        alis.KalanBorc = CalculateKalanBorc(alis);
        alis.OdenmeDurumu_Odendi = IsTamOdendi(alis);
    }

    public static void SetInitialDurum(Alis alis)
    {
        if (alis.OdemeTipi == AlisOdemeTipi.Nakit)
            alis.OdenenTutar = alis.ToplamTutar;
        else
            alis.OdenenTutar = 0;

        RecalculateDurum(alis);
    }
}
