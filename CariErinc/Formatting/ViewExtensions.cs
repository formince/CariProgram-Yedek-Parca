using System.Globalization;

namespace CariErinc.Formatting;

public static class ViewExtensions
{
    private static readonly CultureInfo TrCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static string ToTrDate(this DateTime date) =>
        date.ToString("dd.MM.yyyy", TrCulture);

    public static string ToTrDateTime(this DateTime date) =>
        date.ToString("dd.MM.yyyy HH:mm", TrCulture);

    public static string ToTrDate(this DateTime? date) =>
        date.HasValue ? date.Value.ToTrDate() : "—";

    public static string ToTrDateTime(this DateTime? date) =>
        date.HasValue ? date.Value.ToTrDateTime() : "—";
}
