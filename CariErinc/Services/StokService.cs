using CariErinc.Data;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.EntityFrameworkCore;
using CariErinc.Helpers;

namespace CariErinc.Services;

public class StokService : IStokService
{
    private readonly AppDbContext _db;

    public StokService(AppDbContext db)
    {
        _db = db;
    }


    private static int SonrakiStok(int mevcut, HareketTipi tip, int miktar) => tip switch
    {
        HareketTipi.Giris or HareketTipi.Iade => mevcut + miktar,
        HareketTipi.Cikis => mevcut - miktar,
        HareketTipi.Sayim => miktar,
        _ => mevcut
    };

    private static bool MiktarKuraliGecerli(HareketTipi tip, int miktar, out string? hata)
    {
        hata = null;
        if (miktar < 0)
        {
            hata = "Miktar negatif olamaz.";
            return false;
        }
        if (tip == HareketTipi.Sayim)
            return true;
        if (miktar < 1)
        {
            hata = "Giriş, çıkış ve iade için miktar en az 1 olmalıdır.";
            return false;
        }
        return true;
    }

    /// <summary>Ürün için mevcut hareketlere göre stok son durumu (DB güncellenmez).</summary>
    private async Task<int> HesaplaStokSonDurumAsync(int urunId, CancellationToken cancellationToken = default)
    {
        var hareketler = await _db.StokHareketler
            .AsNoTracking()
            .Where(s => s.UrunId == urunId)
            .OrderBy(s => s.Tarih)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        var stok = 0;
        foreach (var h in hareketler)
            stok = SonrakiStok(stok, h.HareketTipi, h.Miktar);
        return stok;
    }

    private async Task StoguHareketlerdenGuncelleAsync(int urunId, CancellationToken cancellationToken = default)
    {
        var urun = await _db.Urunler.FirstOrDefaultAsync(u => u.Id == urunId, cancellationToken);
        if (urun == null) return;

        var stok = await HesaplaStokSonDurumAsync(urunId, cancellationToken);
        urun.StokAdedi = stok;
        urun.GuncellenmeTarihi = DateTime.UtcNow;
    }

    public async Task<StokHareketIndexVM> GetPagedListAsync(int page = 1, int? urunId = null, DateTime? baslangic = null, DateTime? bitis = null)
    {
        int pageSize = 30;
        var query = ApplyFilters(_db.StokHareketler.Include(s => s.Urun).OrderByDescending(s => s.Tarih).AsQueryable(), urunId, baslangic, bitis);
        
        var pagedResult = await query.ToPagedListAsync(page, pageSize);

        return new StokHareketIndexVM
        {
            Hareketler = pagedResult.Items,
            UrunId = urunId,
            Baslangic = baslangic,
            Bitis = bitis,
            CurrentPage = pagedResult.CurrentPage,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount,
            TotalPages = pagedResult.TotalPages
        };
    }

    private IQueryable<StokHareket> ApplyFilters(IQueryable<StokHareket> query, int? urunId, DateTime? baslangic, DateTime? bitis)
    {
        if (urunId.HasValue && urunId.Value > 0)
            query = query.Where(s => s.UrunId == urunId.Value);

        if (baslangic.HasValue)
            query = query.Where(s => s.Tarih >= baslangic.Value.Date);

        if (bitis.HasValue)
            query = query.Where(s => s.Tarih < bitis.Value.Date.AddDays(1));

        return query;
    }

    public async Task<List<StokHareket>> GetAllAsync(int? urunId, DateTime? baslangic, DateTime? bitis)
    {
        var query = ApplyFilters(_db.StokHareketler.Include(s => s.Urun).AsQueryable(), urunId, baslangic, bitis);
        return await query.OrderByDescending(s => s.Tarih).ToListAsync();
    }

    private async Task<ServiceResult> HareketEkleAsync(StokHareketVM vm)

