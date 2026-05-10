using CariErinc.Models;

namespace CariErinc.ViewModels;

public class MusteriIndexVM
{
    public List<Musteri> Musteriler { get; set; } = new();
    public string? Arama { get; set; }
    
    // Sayfalama
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 30;
}
