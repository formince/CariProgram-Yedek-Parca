using CariErinc.Data;
using CariErinc.Helpers;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class MusteriService : IMusteriService
{
    private readonly IAuditLogService _auditLog;
    private readonly AppDbContext _db;

    public MusteriService(IAuditLogService auditLog, AppDbContext db)
    {
        _auditLog = auditLog;
        _db = db;
    }

    public async Task<MusteriIndexVM> GetPagedListAsync(int page = 1, string? arama = null)
    {
        int pageSize = 30;
        var query = ApplyAramaFilters(_db.Musteriler.OrderBy(m => m.Ad).ThenBy(m => m.Soyad).AsQueryable(), arama);
        
        var pagedResult = await query.ToPagedListAsync(page, pageSize);

        return new MusteriIndexVM
        {
            Musteriler = pagedResult.Items,
            Arama = arama,
            CurrentPage = pagedResult.CurrentPage,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount,
            TotalPages = pagedResult.TotalPages
        };
    }

    private IQueryable<Musteri> ApplyAramaFilters(IQueryable<Musteri> query, string? arama)
    {
        if (!string.IsNullOrWhiteSpace(arama))
        {
            var pattern = TurkceHelper.ILikePattern(arama);
            query = query.Where(m =>
                (m.Ad != null && EF.Functions.ILike(m.Ad, pattern)) ||
                (m.Soyad != null && EF.Functions.ILike(m.Soyad, pattern)) ||
                (m.Telefon != null && EF.Functions.ILike(m.Telefon, pattern)));
        }
        return query;
    }

    public async Task<List<Musteri>> GetAllAsync(string? arama = null)
    {
        var query = ApplyAramaFilters(_db.Musteriler.AsQueryable(), arama);
        return await query.OrderBy(m => m.Ad).ThenBy(m => m.Soyad).ToListAsync();
    }

    public async Task<Musteri?> GetByIdAsync(int id)
    {
        return await _db.Musteriler.Include(m => m.Cari).FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<MusteriDetayVM?> GetDetayVMAsync(int id)
    {
        var musteri = await _db.Musteriler
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (musteri == null) return null;

        var satislar = await _db.Satislar
            .Where(s => s.MusteriId == id)
            .Include(s => s.SatisDetaylari)
            .OrderByDescending(s => s.Tarih)
            .AsNoTracking()
            .ToListAsync();

        var veresiyeler = await _db.Veresiyeler
            .Where(v => v.MusteriId == id)
            .Include(v => v.Odemeler)
            .OrderByDescending(v => v.Tarih)
            .AsNoTracking()
            .ToListAsync();

        return new MusteriDetayVM
        {
            Id = musteri.Id,
            AdSoyad = $"{musteri.Ad} {musteri.Soyad}".Trim(),
            Telefon = musteri.Telefon,
            Adres = musteri.Adres,
            ToplamBorc = veresiyeler
                .Where(v => v.OdenmeDurumu != OdenmeDurumu.Iptal)
                .Sum(v => Math.Max(0, v.Tutar - v.Odemeler.Sum(o => o.OdemeTutari))),
            ToplamSatisSayisi = satislar.Count(s => !s.IptalEdildi),
            ToplamSatisTutari = satislar.Where(s => !s.IptalEdildi).Sum(s => s.ToplamTutar),
            ToplamOdenenVeresiye = veresiyeler.Where(v => v.OdenmeDurumu != OdenmeDurumu.Iptal).SelectMany(v => v.Odemeler).Sum(o => o.OdemeTutari),
            SonSatislar = satislar.Select(s => new MusteriSatisOzetVM
            {
                Id = s.Id,
                Tarih = s.Tarih,
                OdemeTipi = s.OdemeTipi.ToString(),
                ToplamTutar = s.ToplamTutar,
                IptalEdildi = s.IptalEdildi,
                UrunSayisi = s.SatisDetaylari.Count
            }).ToList(),
            AcikVeresiyeler = veresiyeler.Select(v => new MusteriVeresiyeOzetVM
            {
                Id = v.Id,
                Tarih = v.Tarih,
                Tutar = v.Tutar,
                OdenenTutar = v.Odemeler.Sum(o => o.OdemeTutari),
                KalanBorc = v.OdenmeDurumu == OdenmeDurumu.Iptal ? 0 : v.Tutar - v.Odemeler.Sum(o => o.OdemeTutari),
                Durum = v.OdenmeDurumu.ToString(),
                SatisId = v.SatisId,
                Tip = v.Tip.ToString()
            }).ToList(),
            Hareketler = BuildMusteriHareketleri(veresiyeler, satislar)
        };
    }

    private static List<HareketVM> BuildMusteriHareketleri(
        IReadOnlyList<Veresiye> veresiyeler,
        IReadOnlyList<Satis> satislar)
    {
        var satisIdlerVeresiyede = veresiyeler
            .Where(v => v.SatisId.HasValue)
            .Select(v => v.SatisId!.Value)
            .ToHashSet();

        var fromVeresiye = veresiyeler.Select(v => new HareketVM
        {
            Tip = HaraketTip.Veresiye,
            Id = v.Id,
            Tarih = v.Tarih,
            Tutar = v.Tutar,
            KalanBorc = v.OdenmeDurumu == OdenmeDurumu.Iptal ? 0 : v.Tutar - v.Odemeler.Sum(o => o.OdemeTutari),
            Durum = v.OdenmeDurumu.ToString(),
            VeresiyeTip = v.Tip.ToString(),
            SatisId = v.SatisId,
            Odemeler = v.Odemeler.Select(o => new VeresiyeOdemeBilgiVM
            {
                Tutar = o.OdemeTutari,
                Tarih = o.OdemeTarihi,
                KullaniciId = o.KullaniciId,
                OdemeTipi = o.OdemeTipi.ToString()
            }).ToList()
        });

        var fromSatis = satislar
            .Where(s => !satisIdlerVeresiyede.Contains(s.Id))
            .Select(s => new HareketVM
            {
                Tip = HaraketTip.Satis,
                Id = s.Id,
                Tarih = s.Tarih,
                SatisTutar = s.ToplamTutar,
                OdemeTipi = s.OdemeTipi.ToString(),
                IptalEdildi = s.IptalEdildi
            });

        return fromVeresiye.Concat(fromSatis)
            .OrderByDescending(h => h.Tarih)
            .ToList();
    }

    public async Task<ServiceResult> SaveAsync(MusteriVM vm)
    {
        if (vm.Id == 0) // Ekleme
        {
            var musteri = new Musteri
            {
                Ad = vm.Ad?.Trim() ?? string.Empty,
                Soyad = vm.Soyad?.Trim() ?? string.Empty,
                Telefon = vm.Telefon?.Trim(),
                Adres = vm.Adres?.Trim(),
                OlusturulmaTarihi = DateTime.UtcNow
            };

            if (string.IsNullOrWhiteSpace(musteri.Ad))
                return ServiceResult.Failure("Müşteri adı gereklidir.");

            var cari = new Cari
            {
                Ad = $"{musteri.Ad} {musteri.Soyad}".Trim(),
                Telefon = musteri.Telefon,
                Adres = musteri.Adres,
                Rol = CariRol.Musteri,
                OlusturulmaTarihi = DateTime.UtcNow
            };
            _db.Cariler.Add(cari);
            await _db.SaveChangesAsync();
            musteri.CariId = cari.Id;

            _db.Musteriler.Add(musteri);
            _auditLog.LogHazirla("Musteri", musteri.Id, "Eklendi", yeniDeger: musteri);
            await _db.SaveChangesAsync();
            return ServiceResult.Success("Müşteri başarıyla eklendi.");
        }
        else // Güncelleme
        {
            var musteri = await _db.Musteriler.FindAsync(vm.Id);
            if (musteri == null)
                return ServiceResult.Failure("Müşteri bulunamadı.");

            // Log için eski değerleri al
            var eskiMusteri = new Musteri 
            { 
                Ad = musteri.Ad, 
                Soyad = musteri.Soyad, 
                Telefon = musteri.Telefon, 
                Adres = musteri.Adres 
            };

            musteri.Ad = vm.Ad?.Trim() ?? string.Empty;
            musteri.Soyad = vm.Soyad?.Trim() ?? string.Empty;
            musteri.Telefon = vm.Telefon?.Trim();
            musteri.Adres = vm.Adres?.Trim();

            if (!musteri.CariId.HasValue)
            {
                var cari = new Cari
                {
                    Ad = $"{musteri.Ad} {musteri.Soyad}".Trim(),
                    Telefon = musteri.Telefon,
                    Adres = musteri.Adres,
                    Rol = CariRol.Musteri,
                    OlusturulmaTarihi = DateTime.UtcNow
                };
                _db.Cariler.Add(cari);
                await _db.SaveChangesAsync();
                musteri.CariId = cari.Id;
            }
            else
            {
                var cari = await _db.Cariler.FindAsync(musteri.CariId.Value);
                if (cari != null)
                {
                    cari.Ad = $"{musteri.Ad} {musteri.Soyad}".Trim();
                    cari.Telefon = musteri.Telefon;
                    cari.Adres = musteri.Adres;
                    cari.Rol |= CariRol.Musteri;
                }
            }

            if (string.IsNullOrWhiteSpace(musteri.Ad))
                return ServiceResult.Failure("Müşteri adı gereklidir.");

            _db.Musteriler.Update(musteri);
            _auditLog.LogHazirla("Musteri", musteri.Id, "Guncellendi", eskiDeger: eskiMusteri, yeniDeger: musteri);
            await _db.SaveChangesAsync();
            return ServiceResult.Success("Müşteri başarıyla güncellendi.");
        }
    }

    public async Task<ServiceResult> SilAsync(int id)
    {
        if (await _db.Veresiyeler.AnyAsync(v => v.MusteriId == id))
            return ServiceResult.Failure("Bu müşterinin veresiyesi bulunduğu için silinemez.");

        var musteri = await _db.Musteriler.FindAsync(id);
        if (musteri == null)
            return ServiceResult.Failure("Müşteri bulunamadı.");

        _db.Musteriler.Remove(musteri);
        _auditLog.LogHazirla("Musteri", id, "Silindi", eskiDeger: musteri);
        await _db.SaveChangesAsync();
        return ServiceResult.Success("Müşteri başarıyla silindi.");
    }

    public async Task<List<Musteri>> SearchAsync(string query, int limit)
    {
        var q = ApplyAramaFilters(_db.Musteriler.AsQueryable(), query);
        return await q.OrderBy(m => m.Ad).ThenBy(m => m.Soyad).Take(limit).ToListAsync();
    }
}