    {
        var urun = await _db.Urunler.FindAsync(vm.UrunId);
        if (urun == null)
            return ServiceResult.Failure("Ürün bulunamadı.");

        if (!MiktarKuraliGecerli(vm.HareketTipi, vm.Miktar, out var mkHata))
            return ServiceResult.Failure(mkHata!);

        var simdi = await HesaplaStokSonDurumAsync(vm.UrunId);
        var yeni = SonrakiStok(simdi, vm.HareketTipi, vm.Miktar);
        if (vm.HareketTipi == HareketTipi.Cikis && yeni < 0)
            return ServiceResult.Failure($"Stok yetersiz. Hareketlere göre mevcut stok: {simdi}");

        var hareket = new StokHareket
        {
            UrunId = vm.UrunId,
            HareketTipi = vm.HareketTipi,
            Miktar = vm.Miktar,
            Aciklama = vm.Aciklama?.Trim(),
            Tarih = vm.Tarih ?? DateTime.UtcNow

        };

        _db.StokHareketler.Add(hareket);
        await StoguHareketlerdenGuncelleAsync(vm.UrunId);
        await _db.SaveChangesAsync();

        return ServiceResult.Success("Stok hareketi başarıyla kaydedildi.");
    }

    public async Task<ServiceResult> SaveAsync(StokHareketVM vm)
    {
        if (vm.Id == 0)
            return await HareketEkleAsync(vm);
        
        return await HareketGuncelleAsync(vm);
    }

    public async Task<ServiceResult> SilAsync(int id)
    {
        return await HareketSilAsync(id);
    }

    public async Task<StokHareketVM?> GetHareketVmAsync(int id)
    {
        var h = await _db.StokHareketler.Include(s => s.Urun).FirstOrDefaultAsync(x => x.Id == id);
        if (h == null) return null;

        return new StokHareketVM
        {
            Id = h.Id,
            UrunId = h.UrunId,
            HareketTipi = h.HareketTipi,
            Miktar = h.Miktar,
            Aciklama = h.Aciklama,
            Tarih = h.Tarih
        };
    }

    private async Task<ServiceResult> HareketGuncelleAsync(StokHareketVM vm)

    {
        if (vm.Id <= 0)
            return ServiceResult.Failure("Geçersiz kayıt.");

        if (!vm.Tarih.HasValue)
            return ServiceResult.Failure("Tarih gereklidir.");

        var h = await _db.StokHareketler.FirstOrDefaultAsync(x => x.Id == vm.Id);
        if (h == null)
            return ServiceResult.Failure("Hareket bulunamadı.");

        var urun = await _db.Urunler.FindAsync(vm.UrunId);
        if (urun == null)
            return ServiceResult.Failure("Ürün bulunamadı.");

        if (!MiktarKuraliGecerli(vm.HareketTipi, vm.Miktar, out var mkHata))
            return ServiceResult.Failure(mkHata!);

        var oldUrunId = h.UrunId;
        h.UrunId = vm.UrunId;
        h.HareketTipi = vm.HareketTipi;
        h.Miktar = vm.Miktar;
        h.Aciklama = vm.Aciklama?.Trim();
        h.Tarih = vm.Tarih.Value;


        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            await StoguHareketlerdenGuncelleAsync(oldUrunId);
            if (vm.UrunId != oldUrunId)
                await StoguHareketlerdenGuncelleAsync(vm.UrunId);
            
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success("Stok hareketi güncellendi; ürün stoğu hareketlere göre yeniden hesaplandı.");
    }

    private async Task<ServiceResult> HareketSilAsync(int id)

    {
        var h = await _db.StokHareketler.FirstOrDefaultAsync(x => x.Id == id);
        if (h == null)
            return ServiceResult.Failure("Hareket bulunamadı.");

        var urunId = h.UrunId;
        _db.StokHareketler.Remove(h);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            await StoguHareketlerdenGuncelleAsync(urunId);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success("Hareket silindi; ürün stoğu hareketlere göre yeniden hesaplandı.");
    }

    public void StokCikisYap(Urun urun, int miktar, string islemAciklamasi)
    {
        urun.StokCikis(miktar);

        _db.StokHareketler.Add(new StokHareket
        {
            UrunId = urun.Id,
            HareketTipi = HareketTipi.Cikis,
            Miktar = miktar,
            Aciklama = islemAciklamasi,
            Tarih = DateTime.UtcNow
        });
    }

    public void StokGirisYap(Urun urun, int miktar, string islemAciklamasi)
    {
        urun.StokGiris(miktar);

        _db.StokHareketler.Add(new StokHareket
        {
            UrunId = urun.Id,
            HareketTipi = HareketTipi.Giris,
            Miktar = miktar,
            Aciklama = islemAciklamasi,
            Tarih = DateTime.UtcNow
        });
    }
}
