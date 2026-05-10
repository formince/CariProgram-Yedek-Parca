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

    public async Task<UrunIndexVM> GetPagedListAsync(
        int page = 1,
        string? arama = null,
        string? kategori = null,
        int? tedarikciId = null,
        string? stokDurumu = null,
        string? aracMarkasi = null,
        string? aracModeli = null,
        ParcaTipi? parcaTipi = null,
        string? parcaKoduArama = null)
    {
        int pageSize = 30;
        var query = ApplyAdvancedFilters(
            _db.Urunler.Include(u => u.Tedarikci).OrderBy(u => u.Ad).AsQueryable(),
            arama,
            kategori,
            tedarikciId,
            stokDurumu,
            aracMarkasi,
            aracModeli,
            parcaTipi,
            parcaKoduArama);

        var pagedResult = await query.ToPagedListAsync(page, pageSize);

        return new UrunIndexVM
        {
            Urunler = pagedResult.Items,
            Arama = arama,
            Kategori = kategori,
            TedarikciId = tedarikciId,
            StokDurumu = stokDurumu,
            AracMarkasi = aracMarkasi,
            AracModeli = aracModeli,
            ParcaTipi = parcaTipi,
            ParcaKoduArama = parcaKoduArama,
            CurrentPage = pagedResult.CurrentPage,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount,
            TotalPages = pagedResult.TotalPages
        };
    }

    private IQueryable<Urun> ApplyAdvancedFilters(
        IQueryable<Urun> query,
        string? arama,
        string? kategori,
        int? tedarikciId,
        string? stokDurumu,
        string? aracMarkasi,
        string? aracModeli,
        ParcaTipi? parcaTipi,
        string? parcaKoduArama)
    {
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var pattern = TurkceHelper.ILikePattern(arama);
            query = query.Where(u =>
                (u.Ad != null && EF.Functions.ILike(u.Ad, pattern)) ||
                (u.Barkod != null && EF.Functions.ILike(u.Barkod, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(kategori))
            query = query.Where(u => u.Kategori == kategori);

        if (tedarikciId.HasValue && tedarikciId.Value > 0)
            query = query.Where(u => u.TedarikciId == tedarikciId);

        if (!string.IsNullOrWhiteSpace(stokDurumu))
        {
            if (stokDurumu == "Kritik")
                query = query.Where(u => u.StokAdedi <= u.MinStokUyari && u.StokAdedi > 0);
            else if (stokDurumu == "Yok")
                query = query.Where(u => u.StokAdedi <= 0);
            else if (stokDurumu == "Var")
                query = query.Where(u => u.StokAdedi > 0);
        }

        if (!string.IsNullOrWhiteSpace(parcaKoduArama))
        {
            var pkPattern = TurkceHelper.ILikePattern(parcaKoduArama);
            query = query.Where(u => u.ParcaKodlari.Any(pk => EF.Functions.ILike(pk.Kod, pkPattern)));
        }

        if (!string.IsNullOrWhiteSpace(aracMarkasi))
        {
            var mPattern = TurkceHelper.ILikePattern(aracMarkasi);
            query = query.Where(u => u.AracMarkasi != null && EF.Functions.ILike(u.AracMarkasi, mPattern));
        }

        if (!string.IsNullOrWhiteSpace(aracModeli))
        {
            var moPattern = TurkceHelper.ILikePattern(aracModeli);
            query = query.Where(u => u.AracModeli != null && EF.Functions.ILike(u.AracModeli, moPattern));
        }

        if (parcaTipi.HasValue)
            query = query.Where(u => u.ParcaTipi == parcaTipi.Value);

        return query;
    }

    public async Task<List<Urun>> GetAllAsync(string? arama = null)
    {
        var query = ApplyAdvancedFilters(_db.Urunler.Include(u => u.Tedarikci).AsQueryable(),
            arama, null, null, null, null, null, null, null);
        return await query.OrderBy(u => u.Ad).ToListAsync();
    }

    public async Task<Urun?> GetByIdAsync(int id)
    {
        return await _db.Urunler
            .Include(u => u.Tedarikci)
            .Include(u => u.ParcaKodlari.OrderBy(pk => pk.KodTipi).ThenBy(pk => pk.Kod))
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<ServiceResult> SaveAsync(UrunVM vm)
    {
        var kodListe = vm.ParcaKodlari?
            .Where(x => !string.IsNullOrWhiteSpace(x.Kod))
            .Select(x => { x.Kod = x.Kod.Trim(); return x; })
            .ToList() ?? new List<ParcaKoduVM>();

        if (kodListe.GroupBy(x => (x.KodTipi, Kod: x.Kod.ToLowerInvariant())).Any(g => g.Count() > 1))
            return ServiceResult.Failure("Aynı parça kodu tipi için yinelenen kod var.");

        if (vm.Id == 0)
        {
            if (!string.IsNullOrWhiteSpace(vm.Barkod))
            {
                var err = await ValidateBarkodUniqueAsync(vm.Barkod, kodListe, excludeUrunId: null);
                if (err != null) return err;
            }
            else
            {
                var err = await ValidateParcaKoduBarkodlariDbAsync(kodListe, excludeUrunId: null);
                if (err != null) return err;
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
                AracMarkasi = string.IsNullOrWhiteSpace(vm.AracMarkasi) ? null : vm.AracMarkasi.Trim(),
                AracModeli = string.IsNullOrWhiteSpace(vm.AracModeli) ? null : vm.AracModeli.Trim(),
                ModelYiliBaslangic = vm.ModelYiliBaslangic,
                ModelYiliBitis = vm.ModelYiliBitis,
                MotorTipi = string.IsNullOrWhiteSpace(vm.MotorTipi) ? null : vm.MotorTipi.Trim(),
                ParcaTipi = vm.ParcaTipi,
                OlusturulmaTarihi = DateTime.UtcNow,
                GuncellenmeTarihi = DateTime.UtcNow
            };

            foreach (var pkVm in kodListe)
            {
                urun.ParcaKodlari.Add(new ParcaKodu
                {
                    KodTipi = pkVm.KodTipi,
                    Kod = pkVm.Kod.Trim(),
                    Aciklama = string.IsNullOrWhiteSpace(pkVm.Aciklama) ? null : pkVm.Aciklama.Trim(),
                    OlusturulmaTarihi = DateTime.UtcNow
                });
            }

            _db.Urunler.Add(urun);
            await _db.SaveChangesAsync();
            return ServiceResult.Success("Ürün başarıyla eklendi.");
        }

        var mevcut = await _db.Urunler.Include(u => u.ParcaKodlari).FirstOrDefaultAsync(u => u.Id == vm.Id);
        if (mevcut == null)
            return ServiceResult.Failure("Ürün bulunamadı.");

        if (!string.IsNullOrWhiteSpace(vm.Barkod))
        {
            var err = await ValidateBarkodUniqueAsync(vm.Barkod, kodListe, excludeUrunId: vm.Id);
            if (err != null) return err;
        }
        else
        {
            var err = await ValidateParcaKoduBarkodlariDbAsync(kodListe, excludeUrunId: vm.Id);
            if (err != null) return err;
        }

        if (vm.StokAdedi < 0)
            return ServiceResult.Failure("Stok adedi 0'ın altına düşemez.");

        mevcut.Ad = vm.Ad.Trim();
        mevcut.Barkod = string.IsNullOrWhiteSpace(vm.Barkod) ? null : vm.Barkod.Trim();
        mevcut.Kategori = vm.Kategori?.Trim() ?? "";
        mevcut.BirimFiyat = vm.BirimFiyat;
        mevcut.KdvOrani = vm.KdvOrani;
        mevcut.AlisFiyati = vm.AlisFiyati;
        mevcut.StokAdedi = vm.StokAdedi;
        mevcut.MinStokUyari = vm.MinStokUyari;
        mevcut.TedarikciId = vm.TedarikciId;
        mevcut.AracMarkasi = string.IsNullOrWhiteSpace(vm.AracMarkasi) ? null : vm.AracMarkasi.Trim();
        mevcut.AracModeli = string.IsNullOrWhiteSpace(vm.AracModeli) ? null : vm.AracModeli.Trim();
        mevcut.ModelYiliBaslangic = vm.ModelYiliBaslangic;
        mevcut.ModelYiliBitis = vm.ModelYiliBitis;
        mevcut.MotorTipi = string.IsNullOrWhiteSpace(vm.MotorTipi) ? null : vm.MotorTipi.Trim();
        mevcut.ParcaTipi = vm.ParcaTipi;
        mevcut.GuncellenmeTarihi = DateTime.UtcNow;

        var guncelVmIdler = kodListe.Where(x => x.Id > 0).Select(x => x.Id).ToHashSet();
        foreach (var silinecek in mevcut.ParcaKodlari.Where(pk => !guncelVmIdler.Contains(pk.Id)).ToList())
            _db.ParcaKodlari.Remove(silinecek);

        foreach (var pkVm in kodListe)
        {
            if (pkVm.Id > 0)
            {
                var pk = mevcut.ParcaKodlari.FirstOrDefault(x => x.Id == pkVm.Id);
                if (pk == null) continue;
                pk.KodTipi = pkVm.KodTipi;
                pk.Kod = pkVm.Kod.Trim();
                pk.Aciklama = string.IsNullOrWhiteSpace(pkVm.Aciklama) ? null : pkVm.Aciklama.Trim();
            }
            else
            {
                mevcut.ParcaKodlari.Add(new ParcaKodu
                {
                    KodTipi = pkVm.KodTipi,
                    Kod = pkVm.Kod.Trim(),
                    Aciklama = string.IsNullOrWhiteSpace(pkVm.Aciklama) ? null : pkVm.Aciklama.Trim(),
                    OlusturulmaTarihi = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
        return ServiceResult.Success("Ürün başarıyla güncellendi.");
    }

    private async Task<ServiceResult?> ValidateBarkodUniqueAsync(string barkod, List<ParcaKoduVM> kodListe, int? excludeUrunId)
    {
        var normalizedBarkod = barkod.Trim().ToLowerInvariant();
        if (await _db.Urunler.AnyAsync(u =>
                u.Barkod != null && u.Barkod.Trim().ToLower() == normalizedBarkod && u.Id != excludeUrunId))
            return ServiceResult.Failure("Bu barkod zaten kayıtlı.");

        return await ValidateParcaKoduBarkodlariDbAsync(kodListe, excludeUrunId);
    }

    private async Task<ServiceResult?> ValidateParcaKoduBarkodlariDbAsync(List<ParcaKoduVM> kodListe, int? excludeUrunId)
    {
        foreach (var pkVm in kodListe.Where(x => x.KodTipi == ParcaKoduTipi.Barkod))
        {
            var n = pkVm.Kod.Trim().ToLowerInvariant();
            var baskaUrunBarkod = await _db.Urunler.AnyAsync(u =>
                u.Id != excludeUrunId && u.Barkod != null && u.Barkod.Trim().ToLower() == n);
            if (baskaUrunBarkod)
                return ServiceResult.Failure($"Parça kodu (barkod) başka bir ürünün barkodu ile çakışıyor: {pkVm.Kod}");

            var baskaParcaBarkod = await _db.ParcaKodlari.AnyAsync(pk =>
                pk.KodTipi == ParcaKoduTipi.Barkod &&
                pk.Kod.Trim().ToLower() == n &&
                pk.UrunId != excludeUrunId);
            if (baskaParcaBarkod)
                return ServiceResult.Failure($"Bu barkod parça kodlarında başka üründe kayıtlı: {pkVm.Kod}");
        }

        return null;
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
        var term = query.Trim().ToLowerInvariant();
        var pattern = TurkceHelper.ILikePattern(query);
        return await _db.Urunler
            .AsNoTracking()
            .Where(u =>
                (u.Ad != null && EF.Functions.ILike(u.Ad, pattern)) ||
                (u.Barkod != null && u.Barkod.ToLower().Contains(term)) ||
                u.ParcaKodlari.Any(pk => pk.Kod.ToLower().Contains(term)))
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

    public async Task<Urun?> GetByParcaKoduAsync(string kod)
    {
        if (string.IsNullOrWhiteSpace(kod)) return null;
        var t = kod.Trim();
        var lower = t.ToLowerInvariant();

        var viaBarkod = await _db.Urunler.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Barkod != null && u.Barkod.Trim().ToLower() == lower);
        if (viaBarkod != null) return viaBarkod;

        return await _db.Urunler.AsNoTracking()
            .Include(u => u.Tedarikci)
            .Include(u => u.ParcaKodlari)
            .FirstOrDefaultAsync(u => u.ParcaKodlari.Any(pk => pk.Kod.Trim().ToLower() == lower));
    }

    public async Task<List<Urun>> GetByIdsAsync(List<int> urunIds)
    {
        return await _db.Urunler
            .AsNoTracking()
            .Where(u => urunIds.Contains(u.Id))
            .OrderBy(u => u.Ad)
            .ToListAsync();
    }

    public async Task<List<ParcaKodu>> GetParcaKodlariAsync(int urunId)
    {
        return await _db.ParcaKodlari.AsNoTracking()
            .Where(pk => pk.UrunId == urunId)
            .OrderBy(pk => pk.KodTipi)
            .ThenBy(pk => pk.Kod)
            .ToListAsync();
    }

    public async Task<ServiceResult> ParcaKoduEkleAsync(int urunId, ParcaKoduVM vm)
    {
        if (!await _db.Urunler.AnyAsync(u => u.Id == urunId))
            return ServiceResult.Failure("Ürün bulunamadı.");

        if (string.IsNullOrWhiteSpace(vm.Kod))
            return ServiceResult.Failure("Kod gereklidir.");

        vm.Kod = vm.Kod.Trim();
        if (vm.KodTipi == ParcaKoduTipi.Barkod)
        {
            var err = await ValidateParcaKoduBarkodlariDbAsync(new List<ParcaKoduVM> { vm }, excludeUrunId: urunId);
            if (err != null) return err;
            var urunBarkod = await _db.Urunler.Where(u => u.Id == urunId).Select(u => u.Barkod).FirstAsync();
            if (!string.IsNullOrWhiteSpace(urunBarkod) &&
                urunBarkod.Trim().ToLowerInvariant() == vm.Kod.ToLowerInvariant())
                return ServiceResult.Failure("Bu barkod ürün barkodu ile zaten aynı.");
        }

        _db.ParcaKodlari.Add(new ParcaKodu
        {
            UrunId = urunId,
            KodTipi = vm.KodTipi,
            Kod = vm.Kod,
            Aciklama = string.IsNullOrWhiteSpace(vm.Aciklama) ? null : vm.Aciklama.Trim(),
            OlusturulmaTarihi = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return ServiceResult.Success("Parça kodu eklendi.");
    }

    public async Task<ServiceResult> ParcaKoduGuncelleAsync(int kodId, ParcaKoduVM vm)
    {
        var pk = await _db.ParcaKodlari.FirstOrDefaultAsync(x => x.Id == kodId);
        if (pk == null)
            return ServiceResult.Failure("Parça kodu bulunamadı.");

        if (string.IsNullOrWhiteSpace(vm.Kod))
            return ServiceResult.Failure("Kod gereklidir.");

        vm.Kod = vm.Kod.Trim();
        if (vm.KodTipi == ParcaKoduTipi.Barkod)
        {
            var n = vm.Kod.ToLowerInvariant();
            if (await _db.Urunler.AnyAsync(u => u.Id != pk.UrunId && u.Barkod != null && u.Barkod.Trim().ToLower() == n))
                return ServiceResult.Failure("Bu barkod başka bir ürünün barkodunda kayıtlı.");
            if (await _db.ParcaKodlari.AnyAsync(x =>
                    x.Id != kodId && x.KodTipi == ParcaKoduTipi.Barkod && x.Kod.Trim().ToLower() == n))
                return ServiceResult.Failure("Bu barkod başka bir parça kodu satırında kayıtlı.");
        }

        pk.KodTipi = vm.KodTipi;
        pk.Kod = vm.Kod;
        pk.Aciklama = string.IsNullOrWhiteSpace(vm.Aciklama) ? null : vm.Aciklama.Trim();
        await _db.SaveChangesAsync();
        return ServiceResult.Success("Parça kodu güncellendi.");
    }

    public async Task<ServiceResult> ParcaKoduSilAsync(int kodId)
    {
        var pk = await _db.ParcaKodlari.FirstOrDefaultAsync(x => x.Id == kodId);
        if (pk == null)
            return ServiceResult.Failure("Parça kodu bulunamadı.");

        _db.ParcaKodlari.Remove(pk);
        await _db.SaveChangesAsync();
        return ServiceResult.Success("Parça kodu silindi.");
    }
}
