using System;
using System.Collections.Generic;

namespace CariErinc.ViewModels;

public class KarZararRaporVM
{
    // Dönem
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }

    // Satış
    public decimal BrutSatisTutari { get; set; }
    public decimal IadeTutari { get; set; }
    public decimal NetSatisTutari { get; set; }

    // Maliyet
    public decimal SatisMaliyeti { get; set; }       // COGS
    public decimal BrutKar { get; set; }             // NetSatisTutari - SatisMaliyeti

    // Giderler
    public decimal ToplamGider { get; set; }         // kasa gider (alış hariç)
    public List<GiderKategoriOzetVM> GiderKategoriler { get; set; } = new();

    // Sonuç
    public decimal NetKar { get; set; }              // BrutKar - ToplamGider
    public bool KarMi => NetKar >= 0;                // true=kâr, false=zarar

    // Ek bilgiler
    public decimal KdvToplam { get; set; }
    public decimal IndirimToplam { get; set; }
    public int SatisSayisi { get; set; }
    public int IadeSayisi { get; set; }

    // Ürün bazında kâr marjı
    public List<UrunKarVM> EnKarliUrunler { get; set; } = new();   // Top 10
    public List<UrunKarVM> EnAzKarliUrunler { get; set; } = new(); // Bottom 5
}

public class GiderKategoriOzetVM
{
    public string Kategori { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
}

public class UrunKarVM
{
    public string UrunAdi { get; set; } = string.Empty;
    public int SatilanMiktar { get; set; }
    public decimal SatisTutari { get; set; }
    public decimal MaliyetTutari { get; set; }
    public decimal KarTutari { get; set; }
    public decimal KarMarjiYuzdesi { get; set; }   // KarTutari / SatisTutari * 100
}
