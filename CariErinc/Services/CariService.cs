using CariErinc.Data;
using CariErinc.Helpers;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class CariService : ICariService
{
    private readonly AppDbContext _db;

    public CariService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CariIndexVM> GetIndexVMAsync(string? arama)
    {
        var cariSorgu = _db.Cariler
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var pattern = $"%{arama.Trim()}%";
            cariSorgu = cariSorgu.Where(x =>
                EF.Functions.ILike(x.Ad, pattern) ||
                (x.YetkiliKisi != null && EF.Functions.ILike(x.YetkiliKisi, pattern)) ||
                (x.Telefon != null && EF.Functions.ILike(x.Telefon, pattern)));
        }

        var cariler = await cariSorgu.ToListAsync();
        var satirlar = await BuildSatirlarAsync(cariler);

        return new CariIndexVM
        {
            Arama = arama,
            Satirlar = satirlar,
            Dogrulama = await BuildDogrulamaVmAsync(satirlar)
        };
    }

    public async Task<CariDetayVM?> GetDetayVMAsync(int cariId, DateTime? baslangic = null, DateTime? bitis = null)
    {
        var cari = await _db.Cariler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cariId);
        if (cari == null) return null;

        var musteri = await _db.Musteriler.AsNoTracking().FirstOrDefaultAsync(x => x.CariId == cariId);
        var tedarikci = await _db.Tedarikciler.AsNoTracking().FirstOrDefaultAsync(x => x.CariId == cariId);

        var satislar = await _db.Satislar
            .AsNoTracking()
            .Where(x => x.CariId == cariId && !x.IptalEdildi)
            .Include(x => x.SatisDetaylari)
            .OrderByDescending(x => x.Tarih)
            .ToListAsync();

        var veresiyeler = await _db.Veresiyeler
            .AsNoTracking()
            .Where(x => x.CariId == cariId)
            .Include(x => x.Odemeler)
            .OrderByDescending(x => x.Tarih)
            .ToListAsync();

        var alislar = await _db.Alislar
            .AsNoTracking()
            .Where(x => x.CariId == cariId)
            .Include(x => x.AlisDetaylari)
            .Include(x => x.AlisOdemeleri)
            .OrderByDescending(x => x.Tarih)
            .ToListAsync();

        // Veresiyeleri Tip'e göre ayır: Borç (SatisBagli/Elden) ve Avans
        var borclar = veresiyeler
            .Where(v => v.Tip != VeresiyeTipi.Avans && v.OdenmeDurumu != OdenmeDurumu.Iptal)
            .Select(v => new CariVeresiyeSatirVM
            {
                Id = v.Id,
                Tarih = v.Tarih,
                Tutar = v.Tutar,
                OdenenTutar = v.Odemeler.Sum(o => o.OdemeTutari),
                KalanBorc = CalculateVeresiyeKalan(v),
                Durum = v.OdenmeDurumu.ToString(),
                Tip = "Borc"
            })
            .Where(x => x.KalanBorc > 0)
            .ToList();

        var avanslar = veresiyeler
            .Where(v => v.Tip == VeresiyeTipi.Avans && v.OdenmeDurumu != OdenmeDurumu.Iptal)
            .Select(v => new CariVeresiyeSatirVM
            {
                Id = v.Id,
                Tarih = v.Tarih,
                Tutar = v.Tutar,
                OdenenTutar = v.Odemeler.Sum(o => o.OdemeTutari),
                KalanBorc = CalculateVeresiyeKalan(v),
                Durum = v.OdenmeDurumu.ToString(),
                Tip = "Avans"
            })
            .ToList();

        // Tüm tahsilatları (borç ödemeleri + avans yatırmaları) tek listede
        var odemeler = veresiyeler
            .SelectMany(v => v.Odemeler.Select(o => new CariOdemeSatirVM
            {
                Id = o.Id,
                Tarih = o.OdemeTarihi,
                Tutar = o.OdemeTutari,
                KullaniciId = o.KullaniciId,
                OdemeTipi = o.OdemeTipi.ToString(),
                Aciklama = o.Aciklama,
                VeresiyeId = v.Id,
                VeresiyeTip = v.Tip.ToString()
            }))
            .OrderByDescending(o => o.Tarih)
            .ToList();

        // Kümülatif özet (tarihe göre filtrelenebilir)
        // Borç ödemeleri ve avans kullanımları tarihe göre filtrelenir (VeresiyeOdeme.OdemeTarihi)
        // Avans yatırmaları tarihe göre filtrelenir (Veresiye.Tarih — avans oluşturulma anı)
        bool TarihGecerliOdemeler(CariOdemeSatirVM o) =>
            (!baslangic.HasValue || o.Tarih >= baslangic.Value.Date) &&
            (!bitis.HasValue || o.Tarih < bitis.Value.Date.AddDays(1));

        bool TarihGecerliAvans(CariVeresiyeSatirVM a) =>
            (!baslangic.HasValue || a.Tarih >= baslangic.Value.Date) &&
            (!bitis.HasValue || a.Tarih < bitis.Value.Date.AddDays(1));

        var toplamBorcOdemesi = odemeler.Where(o => o.VeresiyeTip != VeresiyeTipi.Avans.ToString() && TarihGecerliOdemeler(o)).Sum(o => o.Tutar);
        var toplamAvansYatirma = avanslar.Where(TarihGecerliAvans).Sum(x => x.Tutar);
        var toplamAvansKullanimi = odemeler.Where(o => o.VeresiyeTip == VeresiyeTipi.Avans.ToString() && TarihGecerliOdemeler(o)).Sum(o => o.Tutar);

        var acikVadeliAlislar = alislar
            .Where(a => a.OdemeTipi == AlisOdemeTipi.Vadeli)
            .Select(a =>
            {
                var hesaplananKalan = CalculateAlisKalan(a);
                return new CariAlisSatirVM
                {
                    Id = a.Id,
                    Tarih = a.Tarih,
                    ToplamTutar = a.ToplamTutar,
                    OdenenTutar = a.OdenenTutar,
                    KalanBorc = hesaplananKalan,
                    OdemeTipi = a.OdemeTipi.ToString()
                };
            })
            .Where(x => x.KalanBorc > 0)
            .ToList();

        return new CariDetayVM
        {
            Id = cari.Id,
            Ad = cari.Ad,
            Telefon = cari.Telefon,
            Adres = cari.Adres,
            RolEtiketi = BuildRolEtiketi(cari.Rol),
            MusteriId = musteri?.Id,
            TedarikciId = tedarikci?.Id,
            AlacakToplam = borclar.Sum(x => x.KalanBorc),
            AvansToplam = avanslar.Sum(x => x.KalanBorc),
            VerecekToplam = acikVadeliAlislar.Sum(x => x.KalanBorc),
            ToplamSatisSayisi = satislar.Count,
            ToplamSatisTutari = satislar.Sum(x => x.ToplamTutar),
            ToplamAlisSayisi = alislar.Count,
            ToplamAlisTutari = alislar.Sum(x => x.ToplamTutar),
            ToplamOdenenVeresiye = odemeler.Where(o => o.VeresiyeTip != VeresiyeTipi.Avans.ToString() && TarihGecerliOdemeler(o)).Sum(o => o.Tutar),
            ToplamOdenenAlis = alislar.Sum(x => x.OdenenTutar),
            ToplamBorcOdemesi = toplamBorcOdemesi,
            ToplamAvansYatirma = toplamAvansYatirma,
            ToplamAvansKullanimi = toplamAvansKullanimi,
            Baslangic = baslangic,
            Bitis = bitis,
            Borclar = borclar,
            Avanslar = avanslar,
            Odemeler = odemeler,
            AcikVadeliAlislar = acikVadeliAlislar,
            Hareketler = new List<CariHareketSatirVM>()
        };
    }

    public async Task<CariEkstreVM?> GetEkstreVMAsync(int cariId, int page = 1, DateTime? baslangic = null, DateTime? bitis = null)
    {
        var cari = await _db.Cariler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cariId);
        if (cari == null) return null;

        var musteri = await _db.Musteriler.AsNoTracking().FirstOrDefaultAsync(x => x.CariId == cariId);
        var tedarikci = await _db.Tedarikciler.AsNoTracking().FirstOrDefaultAsync(x => x.CariId == cariId);

        var satislar = await _db.Satislar
            .AsNoTracking()
            .Where(s => !s.IptalEdildi && (s.CariId == cariId || (musteri != null && s.MusteriId == musteri.Id)))
            .ToListAsync();

        var veresiyeler = await _db.Veresiyeler
            .AsNoTracking()
            .Where(v => v.CariId == cariId || (musteri != null && v.MusteriId == musteri.Id))
            .Include(v => v.Odemeler)
            .ToListAsync();

        var alislar = await _db.Alislar
            .AsNoTracking()
            .Where(a => a.CariId == cariId || (tedarikci != null && a.TedarikciId == tedarikci.Id))
            .Include(a => a.AlisOdemeleri)
            .ToListAsync();

        var acikVeresiyelerList = veresiyeler
            .Where(v => v.OdenmeDurumu != OdenmeDurumu.Iptal)
            .Select(v =>
            {
                var kalan = CalculateVeresiyeKalan(v);
                return new { v.Id, KalanBorc = kalan };
            })
            .Where(x => x.KalanBorc > 0)
            .ToList();

        var acikVadeliList = alislar
            .Where(a => a.OdemeTipi == AlisOdemeTipi.Vadeli)
            .Select(a => new { a.Id, Kalan = CalculateAlisKalan(a) })
            .Where(x => x.Kalan > 0)
            .ToList();

        var alacakToplam = acikVeresiyelerList.Sum(x => x.KalanBorc);
        var verecekToplam = acikVadeliList.Sum(x => x.Kalan);

        var rows = new List<CariEkstreSatirVM>();

        foreach (var s in satislar)
        {
            var tip = s.OdemeTipi == OdemeTipi.Pesin ? "Satış (Peşin)" : "Satış (Veresiye)";
            rows.Add(new CariEkstreSatirVM
            {
                Tarih = s.Tarih,
                Taraf = "Satış",
                IslemTipi = tip,
                Tutar = s.ToplamTutar,
                Aciklama = s.Aciklama,
                Kaynak = "Satis",
                KaynakId = s.Id
            });
        }

        foreach (var v in veresiyeler.Where(x => x.OdenmeDurumu != OdenmeDurumu.Iptal && x.SatisId == null))
        {
            var islemTipi = v.Tip == VeresiyeTipi.Avans ? "Avans" : "Veresiye (borç)";
            var taraf = v.Tip == VeresiyeTipi.Avans ? "Avans" : "Alacak";
            rows.Add(new CariEkstreSatirVM
            {
                Tarih = v.Tarih,
                Taraf = taraf,
                IslemTipi = islemTipi,
                Tutar = v.Tutar,
                Aciklama = v.Aciklama,
                Kaynak = "Veresiye",
                KaynakId = v.Id
            });
        }

        foreach (var v in veresiyeler.Where(x => x.OdenmeDurumu != OdenmeDurumu.Iptal))
        {
            foreach (var o in v.Odemeler)
            {
                rows.Add(new CariEkstreSatirVM
                {
                    Tarih = o.OdemeTarihi,
                    Taraf = "Tahsilat",
                    IslemTipi = "Veresiye tahsilatı",
                    Tutar = o.OdemeTutari,
                    Aciklama = string.IsNullOrWhiteSpace(o.Aciklama) ? $"Veresiye #{v.Id}" : o.Aciklama,
                    Kaynak = "VeresiyeOdeme",
                    KaynakId = o.Id,
                    BagliVeresiyeId = v.Id
                });
            }
        }

        foreach (var a in alislar)
        {
            var tip = a.OdemeTipi == AlisOdemeTipi.Nakit ? "Alış (Nakit)" : "Alış (Vadeli)";
            rows.Add(new CariEkstreSatirVM
            {
                Tarih = a.Tarih,
                Taraf = "Verecek",
                IslemTipi = tip,
                Tutar = a.ToplamTutar,
                Aciklama = a.Aciklama,
                Kaynak = "Alis",
                KaynakId = a.Id
            });
        }

        foreach (var a in alislar)
        {
            foreach (var o in a.AlisOdemeleri)
            {
                rows.Add(new CariEkstreSatirVM
                {
                    Tarih = o.OdemeTarihi,
                    Taraf = "Ödeme",
                    IslemTipi = "Alış ödemesi",
                    Tutar = o.OdemeTutari,
                    Aciklama = string.IsNullOrWhiteSpace(o.Aciklama) ? $"Alış #{a.Id}" : o.Aciklama,
                    Kaynak = "AlisOdeme",
                    KaynakId = o.Id,
                    BagliAlisId = a.Id
                });
            }
        }

        IEnumerable<CariEkstreSatirVM> filtered = rows;
        if (baslangic.HasValue)
            filtered = filtered.Where(r => r.Tarih >= baslangic.Value.Date);
        if (bitis.HasValue)
            filtered = filtered.Where(r => r.Tarih < bitis.Value.Date.AddDays(1));

        var ordered = filtered
            .OrderByDescending(r => r.Tarih)
            .ToList();

        const int pageSize = 30;
        page = page < 1 ? 1 : page;
        var totalCount = ordered.Count;
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
        if (page > totalPages) page = totalPages;

        var slice = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new CariEkstreVM
        {
            CariId = cari.Id,
            Ad = cari.Ad,
            RolEtiketi = BuildRolEtiketi(cari.Rol),
            Telefon = cari.Telefon,
            Adres = cari.Adres,
            AlacakToplam = alacakToplam,
            VerecekToplam = verecekToplam,
            Baslangic = baslangic?.ToString("yyyy-MM-dd"),
            Bitis = bitis?.ToString("yyyy-MM-dd"),
            Satirlar = slice,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            PageSize = pageSize
        };
    }

    public async Task<CariFormVM?> GetFormVMAsync(int id)
    {
        var cari = await _db.Cariler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (cari == null) return null;

        return new CariFormVM
        {
            Id = cari.Id,
            Ad = cari.Ad,
            YetkiliKisi = cari.YetkiliKisi,
            Telefon = cari.Telefon,
            Adres = cari.Adres,
            AktifMi = cari.AktifMi,
            MusteriRol = cari.Rol.HasFlag(CariRol.Musteri),
            TedarikciRol = cari.Rol.HasFlag(CariRol.Tedarikci)
        };
    }

    public async Task<ServiceResult> SaveAsync(CariFormVM vm)
    {
        var rol = BuildRol(vm);
        if (rol == CariRol.Yok)
            return ServiceResult.Failure("En az bir rol seçiniz.");

        if (string.IsNullOrWhiteSpace(vm.Ad))
            return ServiceResult.Failure("Cari adı zorunludur.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            Cari cari;
            if (vm.Id <= 0)
            {
                cari = new Cari
                {
                    Ad = vm.Ad.Trim(),
                    YetkiliKisi = vm.YetkiliKisi?.Trim(),
                    Telefon = vm.Telefon?.Trim(),
                    Adres = vm.Adres?.Trim(),
                    AktifMi = vm.AktifMi,
                    Rol = rol,
                    OlusturulmaTarihi = DateTime.UtcNow
                };

                _db.Cariler.Add(cari);
                await _db.SaveChangesAsync();
            }
            else
            {
                cari = await _db.Cariler.FirstOrDefaultAsync(x => x.Id == vm.Id) ?? throw new InvalidOperationException("Cari bulunamadı.");
                cari.Ad = vm.Ad.Trim();
                cari.YetkiliKisi = vm.YetkiliKisi?.Trim();
                cari.Telefon = vm.Telefon?.Trim();
                cari.Adres = vm.Adres?.Trim();
                cari.AktifMi = vm.AktifMi;
                cari.Rol = rol;
            }

            var syncResult = await SyncToLegacyAsync(cari);
            if (!syncResult.IsSuccess)
            {
                await tx.RollbackAsync();
                return syncResult;
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return ServiceResult.Success(vm.Id <= 0 ? "Cari eklendi." : "Cari güncellendi.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<ServiceResult> SilAsync(int id)
    {
        var cari = await _db.Cariler.FirstOrDefaultAsync(x => x.Id == id);
        if (cari == null) return ServiceResult.Failure("Cari bulunamadı.");

        var musteri = await _db.Musteriler.FirstOrDefaultAsync(x => x.CariId == id);
        if (musteri != null)
        {
            if (await HasMusteriHareketAsync(musteri.Id))
                return ServiceResult.Failure("Bu cari müşteri hareketleri içeriyor; silinemez.");
            _db.Musteriler.Remove(musteri);
        }

        var tedarikci = await _db.Tedarikciler.FirstOrDefaultAsync(x => x.CariId == id);
        if (tedarikci != null)
        {
            if (await HasTedarikciHareketAsync(tedarikci.Id))
                return ServiceResult.Failure("Bu cari tedarikçi hareketleri içeriyor; silinemez.");
            if (await _db.Urunler.AnyAsync(x => x.TedarikciId == tedarikci.Id))
                return ServiceResult.Failure("Bu cariye bağlı ürün bulunduğu için silinemez.");
            _db.Tedarikciler.Remove(tedarikci);
        }

        _db.Cariler.Remove(cari);
        await _db.SaveChangesAsync();
        return ServiceResult.Success("Cari silindi.");
    }

    private async Task<List<CariSatirVM>> BuildSatirlarAsync(List<Cari> cariler)
    {
        var cariIdler = cariler.Select(x => x.Id).ToList();

        var musteriIdMap = await _db.Musteriler
            .AsNoTracking()
            .Where(x => x.CariId.HasValue && cariIdler.Contains(x.CariId.Value))
            .ToDictionaryAsync(x => x.Id, x => x.CariId!.Value);

        var tedarikciIdMap = await _db.Tedarikciler
            .AsNoTracking()
            .Where(x => x.CariId.HasValue && cariIdler.Contains(x.CariId.Value))
            .ToDictionaryAsync(x => x.Id, x => x.CariId!.Value);

        var musteriIdler = musteriIdMap.Keys.ToList();
        var tedarikciIdler = tedarikciIdMap.Keys.ToList();

        var veresiyeKalemleri = await _db.Veresiyeler
            .AsNoTracking()
            .Where(v =>
                (v.CariId.HasValue && cariIdler.Contains(v.CariId.Value)) ||
                (!v.CariId.HasValue && musteriIdler.Contains(v.MusteriId)))
            .Include(v => v.Odemeler)
            .Where(v => v.OdenmeDurumu != OdenmeDurumu.Iptal)
            .ToListAsync();

        // Borç (Tip != Avans) ve Avans (Tip == Avans) ayrı map'lere
        var alacakMap = veresiyeKalemleri
            .Where(v => v.Tip != VeresiyeTipi.Avans)
            .Select(v =>
            {
                var hedefCariId = v.CariId ?? (musteriIdMap.TryGetValue(v.MusteriId, out var mappedCariId) ? mappedCariId : (int?)null);
                return new { Kalem = v, HedefCariId = hedefCariId };
            })
            .Where(x => x.HedefCariId.HasValue)
            .GroupBy(x => x.HedefCariId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => CalculateVeresiyeKalan(x.Kalem)));

        var avansMap = veresiyeKalemleri
            .Where(v => v.Tip == VeresiyeTipi.Avans)
            .Select(v =>
            {
                var hedefCariId = v.CariId ?? (musteriIdMap.TryGetValue(v.MusteriId, out var mappedCariId) ? mappedCariId : (int?)null);
                return new { Kalem = v, HedefCariId = hedefCariId };
            })
            .Where(x => x.HedefCariId.HasValue)
            .GroupBy(x => x.HedefCariId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => CalculateVeresiyeKalan(x.Kalem)));

        var alisKalemleri = await _db.Alislar
            .AsNoTracking()
            .Where(a =>
                (a.CariId.HasValue && cariIdler.Contains(a.CariId.Value)) ||
                (!a.CariId.HasValue && tedarikciIdler.Contains(a.TedarikciId)))
            .Where(a => a.OdemeTipi == AlisOdemeTipi.Vadeli)
            .ToListAsync();

        var verecekMap = alisKalemleri
            .Select(a =>
            {
                var hedefCariId = a.CariId ?? (tedarikciIdMap.TryGetValue(a.TedarikciId, out var mappedCariId) ? mappedCariId : (int?)null);
                var hesaplananKalan = CalculateAlisKalan(a);
                return new { HedefCariId = hedefCariId, Kalan = hesaplananKalan };
            })
            .Where(x => x.HedefCariId.HasValue && x.Kalan > 0)
            .GroupBy(x => x.HedefCariId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Kalan));

        var satirlar = cariler
            .Select(c => new CariSatirVM
            {
                CariId = c.Id,
                Ad = c.Ad,
                Telefon = c.Telefon,
                Alacak = alacakMap.TryGetValue(c.Id, out var alacak) ? alacak : 0,
                Verecek = verecekMap.TryGetValue(c.Id, out var verecek) ? verecek : 0,
                Avans = avansMap.TryGetValue(c.Id, out var avans) ? avans : 0,
                RolEtiketi = BuildRolEtiketi(c.Rol)
            })
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.RolEtiketi)
            .ToList();
        return satirlar;
    }

    private async Task<CariDogrulamaVM> BuildDogrulamaVmAsync(IReadOnlyCollection<CariSatirVM> tumSatirlar)
    {
        var acikVeresiyeler = await _db.Veresiyeler
            .AsNoTracking()
            .Include(v => v.Odemeler)
            .Where(v => v.OdenmeDurumu != OdenmeDurumu.Iptal)
            .ToListAsync();

        var acikAvanslar = acikVeresiyeler.Where(v => v.Tip == VeresiyeTipi.Avans).ToList();
        var acikBorclar = acikVeresiyeler.Where(v => v.Tip != VeresiyeTipi.Avans).ToList();

        var acikVadeliAlislar = await _db.Alislar
            .AsNoTracking()
            .Where(a => a.OdemeTipi == AlisOdemeTipi.Vadeli)
            .ToListAsync();

        return new CariDogrulamaVM
        {
            CariToplamAlacak = tumSatirlar.Sum(x => x.Alacak),
            CariToplamVerecek = tumSatirlar.Sum(x => x.Verecek),
            CariToplamAvans = tumSatirlar.Sum(x => x.Avans),
            AcikVeresiyeToplam = acikBorclar.Sum(CalculateVeresiyeKalan),
            AcikAvansToplam = acikAvanslar.Sum(CalculateVeresiyeKalan),
            AcikVadeliAlisToplam = acikVadeliAlislar.Sum(CalculateAlisKalan),
            KasaBakiye = await _db.KasaHareketler.AsNoTracking()
                .SumAsync(x => x.HareketTipi == KasaHareketTipi.Gelir ? x.Tutar : -x.Tutar)
        };
    }

    private async Task<ServiceResult> SyncToLegacyAsync(Cari cari)
    {
        if (cari.Rol.HasFlag(CariRol.Musteri))
        {
            await UpsertMusteriAsync(cari);
        }
        else
        {
            var musteri = await _db.Musteriler.FirstOrDefaultAsync(x => x.CariId == cari.Id);
            if (musteri != null)
            {
                if (await HasMusteriHareketAsync(musteri.Id))
                    return ServiceResult.Failure("Cari üzerinde müşteri hareketi olduğu için müşteri rolü kaldırılamaz.");
                _db.Musteriler.Remove(musteri);
            }
        }

        if (cari.Rol.HasFlag(CariRol.Tedarikci))
        {
            await UpsertTedarikciAsync(cari);
        }
        else
        {
            var tedarikci = await _db.Tedarikciler.FirstOrDefaultAsync(x => x.CariId == cari.Id);
            if (tedarikci != null)
            {
                if (await HasTedarikciHareketAsync(tedarikci.Id))
                    return ServiceResult.Failure("Cari üzerinde tedarikçi hareketi olduğu için tedarikçi rolü kaldırılamaz.");
                if (await _db.Urunler.AnyAsync(x => x.TedarikciId == tedarikci.Id))
                    return ServiceResult.Failure("Cariye bağlı ürünler bulunduğu için tedarikçi rolü kaldırılamaz.");
                _db.Tedarikciler.Remove(tedarikci);
            }
        }

        return ServiceResult.Success();
    }

    private async Task UpsertMusteriAsync(Cari cari)
    {
        var musteri = await _db.Musteriler.FirstOrDefaultAsync(x => x.CariId == cari.Id);
        var (ad, soyad) = SplitCariAd(cari.Ad);

        if (musteri == null)
        {
            _db.Musteriler.Add(new Musteri
            {
                CariId = cari.Id,
                Ad = ad,
                Soyad = soyad,
                Telefon = cari.Telefon,
                Adres = cari.Adres,
                OlusturulmaTarihi = DateTime.UtcNow
            });
            return;
        }

        musteri.Ad = ad;
        musteri.Soyad = soyad;
        musteri.Telefon = cari.Telefon;
        musteri.Adres = cari.Adres;
    }

    private async Task UpsertTedarikciAsync(Cari cari)
    {
        var tedarikci = await _db.Tedarikciler.FirstOrDefaultAsync(x => x.CariId == cari.Id);
        if (tedarikci == null)
        {
            _db.Tedarikciler.Add(new Tedarikci
            {
                CariId = cari.Id,
                Ad = cari.Ad,
                YetkiliKisi = cari.YetkiliKisi,
                Telefon = cari.Telefon,
                Adres = cari.Adres,
                OlusturulmaTarihi = DateTime.UtcNow
            });
            return;
        }

        tedarikci.Ad = cari.Ad;
        tedarikci.YetkiliKisi = cari.YetkiliKisi;
        tedarikci.Telefon = cari.Telefon;
        tedarikci.Adres = cari.Adres;
    }

    private async Task<bool> HasMusteriHareketAsync(int musteriId)
    {
        var hasSatis = await _db.Satislar.AnyAsync(x => x.MusteriId == musteriId);
        var hasVeresiye = await _db.Veresiyeler.AnyAsync(x => x.MusteriId == musteriId);
        return hasSatis || hasVeresiye;
    }

    private async Task<bool> HasTedarikciHareketAsync(int tedarikciId)
    {
        return await _db.Alislar.AnyAsync(x => x.TedarikciId == tedarikciId);
    }

    private static CariRol BuildRol(CariFormVM vm)
    {
        var rol = CariRol.Yok;
        if (vm.MusteriRol) rol |= CariRol.Musteri;
        if (vm.TedarikciRol) rol |= CariRol.Tedarikci;
        return rol;
    }

    private static (string Ad, string Soyad) SplitCariAd(string cariAd)
    {
        var temiz = (cariAd ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(temiz))
            return (string.Empty, string.Empty);

        var parcalar = temiz.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parcalar.Length == 1)
            return (parcalar[0], string.Empty);

        return (parcalar[0], string.Join(" ", parcalar.Skip(1)));
    }

    private static string BuildRolEtiketi(CariRol rol)
    {
        var isMusteri = rol.HasFlag(CariRol.Musteri);
        var isTedarikci = rol.HasFlag(CariRol.Tedarikci);
        if (isMusteri && isTedarikci) return "Müşteri + Tedarikçi";
        if (isMusteri) return "Müşteri";
        if (isTedarikci) return "Tedarikçi";
        return "-";
    }

    private static decimal CalculateVeresiyeKalan(Veresiye veresiye)
    {
        if (veresiye.OdenmeDurumu == OdenmeDurumu.Iptal)
            return 0;

        return Math.Max(0, veresiye.Tutar - veresiye.Odemeler.Sum(o => o.OdemeTutari));
    }

    private static decimal CalculateAlisKalan(Alis alis)
    {
        return AlisBorcHesaplayici.CalculateKalanBorc(alis);
    }
}
