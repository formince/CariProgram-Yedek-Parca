using CariErinc.Data;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class RaporService : IRaporService
{
    private readonly AppDbContext _db;

    public RaporService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<GunlukSatisRaporVM> GetGunlukSatisAsync(DateTime? tarih = null)
    {
        var gun = tarih?.Date ?? DateTime.Today;

        var query = _db.KasaHareketler.AsQueryable();
        var gelirler = await ApplyFilters(query, gun, gun, KasaHareketTipi.Gelir).OrderBy(k => k.Tarih).ToListAsync();
        
        var toplamGelir = await GetToplamAsync(KasaHareketTipi.Gelir, gun, gun);
        var toplamGider = await GetToplamAsync(KasaHareketTipi.Gider, gun, gun);

        return new GunlukSatisRaporVM
        {
            Tarih = gun,
            KasaHareketler = gelirler,
            ToplamGelir = toplamGelir,
            ToplamGider = toplamGider,
            NetKar = toplamGelir - toplamGider
        };
    }

    private IQueryable<KasaHareket> ApplyFilters(IQueryable<KasaHareket> query, DateTime? baslangic, DateTime? bitis, KasaHareketTipi? tip)
    {
        if (baslangic.HasValue)
            query = query.Where(k => k.Tarih >= baslangic.Value.Date);
        if (bitis.HasValue)
            query = query.Where(k => k.Tarih < bitis.Value.Date.AddDays(1));
        if (tip.HasValue)
            query = query.Where(k => k.HareketTipi == tip.Value);
        return query;
    }

    private async Task<decimal> GetToplamAsync(KasaHareketTipi tip, DateTime? baslangic, DateTime? bitis)
    {
        var query = _db.KasaHareketler.Where(k => k.HareketTipi == tip);
        if (baslangic.HasValue)
            query = query.Where(k => k.Tarih >= baslangic.Value.Date);
        if (bitis.HasValue)
            query = query.Where(k => k.Tarih < bitis.Value.Date.AddDays(1));
        return await query.SumAsync(k => k.Tutar);
    }

    public async Task<AylikRaporVM> GetAylikRaporAsync(int? yil = null, int? ay = null)
    {
        var now = DateTime.UtcNow;
        var y = yil ?? now.Year;
        var a = ay ?? now.Month;

        var baslangic = new DateTime(y, a, 1);

        var bitis = baslangic.AddMonths(1).AddDays(-1);

        var hareketler = await ApplyFilters(_db.KasaHareketler.AsQueryable(), baslangic, bitis, null).ToListAsync();
        var aylikGelir = await GetToplamAsync(KasaHareketTipi.Gelir, baslangic, bitis);
        var aylikGider = await GetToplamAsync(KasaHareketTipi.Gider, baslangic, bitis);

        var gunlukOzetler = hareketler
            .GroupBy(k => k.Tarih.Date)
            .Select(g => new GunlukOzetVM
            {
                Tarih = g.Key,
                Gelir = g.Where(k => k.HareketTipi == KasaHareketTipi.Gelir).Sum(k => k.Tutar),
                Gider = g.Where(k => k.HareketTipi == KasaHareketTipi.Gider).Sum(k => k.Tutar)
            })
            .OrderBy(x => x.Tarih)
            .ToList();

        for (var d = 1; d <= DateTime.DaysInMonth(y, a); d++)
        {
            var gun = new DateTime(y, a, d);

            if (!gunlukOzetler.Any(o => o.Tarih.Date == gun.Date))
                gunlukOzetler.Add(new GunlukOzetVM { Tarih = gun, Gelir = 0, Gider = 0 });
        }
        gunlukOzetler = gunlukOzetler.OrderBy(o => o.Tarih).ToList();

        return new AylikRaporVM
        {
            Yil = y,
            Ay = a,
            AylikGelir = aylikGelir,
            AylikGider = aylikGider,
            NetBakiye = aylikGelir - aylikGider,
            GunlukOzetler = gunlukOzetler
        };
    }

    public async Task<StokUyariRaporVM> GetStokUyariAsync()
    {
        var kritikler = await _db.Urunler
            .Include(u => u.Tedarikci)
            .Where(u => u.StokAdedi <= u.MinStokUyari)
            .OrderBy(u => u.StokAdedi)
            .ToListAsync();
            
        return new StokUyariRaporVM { KritikUrunler = kritikler };
    }

    public async Task<VeresiyeRaporVM> GetVeresiyeRaporAsync()
    {
        var aciklar = await _db.Veresiyeler
            .Include(v => v.Musteri)
            .Include(v => v.Odemeler)
            .Where(v => v.OdenmeDurumu != OdenmeDurumu.Odendi && v.OdenmeDurumu != OdenmeDurumu.Iptal)
            .ToListAsync();

        var toplamAcikBorc = aciklar.Sum(v =>
        {
            var odenen = v.Odemeler?.Sum(o => o.OdemeTutari) ?? 0;
            return v.Tutar - odenen;
        });

        return new VeresiyeRaporVM
        {
            AcikVeresiyeler = aciklar,
            ToplamAcikBorc = toplamAcikBorc
        };
    }

    public async Task<KarZararRaporVM> GetKarZararAsync(DateTime baslangic, DateTime bitis)
    {
        var baslangicUtc = baslangic;
        var bitisUtc = bitis;

        var bitisDahil = bitisUtc.Date.AddDays(1).AddTicks(-1);

        // 1. Satışlar (Yalnızca tamamlanmış ve iptal edilmemiş)
        var satislar = await _db.Satislar
            .Include(s => s.SatisDetaylari)
                .ThenInclude(sd => sd.Urun)
            .Include(s => s.SatisDetaylari)
                .ThenInclude(sd => sd.SatisIadeDetaylari)
            .Where(s => s.Tarih >= baslangicUtc && s.Tarih <= bitisDahil && s.Durum == SatisDurum.Tamamlandi && !s.IptalEdildi)
            .ToListAsync();

        // 2. İadeler
        var iadeler = await _db.SatisIadeler
            .Include(i => i.IadeDetaylari)
            .Where(i => i.IadeTarihi >= baslangicUtc && i.IadeTarihi <= bitisDahil)
            .ToListAsync();

        // 3. Giderler (Alış hariç)
        var hareketler = await _db.KasaHareketler
            .Include(kh => kh.GiderKategori)
            .Where(kh => kh.Tarih >= baslangicUtc && kh.Tarih <= bitisDahil)
            .ToListAsync();

        var giderler = hareketler
            .Where(kh => kh.HareketTipi == KasaHareketTipi.Gider && kh.Kategori != "Alış")
            .ToList();

        // --- Hesaplamalar ---
        
        var brutSatisTutari = satislar.Sum(s => s.IndirimSonrasiToplam); // İndirim sonrası ama iade öncesi tutar
        var iadeTutari = iadeler.Sum(i => i.IadeDetaylari.Sum(d => d.IadeTutari));
        var netSatisTutari = brutSatisTutari - iadeTutari;

        // COGS (Satılan Malın Maliyeti) calculation
        // Her bir satış detayının maliyeti: (Satılan Miktar - İade Edilen Miktar) * Alış Birim Fiyatı
        var satisDetaylari = satislar.SelectMany(s => s.SatisDetaylari).ToList();
        
        // Bu dönemdeki satışların maliyeti (iadeler düşülerek)
        decimal satisMaliyeti = 0;
        foreach (var sd in satisDetaylari)
        {
            var iadeMiktari = sd.SatisIadeDetaylari?.Sum(x => x.IadeMiktar) ?? 0;
            var netMiktar = Math.Max(0, sd.Miktar - iadeMiktari);
            satisMaliyeti += netMiktar * sd.AlisBirimFiyati;
        }

        var brutKar = netSatisTutari - satisMaliyeti;
        var toplamGider = giderler.Sum(g => g.Tutar);
        var netKar = brutKar - toplamGider;

        var rapor = new KarZararRaporVM
        {
            BaslangicTarihi = baslangic,
            BitisTarihi = bitis,
            BrutSatisTutari = brutSatisTutari,
            IadeTutari = iadeTutari,
            NetSatisTutari = netSatisTutari,
            SatisMaliyeti = satisMaliyeti,
            BrutKar = brutKar,
            ToplamGider = toplamGider,
            NetKar = netKar,
            KdvToplam = satisDetaylari.Sum(d => d.KdvTutari), // Bu biraz tartışmalı; iade KDV'si düşülmeli mi? Şimdilik satış KDV'si
            IndirimToplam = satislar.Sum(s => s.GenelIndirimTutari) + satisDetaylari.Sum(d => d.IndirimTutari),
            SatisSayisi = satislar.Count,
            IadeSayisi = iadeler.Count
        };

        // Gider Kategorileri
        rapor.GiderKategoriler = giderler
            .GroupBy(g => g.GiderKategori?.Ad ?? g.Kategori ?? "Diğer")
            .Select(g => new GiderKategoriOzetVM
            {
                Kategori = g.Key,
                Tutar = g.Sum(x => x.Tutar)
            })
            .OrderByDescending(x => x.Tutar)
            .ToList();

        // Ürün Bazlı Kâr Analizi
        var urunGruplari = satisDetaylari
            .GroupBy(d => d.UrunId)
            .Select(g => {
                var model = g.First();
                var toplamSatis = g.Sum(x => x.NetTutar); // Satırın net tutarı
                // Satırdan gelen iadeleri düş
                var satirdakiIadeTutari = g.Sum(x => x.SatisIadeDetaylari?.Sum(i => i.IadeTutari) ?? 0);
                var netSatis = toplamSatis - satirdakiIadeTutari;
                
                var toplamMiktar = g.Sum(x => x.Miktar);
                var toplamIadeMiktar = g.Sum(x => x.SatisIadeDetaylari?.Sum(i => i.IadeMiktar) ?? 0);
                var netMiktar = Math.Max(0, toplamMiktar - toplamIadeMiktar);
                
                var toplamMaliyet = netMiktar * model.AlisBirimFiyati;
                var kar = netSatis - toplamMaliyet;

                return new UrunKarVM
                {
                    UrunAdi = model.Urun?.Ad ?? "Bilinmeyen Ürün",
                    SatilanMiktar = netMiktar,
                    SatisTutari = netSatis,
                    MaliyetTutari = toplamMaliyet,
                    KarTutari = kar,
                    KarMarjiYuzdesi = netSatis > 0 ? (kar / netSatis * 100) : 0
                };
            })
            .Where(x => x.SatilanMiktar > 0 || x.SatisTutari != 0)
            .ToList();

        rapor.EnKarliUrunler = urunGruplari.OrderByDescending(x => x.KarTutari).Take(10).ToList();
        rapor.EnAzKarliUrunler = urunGruplari.OrderBy(x => x.KarTutari).Take(5).ToList();

        return rapor;
    }
}
