using CariErinc.Models;

namespace CariErinc.ViewModels;

public class SatisIndexVM
{
    public List<Satis> Satislar { get; set; } = new();

    public int? MusteriId { get; set; }
    public string? Tip { get; set; }
    public string? Baslangic { get; set; }
    public string? Bitis { get; set; }
    public bool DahilIptaller { get; set; }

    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 30;
}
