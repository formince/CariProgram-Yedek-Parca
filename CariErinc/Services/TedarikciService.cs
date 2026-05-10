using CariErinc.Data;
using CariErinc.Helpers;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class TedarikciService : ITedarikciService
{
    private readonly AppDbContext _db;

    public TedarikciService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Tedarikci>> GetAllAsync(string? arama)
    {
        var query = _db.Tedarikciler.AsQueryable();

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var pattern = TurkceHelper.ILikePattern(arama);
            query = query.Where(t =>
                (t.Ad != null && EF.Functions.ILike(t.Ad, pattern)) ||
                (t.YetkiliKisi != null && EF.Functions.ILike(t.YetkiliKisi, pattern)) ||
                (t.Telefon != null && EF.Functions.ILike(t.Telefon, pattern)));
        }

        return await query.OrderBy(t => t.Ad).ToListAsync();
    }

    public async Task<Tedarikci?> GetByIdAsync(int id)
    {
        return await _db.Tedarikciler.Include(t => t.Cari).FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<TedarikciDetayVM?> GetDetayVMAsync(int id)
    {
        var tedarikci = await _db.Tedarikciler
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tedarikci == null) return null;

        var alislar = await _db.Alislar
            .Where(a => a.TedarikciId == id)
            .Include(a => a.AlisDetaylari)
            .Include(a => a.AlisOdemeleri)
            .OrderByDescending(a => a.Tarih)
            .AsNoTracking()
            .ToListAsync();

        return new TedarikciDetayVM
        {
            Id = tedarikci.Id,
            Ad = tedarikci.Ad,
            YetkiliKisi = tedarikci.YetkiliKisi,
            Telefon = tedarikci.Telefon,
            Adres = tedarikci.Adres,
            ToplamAlisSayisi = alislar.Count,
            ToplamAlisTutari = alislar.Sum(a => a.ToplamTutar),
            ToplamOdenen = alislar.Sum(a => a.OdenenTutar),
            ToplamKalanBorc = alislar.Sum(CalculateAlisKalanBorc),
            SonAlislar = alislar.Select(a => new TedarikciAlisOzetVM
            {
                Id = a.Id,
                Tarih = a.Tarih,
                OdemeTipi = a.OdemeTipi.ToString(),
                ToplamTutar = a.ToplamTutar,
                KalanBorc = CalculateAlisKalanBorc(a),
                VadeTarihi = a.VadeTarihi,
                UrunSayisi = a.AlisDetaylari.Count
            }).ToList()
        };
    }

    private static decimal CalculateAlisKalanBorc(Alis alis)
    {
        return AlisBorcHesaplayici.CalculateKalanBorc(alis);
    }

    public async Task<ServiceResult> SaveAsync(TedarikciVM vm)
    {
        if (vm.Id == 0) // Ekleme
        {
            var tedarikci = new Tedarikci
            {
                Ad = vm.Ad.Trim(),
                YetkiliKisi = vm.YetkiliKisi?.Trim(),
                Telefon = vm.Telefon?.Trim(),
                Adres = vm.Adres?.Trim(),
                OlusturulmaTarihi = DateTime.UtcNow
            };

            var cari = new Cari
            {
                Ad = tedarikci.Ad,
                YetkiliKisi = tedarikci.YetkiliKisi,
                Telefon = tedarikci.Telefon,
                Adres = tedarikci.Adres,
                Rol = CariRol.Tedarikci,
                OlusturulmaTarihi = DateTime.UtcNow
            };
            _db.Cariler.Add(cari);
            await _db.SaveChangesAsync();
            tedarikci.CariId = cari.Id;

            _db.Tedarikciler.Add(tedarikci);
            await _db.SaveChangesAsync();
            return ServiceResult.Success("Tedarikçi başarıyla eklendi.");
        }
        else // Güncelleme
        {
            var tedarikci = await _db.Tedarikciler.FindAsync(vm.Id);
            if (tedarikci == null)
                return ServiceResult.Failure("Tedarikçi bulunamadı.");

            tedarikci.Ad = vm.Ad.Trim();
            tedarikci.YetkiliKisi = vm.YetkiliKisi?.Trim();
            tedarikci.Telefon = vm.Telefon?.Trim();
            tedarikci.Adres = vm.Adres?.Trim();

            if (!tedarikci.CariId.HasValue)
            {
                var cari = new Cari
                {
                    Ad = tedarikci.Ad,
                    YetkiliKisi = tedarikci.YetkiliKisi,
                    Telefon = tedarikci.Telefon,
                    Adres = tedarikci.Adres,
                    Rol = CariRol.Tedarikci,
                    OlusturulmaTarihi = DateTime.UtcNow
                };
                _db.Cariler.Add(cari);
                await _db.SaveChangesAsync();
                tedarikci.CariId = cari.Id;
            }
            else
            {
                var cari = await _db.Cariler.FindAsync(tedarikci.CariId.Value);
                if (cari != null)
                {
                    cari.Ad = tedarikci.Ad;
                    cari.YetkiliKisi = tedarikci.YetkiliKisi;
                    cari.Telefon = tedarikci.Telefon;
                    cari.Adres = tedarikci.Adres;
                    cari.Rol |= CariRol.Tedarikci;
                }
            }

            _db.Tedarikciler.Update(tedarikci);
            await _db.SaveChangesAsync();
            return ServiceResult.Success("Tedarikçi başarıyla güncellendi.");
        }
    }

    public async Task<ServiceResult> SilAsync(int id)
    {
        if (await _db.Alislar.AnyAsync(a => a.TedarikciId == id))
            return ServiceResult.Failure("Bu tedarikçinin alış kaydı bulunduğu için silinemez.");

        var tedarikci = await _db.Tedarikciler.FindAsync(id);
        if (tedarikci == null)
            return ServiceResult.Failure("Tedarikçi bulunamadı.");

        _db.Tedarikciler.Remove(tedarikci);
        await _db.SaveChangesAsync();
        return ServiceResult.Success("Tedarikçi başarıyla silindi.");
    }
}
