using CariErinc.Models;

namespace CariErinc.ViewModels;

public class DashboardVM
{
    public decimal BugunkuSatisToplam { get; set; }
    public decimal KasaBakiyesi { get; set; }
    public decimal AcikVeresiyeToplam { get; set; }
    public int AcikVeresiyeMusteriSayisi { get; set; }
    public int KritikStokSayisi { get; set; }
    public List<Urun> KritikStokUrunleri { get; set; } = new();
}
