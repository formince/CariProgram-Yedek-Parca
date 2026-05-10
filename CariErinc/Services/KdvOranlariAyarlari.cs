namespace CariErinc.Services;

/// <summary>
/// İşletme ayarı <c>KdvOranlari</c> (virgül/noktalı virgülle ayrılmış tam sayılar) ayrıştırma.
/// </summary>
public static class KdvOranlariAyarlari
{
    public const string Anahtar = "KdvOranlari";

    /// <summary>Boş/geçersiz parçalar atlanır; 0–100 dışı yok sayılır.</summary>
    public static List<int> ParseLenient(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin)) return new List<int>();
        var list = new List<int>();
        foreach (var part in metin.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var v) && v >= 0 && v <= 100)
                list.Add(v);
        }
        return list.Distinct().OrderBy(x => x).ToList();
    }

    /// <summary>Ayarlar kaydı için: en az bir geçerli oran zorunlu.</summary>
    public static bool TryParseKayit(string? metin, out List<int> sonuc, out string? hata)
    {
        sonuc = new List<int>();
        hata = null;
        if (string.IsNullOrWhiteSpace(metin))
        {
            hata = "En az bir KDV oranı girin (örn: 0,1,10,20).";
            return false;
        }
        foreach (var part in metin.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var v))
            {
                hata = $"Geçersiz sayı: \"{part}\"";
                return false;
            }
            if (v < 0 || v > 100)
            {
                hata = $"KDV oranı 0–100 olmalıdır: {v}";
                return false;
            }
            sonuc.Add(v);
        }
        if (sonuc.Count == 0)
        {
            hata = "En az bir KDV oranı girin.";
            return false;
        }
        sonuc = sonuc.Distinct().OrderBy(x => x).ToList();
        return true;
    }
}
