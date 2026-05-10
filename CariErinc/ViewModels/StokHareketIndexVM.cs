using CariErinc.Models;

namespace CariErinc.ViewModels;

public class StokHareketIndexVM
{
    public List<StokHareket> Hareketler { get; set; } = new();
    public int? UrunId { get; set; }
    public DateTime? Baslangic { get; set; }
    public DateTime? Bitis { get; set; }
    
    // Sayfalama
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 30;
}
