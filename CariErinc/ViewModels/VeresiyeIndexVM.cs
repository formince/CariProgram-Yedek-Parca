using CariErinc.Models;

namespace CariErinc.ViewModels;

public class VeresiyeIndexVM
{
    public List<Veresiye> Veresiyeler { get; set; } = new();
    public int? MusteriId { get; set; }
    public OdenmeDurumu? Durum { get; set; }
    public string? Baslangic { get; set; }
    public string? Bitis { get; set; }
    
    // Sayfalama
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 30;
}
