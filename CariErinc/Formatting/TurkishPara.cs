using System.Globalization;

namespace CariErinc.Formatting;

/// <summary>Türk Lirası tutarları: 1.234,56 (binlik ayırıcı nokta, ondalık virgül).</summary>
public static class TurkishPara
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Kullanıcı/API metnini tr-TR sayı kurallarıyla <see cref="decimal"/> yapar (örn. 1.234,56).</summary>
    public static bool TryParse(string? s, out decimal value) =>
        decimal.TryParse(s, NumberStyles.Number, Culture, out value);

    public static string Format(decimal value, int ondalikBasamak = 2) =>
        value.ToString("N" + ondalikBasamak, Culture);

    /// <summary>Sayı + boşluk + ₺ (örn. 123,45 ₺).</summary>
    public static string FormatMoney(decimal value, int ondalikBasamak = 2) =>
        Format(value, ondalikBasamak) + " \u20BA";
}

public static class TurkishParaExtensions
{
    public static string ToTrPara(this decimal value, int ondalikBasamak = 2) =>
        TurkishPara.Format(value, ondalikBasamak);

    public static string ToTrParaMoney(this decimal value, int ondalikBasamak = 2) =>
        TurkishPara.FormatMoney(value, ondalikBasamak);

    public static string ToTrPara(this decimal? value, int ondalikBasamak = 2) =>
        value.HasValue ? value.Value.ToTrPara(ondalikBasamak) : "—";

    public static string ToTrParaMoney(this decimal? value, int ondalikBasamak = 2) =>
        value.HasValue ? value.Value.ToTrParaMoney(ondalikBasamak) : "—";
}
