using CariErinc.Data;
using CariErinc.Helpers;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class KasaService : IKasaService
{
    private readonly AppDbContext _db;

    public KasaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<KasaIndexVM> GetKasaVerileriAsync(DateTime? baslangic, DateTime? bitis, int page = 1, string? search = null, int? kategoriId = null, KasaHareketTipi? tip = null)
    {
        int pageSize = 30;
        var query = ApplyFilters(_db.KasaHareketler.Include(k => k.GiderKategori).OrderByDescending(k => k.Tarih).AsQueryable(), baslangic, bitis, tip, search, kategoriId);
        
        var pagedResult = await query.ToPagedListAsync(page, pageSize);
        
        var toplamGelir = await GetToplamAsync(KasaHareketTipi.Gelir, baslangic, bitis);
        var toplamGider = await GetToplamAsync(KasaHareketTipi.Gider, baslangic, bitis);

        var kategoriler = await _db.GiderKategoriler.Where(k => k.AktifMi).OrderBy(k => k.Ad).ToListAsync();

        return new KasaIndexVM
        {
            Hareketler = pagedResult.Items,
            ToplamGelir = toplamGelir,
            ToplamGider = toplamGider,
            NetBakiye = toplamGelir - toplamGider,
            BaslangicTarihi = baslangic,
            BitisTarihi = bitis,
            SearchTerm = search,
            GiderKategoriId = kategoriId,
            HareketTipi = tip,
            CurrentPage = pagedResult.CurrentPage,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount,
            TotalPages = pagedResult.TotalPages,
            KategoriListesi = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(kategoriler, "Id", "Ad", kategoriId)
        };
    }

    private IQueryable<KasaHareket> ApplyFilters(IQueryable<KasaHareket> query, DateTime? baslangic, DateTime? bitis, KasaHareketTipi? tip, string? search, int? kategoriId)
    {
        if (baslangic.HasValue)
            query = query.Where(k => k.Tarih >= baslangic.Value.Date);

        if (bitis.HasValue)
            query = query.Where(k => k.Tarih < bitis.Value.Date.AddDays(1));

        if (tip.HasValue)
            query = query.Where(k => k.HareketTipi == tip.Value);

        if (kategoriId.HasValue && kategoriId.Value > 0)
            query = query.Where(k => k.GiderKategoriId == kategoriId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = TurkceHelper.ILikePattern(search);
            query = query.Where(k =>
                (k.Aciklama != null && EF.Functions.ILike(k.Aciklama, pattern)) ||
                (k.Kategori != null && EF.Functions.ILike(k.Kategori, pattern)));
        }

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

    public async Task<ServiceResult> SaveAsync(KasaVM vm)
    {
        var kategori = await _db.GiderKategoriler.FindAsync(vm.GiderKategoriId);
        if (kategori == null)
            return ServiceResult.Failure("Kategori bulunamadı.");

        var hareket = new KasaHareket
        {
            HareketTipi = vm.HareketTipi,
            GiderKategoriId = vm.GiderKategoriId,
            Kategori = kategori.Ad,
            Tutar = vm.Tutar,
            Aciklama = vm.Aciklama?.Trim(),
            Tarih = vm.Tarih
        };

        _db.KasaHareketler.Add(hareket);
        await _db.SaveChangesAsync();
        return ServiceResult.Success("Kasa kaydı başarıyla eklendi.");
    }

    public async Task<ServiceResult> SilAsync(int id)
    {
        var hareket = await _db.KasaHareketler.Include(k => k.GiderKategori).FirstOrDefaultAsync(k => k.Id == id);
        if (hareket == null)
            return ServiceResult.Failure("Kayıt bulunamadı.");

        if (hareket.GiderKategori != null && !hareket.GiderKategori.SilinebilirMi)
            return ServiceResult.Failure($"'{hareket.GiderKategori.Ad}' kayıtları silinemez (otomatik oluşturulur).");

        if (hareket.Kategori == "Alış" || hareket.Kategori == "Satış")
            return ServiceResult.Failure("Sistem kayıtları silinemez.");

        _db.KasaHareketler.Remove(hareket);
        await _db.SaveChangesAsync();
        return ServiceResult.Success("Kayıt başarıyla silindi.");
    }

    public void KasaGelirEkle(decimal tutar, string kategori, string aciklama)
    {
        _db.KasaHareketler.Add(new KasaHareket
        {
            HareketTipi = KasaHareketTipi.Gelir,
            Tutar = tutar,
            Kategori = kategori,
            Aciklama = aciklama,
            Tarih = DateTime.UtcNow
        });
    }

    public void KasaGiderCik(decimal tutar, string kategori, string aciklama, int? giderKategoriId = null)
    {
        _db.KasaHareketler.Add(new KasaHareket
        {
            HareketTipi = KasaHareketTipi.Gider,
            Tutar = tutar,
            Kategori = kategori,
            GiderKategoriId = giderKategoriId,
            Aciklama = aciklama,
            Tarih = DateTime.UtcNow
        });
    }
}
