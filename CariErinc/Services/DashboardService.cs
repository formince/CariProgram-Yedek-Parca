using CariErinc.Data;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardVM> GetDashboardVerileriAsync()
    {
        var bugunBaslangic = DateTime.Today;
        var bugunBitis = DateTime.Today.AddDays(1);

        var bugunkuSatis = await _db.KasaHareketler
            .Where(k => k.HareketTipi == KasaHareketTipi.Gelir && k.Tarih >= bugunBaslangic && k.Tarih < bugunBitis)
            .SumAsync(k => k.Tutar);

        var gelir = await _db.KasaHareketler
            .Where(k => k.HareketTipi == KasaHareketTipi.Gelir)
            .SumAsync(k => k.Tutar);

        var gider = await _db.KasaHareketler
            .Where(k => k.HareketTipi == KasaHareketTipi.Gider)
            .SumAsync(k => k.Tutar);

        var acikVeresiyeler = await _db.Veresiyeler
            .Include(v => v.Odemeler)
            .Where(v => v.OdenmeDurumu != OdenmeDurumu.Odendi)
            .ToListAsync();

        var acikVeresiyeMusteriSayisi = await _db.Veresiyeler
            .Where(v => v.OdenmeDurumu != OdenmeDurumu.Odendi)
            .Select(v => v.MusteriId)
            .Distinct()
            .CountAsync();

        var kritikStokSayisi = await _db.Urunler
            .Where(u => u.StokAdedi <= u.MinStokUyari)
            .CountAsync();

        var kritikStokUrunleri = await _db.Urunler
            .Where(u => u.StokAdedi <= u.MinStokUyari)
            .OrderBy(u => u.StokAdedi)
            .Take(5)
            .ToListAsync();

        return new DashboardVM
        {
            BugunkuSatisToplam = bugunkuSatis,
            KasaBakiyesi = gelir - gider,
            AcikVeresiyeToplam = acikVeresiyeler.Sum(v => v.Tutar - (v.Odemeler?.Sum(o => o.OdemeTutari) ?? 0)),
            AcikVeresiyeMusteriSayisi = acikVeresiyeMusteriSayisi,
            KritikStokSayisi = kritikStokSayisi,
            KritikStokUrunleri = kritikStokUrunleri
        };
    }
}
