using CariErinc.Models;

namespace CariErinc.ViewModels;

public enum EslesmeTipi { Tam, Oneri, Yok }

public class FaturaAnalizSonucVM
{
    public string? FaturaNo { get; set; }
    public DateTime? Tarih { get; set; }
    public string? TedarikciVkn { get; set; }
    public string? TedarikciUnvan { get; set; }
    public bool ZatenKayitliMi { get; set; } // Duplicate control
    
    public List<FaturaSatirAnalizVM> Satirlar { get; set; } = new();
}

public class FaturaSatirAnalizVM
{
    public string FaturaUrunAdi { get; set; } = string.Empty;
    public string? Barkod { get; set; }
    public int Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public int KdvOrani { get; set; }
    public decimal IskontoTutari { get; set; }
    public decimal IskontoOrani { get; set; }
    
    // Eşleşme Bilgileri
    public int? SistemUrunId { get; set; }
    public string? SistemUrunAdi { get; set; }
    public EslesmeTipi Durum { get; set; }
    public int GuvenSkoru { get; set; }
}
