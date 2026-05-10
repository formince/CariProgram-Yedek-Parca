using System.Globalization;

namespace CariErinc.Helpers;

/// <summary>
/// Türkçe büyük/küçük harf sorununu çözmek için yardımcı sınıf.
/// DB sorgularında EF.Functions.ILike ile birlikte kullanılır;
/// bu metot sadece arama terimini temizlemek / normalize etmek içindir.
/// </summary>
public static class TurkceHelper
{
    private static readonly CultureInfo TrCulture = new CultureInfo("tr-TR");

    /// <summary>
    /// Verilen metni Türk kültürü kurallarıyla küçük harfe çevirir ve baştaki/sondaki boşlukları siler.
    /// Örnek: "İSTANBUL" → "istanbul" | "IŞIK" → "ışık"
    /// </summary>
    public static string Normalize(string? text)
        => string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim().ToLower(TrCulture);

    /// <summary>
    /// PostgreSQL ILIKE sorguları için arama deseni üretir.
    /// Örnek: Normalize("İst") → "%istanbul%" pattern için "%ist%"
    /// </summary>
    public static string ILikePattern(string? aranan)
        => $"%{Normalize(aranan)}%";
}
