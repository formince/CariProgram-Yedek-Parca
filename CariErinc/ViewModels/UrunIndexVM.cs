using CariErinc.Models;

namespace CariErinc.ViewModels;

/// <summary>Ürün listesinde alış kolonunda gösterilecek son fiş verileri.</summary>
public class SonAlisInfoVM
{
    public decimal ListeFiyati { get; set; }
    public decimal Iskonto1 { get; set; }
    public decimal Iskonto2 { get; set; }

    /// <summary>İskontolar uygulandıktan sonra net alış birim fiyatı (KDV hariç).</summary>
    public decimal NetFiyati => Math.Round(ListeFiyati * (1 - Iskonto1 / 100m) * (1 - Iskonto2 / 100m), 2);
}

public class UrunIndexVM
{
    public List<Urun> Urunler { get; set; } = new();

    public string? Arama { get; set; }
    public string? Kategori { get; set; }
    public int? TedarikciId { get; set; }
    public string? StokDurumu { get; set; }

    public string? AracMarkasi { get; set; }
    public string? AracModeli { get; set; }
    public ParcaTipi? ParcaTipi { get; set; }
    public string? ParcaKoduArama { get; set; }

    // Sayfalama
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 30;
}
