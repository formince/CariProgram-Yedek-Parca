using CariErinc.Helpers;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class UrunService : IUrunService
{
    private readonly CariErinc.Data.AppDbContext _db;

    public UrunService(CariErinc.Data.AppDbContext db)
    {
        _db = db;
    }

    public async Task<UrunIndexVM> GetPagedListAsync(int page = 1, string? arama = null, string? kategori = null, int? tedarikciId = null, string? stokDurumu = null)
    {
        int pageSize = 30;
        var query = ApplyAdvancedFilters(_db.Urunler.Include(u => u.Tedarikci).OrderBy(u => u.Ad).AsQueryable(), arama, kategori, tedarikciId, stokDurumu);
        
        var pagedResult = await query.ToPagedListAsync(page, pageSize);

        return new UrunIndexVM
        {
            Urunler = pagedResult.Items,
            Arama = arama,
            Kategori = kategori,
            TedarikciId = tedarikciId,
            StokDurumu = stokDurumu,
            CurrentPage = pagedResult.CurrentPage,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount,
            TotalPages = pagedResult.TotalPages
        };
    }

    private IQueryable<Urun> ApplyAdvancedFilters(IQueryable<Urun> query, string? arama, string? kategori, int? tedarikciId, string? stokDurumu)
    {
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var pattern = TurkceHelper.ILikePattern(arama);
            query = query.Where(u =>
                (u.Ad != null && EF.Functions.ILike(u.Ad, pattern)) ||
                (u.Barkod != null && EF.Functions.ILike(u.Barkod, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(kategori))
        {
            query = query.Where(u => u.Kategori == kategori);
        }

        if (tedarikciId.HasValue && tedarikciId.Value > 0)
        {
            query = query.Where(u => u.TedarikciId == tedarikciId);
        }

        if (!string.IsNullOrWhiteSpace(stokDurumu))
        {
            if (stokDurumu == "Kritik")
                query = query.Where(u => u.StokAdedi <= u.MinStokUyari && u.StokAdedi > 0);
            else if (stokDurumu == "Yok")
                query = query.Where(u => u.StokAdedi <= 0);
            else if (stokDurumu == "Var")
                query = query.Where(u => u.StokAdedi > 0);
        }

        return query;
    }

    public async Task<List<Urun>> GetAllAsync(string? arama = null)
    {
        var query = ApplyAdvancedFilters(_db.Urunler.Include(u => u.Tedarikci).AsQueryable(), arama, null, null, null);
        return await query.OrderBy(u => u.Ad).ToListAsync();
    }

    public async Task<Urun?> GetByIdAsync(int id)
    {
        return await _db.Urunler.Include(u => u.Tedarikci).FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<ServiceResult> SaveAsync(UrunVM vm)
    {
        if (vm.Id == 0) // Ekleme
        {
            if (!string.IsNullOrWhiteSpace(vm.Barkod))
            {
                var normalizedBarkod = vm.Barkod.Trim().ToLower();
                if (await _db.Urunler.AnyAsync(u => u.Barkod != null && u.Barkod.Trim().ToLower() == normalizedBarkod))
                    return ServiceResult.Failure("Bu barkod zaten kayıtlı.");
            }

            if (vm.StokAdedi < 0)
                return ServiceResult.Failure("Stok adedi 0'ın altına düşemez.");

            var urun = new Urun
            {
                Ad = vm.Ad.Trim(),
                Barkod = string.IsNullOrWhiteSpace(vm.Barkod) ? null : vm.Barkod.Trim(),
                Kategori = vm.Kategori?.Trim() ?? "",
                BirimFiyat = vm.BirimFiyat,
                KdvOrani = vm.KdvOrani,
                AlisFiyati = vm.AlisFiyati,
                StokAdedi = vm.StokAdedi,
                MinStokUyari = vm.MinStokUyari,
                TedarikciId = vm.TedarikciId,
                OlusturulmaTarihi = DateTime.UtcNow,
                GuncellenmeTarihi = DateTime.UtcNow
            };

            _db.Urunler.Add(urun);
            await _db.SaveChangesAsync();
            return ServiceResult.Success("Ürün başarıyla eklendi.");
        }
        else // Güncelleme
        {
            var urun = await _db.Urunler.FindAsync(vm.Id);
            if (urun == null)
                return ServiceResult.Failure("Ürün bulunamadı.");

            if (!string.IsNullOrWhiteSpace(vm.Barkod))
            {
                var normalizedBarkod = vm.Barkod.Trim().ToLower();
                if (await _db.Urunler.AnyAsync(u => u.Barkod != null && u.Barkod.Trim().ToLower() == normalizedBarkod && u.Id != vm.Id))
                    return ServiceResult.Failure("Bu barkod başka bir üründe kayıtlı.");
            }

            if (vm.StokAdedi < 0)
                return ServiceResult.Failure("Stok adedi 0'ın altına düşemez.");

            urun.Ad = vm.Ad.Trim();
            urun.Barkod = string.IsNullOrWhiteSpace(vm.Barkod) ? null : vm.Barkod.Trim();
            urun.Kategori = vm.Kategori?.Trim() ?? "";
            urun.BirimFiyat = vm.BirimFiyat;
            urun.KdvOrani = vm.KdvOrani;
            urun.AlisFiyati = vm.AlisFiyati;
            urun.StokAdedi = vm.StokAdedi;
            urun.MinStokUyari = vm.MinStokUyari;
            urun.TedarikciId = vm.TedarikciId;
            urun.GuncellenmeTarihi = DateTime.UtcNow;

            _db.Urunler.Update(urun);
            await _db.SaveChangesAsync();
            return ServiceResult.Success("Ürün başarıyla güncellendi.");
        }
    }

    public async Task<ServiceResult> SilAsync(int id)
    {
        if (await _db.StokHareketler.AnyAsync(s => s.UrunId == id))
            return ServiceResult.Failure("Bu üründe stok hareketi bulunduğu için silinemez.");

        var urun = await _db.Urunler.FindAsync(id);
        if (urun == null)
            return ServiceResult.Failure("Ürün bulunamadı.");

        _db.Urunler.Remove(urun);
        await _db.SaveChangesAsync();
        return ServiceResult.Success("Ürün başarıyla silindi.");
    }

    public async Task<Dictionary<int, SonAlisInfoVM>> GetSonAlisBilgileriAsync(List<int> urunIds)
    {
        var result = new Dictionary<int, SonAlisInfoVM>();
        if (urunIds == null || !urunIds.Any()) return result;

        var sonAlislar = await (
            from ad in _db.AlisDetaylari.AsNoTracking()
            join a in _db.Alislar.AsNoTracking() on ad.AlisId equals a.Id
            where urunIds.Contains(ad.UrunId)
            orderby a.Tarih descending
            select new { ad.UrunId, ad.BirimFiyat, ad.Iskonto1, ad.Iskonto2 }
        ).ToListAsync();

        foreach (var r in sonAlislar)
        {
            if (!result.ContainsKey(r.UrunId))
            {
                result[r.UrunId] = new SonAlisInfoVM
                {
                    ListeFiyati = r.BirimFiyat,
                    Iskonto1 = r.Iskonto1,
                    Iskonto2 = r.Iskonto2
                };
            }
        }
        return result;
    }

    public async Task<List<StokHareket>> GetSonStokHareketleriAsync(int urunId, int count)
    {
        return await _db.StokHareketler
            .AsNoTracking()
            .Where(s => s.UrunId == urunId)
            .OrderByDescending(s => s.Tarih)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Urun>> SearchAsync(string query, int limit)
    {
        var term = query.Trim().ToLower();
        return await _db.Urunler
            .AsNoTracking()
            .Where(u =>
                (u.Ad != null && u.Ad.ToLower().Contains(term)) ||
                (u.Barkod != null && u.Barkod.ToLower().Contains(term)))
            .OrderBy(u => u.Ad)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Urun?> GetByBarkodAsync(string barkod)
    {
        return await _db.Urunler
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Barkod != null && u.Barkod.Trim().ToLower() == barkod.Trim().ToLower());
    }

    public async Task<List<Urun>> GetByIdsAsync(List<int> urunIds)
    {
        return await _db.Urunler
            .AsNoTracking()
            .Where(u => urunIds.Contains(u.Id))
            .OrderBy(u => u.Ad)
            .ToListAsync();
    }
}
