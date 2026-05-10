using CariErinc.Models;

namespace CariErinc.ViewModels;

public class AlisIndexVM
{
    public List<Alis> Alislar { get; set; } = new();
    
    // Filtreler
    public int? TedarikciId { get; set; }
    public string? Baslangic { get; set; }
    public string? Bitis { get; set; }

    // Sayfalama
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 30;
}
