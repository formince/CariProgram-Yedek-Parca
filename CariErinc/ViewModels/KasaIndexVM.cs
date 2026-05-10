using CariErinc.Models;

namespace CariErinc.ViewModels;

public class KasaIndexVM
{
    public List<KasaHareket> Hareketler { get; set; } = new();
    public decimal ToplamGelir { get; set; }
    public decimal ToplamGider { get; set; }
    public decimal NetBakiye { get; set; }
    
    // Filtreler
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public string? SearchTerm { get; set; }
    public int? GiderKategoriId { get; set; }
    public KasaHareketTipi? HareketTipi { get; set; }

    // Sayfalama
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 30;

    // UI Destek
    public Microsoft.AspNetCore.Mvc.Rendering.SelectList? KategoriListesi { get; set; }
}
