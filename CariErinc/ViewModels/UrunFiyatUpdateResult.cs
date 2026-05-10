using System;

namespace CariErinc.ViewModels;

public class UrunFiyatUpdateResult
{
    public bool IsChanged { get; set; }
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
