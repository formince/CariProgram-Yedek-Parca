using CariErinc.Data;
using CariErinc.Helpers;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class SatisService : ISatisService
{
    private const decimal MaxNumeric18_2 = 9999999999999999.99m;

    private readonly AppDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly IAyarService _ayarService;
    private readonly IKasaService _kasaService;
    private readonly IStokService _stokService;
    private readonly IVeresiyeService _veresiyeService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SatisService(
        AppDbContext db,
        IAuditLogService auditLog,
        IAyarService ayarService,
        IKasaService kasaService,
        IStokService stokService,
        IVeresiyeService veresiyeService,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _auditLog = auditLog;
        _ayarService = ayarService;
        _kasaService = kasaService;
        _stokService = stokService;
        _veresiyeService = veresiyeService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<Satis>> GetAllAsync(int? musteriId, OdemeTipi? tip, DateTime? baslangic, DateTime? bitis, bool dahilIptaller = false)
    {
        var query = _db.Satislar
            .Include(s => s.Musteri)
            .Include(s => s.Veresiye)
            .AsQueryable();

        query = ApplyFilters(query, musteriId, tip, baslangic, bitis, dahilIptaller);
        return await query.OrderByDescending(s => s.Tarih).ToListAsync();
    }

    public async Task<SatisIndexVM> GetPagedListAsync(int page, int? musteriId, OdemeTipi? tip, DateTime? baslangic,
        DateTime? bitis, bool dahilIptaller = false)
    {
        const int pageSize = 30;
        var query = _db.Satislar
            .Include(s => s.Musteri)
            .Include(s => s.Veresiye)
            .AsQueryable();

        query = ApplyFilters(query, musteriId, tip, baslangic, bitis, dahilIptaller);
        query = query.OrderByDescending(s => s.Tarih);
        var paged = await query.ToPagedListAsync(page, pageSize);
        return new SatisIndexVM
        {
            Satislar = paged.Items,
            MusteriId = musteriId,
            Tip = tip switch
            {
                OdemeTipi.Pesin => "Pesin",
                OdemeTipi.Veresiye => "Veresiye",
                _ => ""
            },
            Baslangic = baslangic?.ToString("yyyy-MM-dd"),
            Bitis = bitis?.ToString("yyyy-MM-dd"),
            DahilIptaller = dahilIptaller,
            CurrentPage = paged.CurrentPage,
            TotalPages = paged.TotalPages,
            TotalCount = paged.TotalCount,
            PageSize = paged.PageSize
        };
    }

    public async Task<Satis?> GetByIdAsync(int id)
    {
        return await _db.Satislar
            .Include(s => s.Musteri)
            .Include(s => s.Veresiye)
                .ThenInclude(v => v!.Odemeler)
            .Include(s => s.SatisDetaylari)
                .ThenInclude(d => d.Urun)
            .Include(s => s.SatisDetaylari)
                .ThenInclude(d => d.SatisIadeDetaylari)
            .Include(s => s.SatisIadeler)
                .ThenInclude(i => i.IadeDetaylari)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<ServiceResult> SaveAsync(SatisVM vm)
    {
        return vm.Id == 0
            ? await SatisYapAsync(vm)
            : await UpdateSatisAsync(vm);
    }

    public async Task<ServiceResult> SilAsync(int id, string? neden = null)
    {
        return await TamIptalAsync(id, neden ?? "Satış silme (iptal)");
    }

    public async Task<ServiceResult> SatisYapAsync(SatisVM vm)
    {
        // Adım 1: Validasyon
        var validation = await ValidateSatisVmAsync(vm);
        if (!validation.IsSuccess)
            return validation;

        var (satirlar, genelMod, varsayilanKdv) = validation.Value!;
        var satis = new Satis
        {
            MusteriId = vm.MusteriId,
            Tarih = DateTime.UtcNow,
            OdemeTipi = vm.OdemeTipi,
            Aciklama = vm.Aciklama?.Trim()
        };

        // Adım 2: Satış detaylarını hesapla ve ekle (tutar, KDV, indirim)
        var araToplam = await SatisDetaylariniHesaplaAsync(satis, satirlar, varsayilanKdv);
        
        // Adım 3: Genel indirim uygula
        ApplyGeneralDiscount(satis, araToplam, vm, genelMod);

        if (IsTooLarge(satis.ToplamTutar))
            return ServiceResult.Failure("Genel toplam veri tabanı sınırını aşıyor.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Adım 4: Satışı kaydet (ID almak için)
            _db.Satislar.Add(satis);
            await _db.SaveChangesAsync();

            // Adım 5: Her ürün için stok çıkışı yap
            await ApplySatisStokCikisiAsync(satis, $"Satış #{satis.Id}");

            // Adım 6: Peşin → kasaya gelir ekle / Veresiye → veresiye oluştur
            await ApplySatisOdemeEtkisiAsync(satis, vm.MusteriId, vm.Aciklama);

            // Adım 7: Bekleyen sepet taslağını temizle
            RemoveDraft(vm.TaslakId);

            // Adım 8: Audit log hazırla
            _auditLog.LogHazirla("Satis", satis.Id, "Eklendi", yeniDeger: satis,
                aciklama: $"{(vm.OdemeTipi == OdemeTipi.Pesin ? "Peşin" : "Veresiye")} Satış Yapıldı");

            // Adım 9: SaveChanges + Commit
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success("Satış başarıyla kaydedildi.");
    }

    public async Task<ServiceResult> UpdateSatisAsync(SatisVM vm)
    {
        // Adım 1: Validasyon
        if (vm.Id <= 0)
            return ServiceResult.Failure("Geçersiz satış ID.");

        var eskiSatis = await GetByIdAsync(vm.Id);
        if (eskiSatis == null)
            return ServiceResult.Failure("Satış bulunamadı.");
        if (eskiSatis.IptalEdildi)
            return ServiceResult.Failure("İptal edilmiş satış düzenlenemez.");

        var validation = await ValidateSatisVmAsync(vm);
        if (!validation.IsSuccess)
            return validation;

        var (satirlar, genelMod, varsayilanKdv) = validation.Value!;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Adım 2: Eski satışın etkilerini geri al (stok iade, kasa geri, veresiye sil)
            await RevertSatisYanEtkileriAsync(eskiSatis);

            await UpdateSatisCariAsync(eskiSatis, vm.MusteriId);
            eskiSatis.OdemeTipi = vm.OdemeTipi;
            eskiSatis.Aciklama = vm.Aciklama?.Trim();

            _db.SatisDetaylari.RemoveRange(eskiSatis.SatisDetaylari);
            eskiSatis.SatisDetaylari.Clear();

            // Adım 3: Yeni detayları hesapla ve ekle
            var araToplam = await SatisDetaylariniHesaplaAsync(eskiSatis, satirlar, varsayilanKdv);
            ApplyGeneralDiscount(eskiSatis, araToplam, vm, genelMod);

            if (IsTooLarge(eskiSatis.ToplamTutar))
                return ServiceResult.Failure("Genel toplam veri tabanı sınırını aşıyor.");

            // Adım 4: Yeni side effects uygula (stok, kasa/veresiye)
            await ApplySatisStokCikisiAsync(eskiSatis, $"Satış #{eskiSatis.Id}");
            await ApplySatisOdemeEtkisiAsync(eskiSatis, vm.MusteriId, vm.Aciklama);

            // Adım 5: Audit log hazırla
            _auditLog.LogHazirla("Satis", eskiSatis.Id, "Guncellendi", yeniDeger: eskiSatis, aciklama: "Satış düzenlendi.");

            // Adım 6: SaveChanges + Commit
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success("Satış başarıyla güncellendi.");
    }

    public async Task<ServiceResult> KismiIadeAsync(SatisIadeVM vm)
    {
        // Adım 1: İade satırlarını doğrula
        var satis = await GetByIdAsync(vm.SatisId);
        if (satis == null)
            return ServiceResult.Failure("Satış bulunamadı.");
        if (satis.IptalEdildi)
            return ServiceResult.Failure("İptal edilmiş satıştan iade alınamaz.");

        var iadeSatirlari = vm.Satirlar.Where(x => x.IadeMiktar > 0).ToList();
        if (!iadeSatirlari.Any())
            return ServiceResult.Failure("İade edilecek ürün seçilmedi.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Adım 2: İade kaydı oluştur, miktarları hesapla
            var iade = new SatisIade
            {
                SatisId = vm.SatisId,
                IadeTarihi = DateTime.UtcNow,
                Neden = vm.Neden?.Trim()
            };

            decimal toplamIadeTutari = 0;
            foreach (var satir in iadeSatirlari)
            {
                var detay = satis.SatisDetaylari.FirstOrDefault(d => d.Id == satir.SatisDetayId);
                if (detay == null)
                    continue;

                var iadeMiktarKontrolu = ValidateIadeMiktari(detay, satir.IadeMiktar);
                if (iadeMiktarKontrolu != null)
                    return iadeMiktarKontrolu;

                var satirBirimNet = detay.NetTutar / detay.Miktar;
                var iadeTutari = Math.Round(satir.IadeMiktar * satirBirimNet * (1 - satis.GenelIndirimOrani / 100m), 2);
                toplamIadeTutari += iadeTutari;

                iade.IadeDetaylari.Add(new SatisIadeDetay
                {
                    SatisDetayId = detay.Id,
                    IadeMiktar = satir.IadeMiktar,
                    IadeTutari = iadeTutari
                });

                // Adım 3: Her ürün için stok girişi yap (iade = stoğa geri döner)
                var urun = await _db.Urunler.FindAsync(detay.UrunId);
                if (urun != null)
                    _stokService.StokGirisYap(urun, satir.IadeMiktar, $"Kısmi İade - Satış #{satis.Id}");
            }

            _db.SatisIadeler.Add(iade);
            satis.KismiIade = true;
            _db.Satislar.Update(satis);

            // Adım 4: Peşin → kasaya gider (iade) / Veresiye → veresiye tutarını düşür + borç azalt
            if (satis.OdemeTipi == OdemeTipi.Pesin)
            {
                var iadeKategori = await _db.GiderKategoriler.FirstOrDefaultAsync(k => k.Ad == "Satış İadesi");
                _kasaService.KasaGiderCik(toplamIadeTutari, "Satış İadesi", $"Kısmi İade - Satış #{satis.Id}", iadeKategori?.Id);
            }
            else if (satis.OdemeTipi == OdemeTipi.Veresiye && satis.Veresiye != null)
            {
                var veresiye = satis.Veresiye;
                veresiye.Tutar = Math.Max(0, veresiye.Tutar - toplamIadeTutari);
                var odenenTutar = veresiye.Odemeler?.Sum(o => o.OdemeTutari) ?? 0;
                if (odenenTutar >= veresiye.Tutar)
                    veresiye.OdenmeDurumu = OdenmeDurumu.Odendi;
                veresiye.Aciklama += $" (İADE: {toplamIadeTutari:N2} ₺)";
                _db.Veresiyeler.Update(veresiye);
            }

            // Adım 5: Audit log hazırla
            _auditLog.LogHazirla("Satis", satis.Id, "Guncellendi", yeniDeger: iade, aciklama: $"Kısmi iade yapıldı. Toplam: {toplamIadeTutari:N2} ₺");

            // Adım 6: SaveChanges + Commit
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return ServiceResult.Success("İade başarıyla kaydedildi.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return ServiceResult.Failure($"Hata: {ex.Message}");
        }
    }

    public async Task<ServiceResult<int>> TaslakKaydetAsync(SatisVM vm)
    {
        var validation = await ValidateSatisVmAsync(vm);
        if (!validation.IsSuccess)
            return ServiceResult<int>.Failure(validation.Message);

        var (satirlar, genelMod, varsayilanKdv) = validation.Value!;
        var satis = new Satis
        {
            MusteriId = vm.MusteriId,
            CariId = vm.MusteriId.HasValue
                ? await _db.Musteriler.Where(m => m.Id == vm.MusteriId.Value).Select(m => m.CariId).FirstOrDefaultAsync()
                : null,
            Tarih = DateTime.UtcNow,
            Durum = SatisDurum.Taslak,
            Aciklama = vm.Aciklama?.Trim() ?? "Bekleyen Sepet",
            OdemeTipi = vm.OdemeTipi
        };

        var araToplam = await AddDraftDetailsAsync(satis, satirlar, varsayilanKdv);
        ApplyGeneralDiscount(satis, araToplam, vm, genelMod);

        _db.Satislar.Add(satis);
        if (vm.TaslakId.HasValue)
            RemoveDraft(vm.TaslakId);

        await _db.SaveChangesAsync();
        return ServiceResult<int>.Success(satis.Id, "Sepet beklemeye alındı.");
    }

    public async Task<List<Satis>> GetTaslaklarAsync()
    {
        return await _db.Satislar
            .Include(s => s.Musteri)
            .Include(s => s.SatisDetaylari)
                .ThenInclude(sd => sd.Urun)
            .Where(s => s.Durum == SatisDurum.Taslak)
            .OrderByDescending(s => s.Tarih)
            .ToListAsync();
    }

    public async Task<SatisVM?> TaslagiYukleAsync(int taslakId)
    {
        var taslak = await _db.Satislar
            .Include(s => s.SatisDetaylari)
                .ThenInclude(d => d.Urun)
            .FirstOrDefaultAsync(s => s.Id == taslakId && s.Durum == SatisDurum.Taslak);

        if (taslak == null)
            return null;

        return new SatisVM
        {
            TaslakId = taslak.Id,
            MusteriId = taslak.MusteriId,
            OdemeTipi = taslak.OdemeTipi,
            Aciklama = taslak.Aciklama,
            GenelIndirimOrani = taslak.GenelIndirimOrani,
            GenelIndirimTutari = taslak.GenelIndirimTutari,
            GenelIndirimModu = taslak.GenelIndirimHesapModu,
            HedefToplam = taslak.GenelIndirimHedefToplam,
            Satirlar = taslak.SatisDetaylari.Select(d => new SatisDetaySatirVM
            {
                UrunId = d.UrunId,
                Miktar = d.Miktar,
                BirimFiyat = d.BirimFiyat,
                IndirimOrani = d.IndirimOrani,
                KdvOrani = d.KdvOrani,
                SatirNetTutarHedef = d.NetTutar
            }).ToList()
        };
    }

    public async Task<ServiceResult> TaslakSilAsync(int taslakId)
    {
        var taslak = await _db.Satislar
            .Include(s => s.SatisDetaylari)
            .FirstOrDefaultAsync(s => s.Id == taslakId);

        if (taslak == null)
            return ServiceResult.Success("Taslak sepet silindi.");

        _db.SatisDetaylari.RemoveRange(taslak.SatisDetaylari);
        _db.Satislar.Remove(taslak);
        await _db.SaveChangesAsync();
        return ServiceResult.Success("Taslak sepet silindi.");
    }

    public async Task<Satis?> GetForEditAsync(int id)
    {
        return await _db.Satislar
            .Include(s => s.SatisDetaylari)
                .ThenInclude(d => d.Urun)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    private IQueryable<Satis> ApplyFilters(IQueryable<Satis> query, int? musteriId, OdemeTipi? tip, DateTime? baslangic, DateTime? bitis, bool dahilIptaller)
    {
        query = query.Where(s => s.Durum == SatisDurum.Tamamlandi);

        if (!dahilIptaller)
            query = query.Where(s => !s.IptalEdildi);

        if (musteriId.HasValue && musteriId.Value > 0)
            query = query.Where(s => s.MusteriId == musteriId.Value);

        if (tip.HasValue)
            query = query.Where(s => s.OdemeTipi == tip.Value);

        if (baslangic.HasValue)
            query = query.Where(s => s.Tarih >= baslangic.Value.Date);

        if (bitis.HasValue)
            query = query.Where(s => s.Tarih < bitis.Value.Date.AddDays(1));

        return query;
    }

    private async Task<ServiceResult<(List<SatisDetaySatirVM> Satirlar, GenelIndirimModu GenelMod, int VarsayilanKdv)>> ValidateSatisVmAsync(SatisVM vm)
    {
        var satirlar = vm.Satirlar?
            .Where(s => s.UrunId > 0 && s.Miktar > 0 && s.BirimFiyat > 0)
            .ToList() ?? new List<SatisDetaySatirVM>();

        if (!satirlar.Any())
            return ServiceResult<(List<SatisDetaySatirVM>, GenelIndirimModu, int)>.Failure("En az bir geçerli ürün satırı ekleyin.");

        if (vm.OdemeTipi == OdemeTipi.Veresiye && (!vm.MusteriId.HasValue || vm.MusteriId.Value <= 0))
            return ServiceResult<(List<SatisDetaySatirVM>, GenelIndirimModu, int)>.Failure("Veresiye satış için müşteri seçiniz.");

        foreach (var satir in satirlar)
        {
            if (satir.BirimFiyat < 0 || satir.BirimFiyat > MaxNumeric18_2)
                return ServiceResult<(List<SatisDetaySatirVM>, GenelIndirimModu, int)>.Failure("Birim fiyat veri tabanı sınırını aşıyor.");

            if (satir.IndirimOrani < 0 || satir.IndirimOrani > 100)
                return ServiceResult<(List<SatisDetaySatirVM>, GenelIndirimModu, int)>.Failure("Satır indirim oranı 0-100 arasında olmalıdır.");

            if (await _db.Urunler.FindAsync(satir.UrunId) == null)
                return ServiceResult<(List<SatisDetaySatirVM>, GenelIndirimModu, int)>.Failure("Ürün bulunamadı.");
        }

        var genelMod = SatisTutarHesaplayici.CozumleGenelIndirimModu(vm.GenelIndirimModu, vm.GenelIndirimTutari);
        if (genelMod == GenelIndirimModu.Yuzde && (vm.GenelIndirimOrani < 0 || vm.GenelIndirimOrani > 100))
            return ServiceResult<(List<SatisDetaySatirVM>, GenelIndirimModu, int)>.Failure("Genel indirim oranı 0-100 arasında olmalıdır.");

        var varsayilanKdv = await _ayarService.GetVarsayilanKdvOraniAsync();
        return ServiceResult<(List<SatisDetaySatirVM>, GenelIndirimModu, int)>.Success((satirlar, genelMod, varsayilanKdv));
    }

    private static Satis HazirlaSatis(SatisVM vm) => new()
    {
        MusteriId = vm.MusteriId,
        Tarih = DateTime.UtcNow,
        OdemeTipi = vm.OdemeTipi,
        Aciklama = vm.Aciklama?.Trim()
    };

    private async Task<decimal> SatisDetaylariniHesaplaAsync(Satis satis, IEnumerable<SatisDetaySatirVM> satirlar, int varsayilanKdv)
    {
        decimal araToplam = 0;
        foreach (var satir in satirlar)
        {
            var urun = await _db.Urunler.FindAsync(satir.UrunId);
            var kdvOrani = satir.KdvOrani > 0 ? satir.KdvOrani : (urun?.KdvOrani > 0 ? urun.KdvOrani : varsayilanKdv);

            var (brutTutar, indirimTutari, netTutar, kdvTutari, indirimOraniKayit) = SatisTutarHesaplayici.SatirHesapla(
                satir.Miktar, satir.BirimFiyat, satir.IndirimOrani, kdvOrani, satir.SatirNetTutarHedef);

            if (IsTooLarge(brutTutar) || IsTooLarge(indirimTutari) || IsTooLarge(netTutar))
                throw new InvalidOperationException("Tutar hesaplaması veri tabanı sınırını aşıyor.");

            araToplam += netTutar;
            satis.SatisDetaylari.Add(new SatisDetay
            {
                UrunId = satir.UrunId,
                Miktar = satir.Miktar,
                BirimFiyat = satir.BirimFiyat,
                KdvOrani = kdvOrani,
                KdvTutari = kdvTutari,
                IndirimOrani = indirimOraniKayit,
                IndirimTutari = indirimTutari,
                NetTutar = netTutar,
                AlisBirimFiyati = urun?.AlisFiyati ?? 0
            });
        }

        return araToplam;
    }

    private async Task<decimal> AddDraftDetailsAsync(Satis satis, IEnumerable<SatisDetaySatirVM> satirlar, int varsayilanKdv)
    {
        decimal araToplam = 0;
        foreach (var satir in satirlar)
        {
            var urun = await _db.Urunler.FindAsync(satir.UrunId);
            var kdvOrani = satir.KdvOrani > 0 ? satir.KdvOrani : 0;

            var (brutTutar, indirimTutari, netTutar, _, indirimOraniKayit) = SatisTutarHesaplayici.SatirHesapla(
                satir.Miktar, satir.BirimFiyat, satir.IndirimOrani, kdvOrani, satir.SatirNetTutarHedef);

            araToplam += netTutar;
            satis.SatisDetaylari.Add(new SatisDetay
            {
                UrunId = satir.UrunId,
                Miktar = satir.Miktar,
                BirimFiyat = satir.BirimFiyat,
                IndirimOrani = indirimOraniKayit,
                IndirimTutari = indirimTutari,
                NetTutar = netTutar,
                KdvOrani = kdvOrani,
                AlisBirimFiyati = urun?.AlisFiyati ?? 0
            });
        }

        return araToplam;
    }

    private static void ApplyGeneralDiscount(Satis satis, decimal araToplam, SatisVM vm, GenelIndirimModu genelMod)
    {
        var (genelTutar, genelOran) = SatisTutarHesaplayici.GenelIndirimHesapla(
            araToplam, vm.GenelIndirimModu, vm.GenelIndirimOrani, vm.HedefToplam, vm.GenelIndirimTutari);

        satis.GenelIndirimTutari = genelTutar;
        satis.GenelIndirimOrani = genelOran;
        satis.GenelIndirimHesapModu = genelMod;
        satis.GenelIndirimHedefToplam = genelMod == GenelIndirimModu.ManuelHedefToplam
            ? Math.Round(araToplam - genelTutar, 2)
            : 0m;
        satis.IndirimSonrasiToplam = Math.Round(araToplam - genelTutar, 2);
        satis.ToplamTutar = satis.IndirimSonrasiToplam;
    }

    private void RemoveDraft(int? taslakId)
    {
        if (!taslakId.HasValue)
            return;

        var taslak = _db.Satislar.Include(s => s.SatisDetaylari).FirstOrDefault(s => s.Id == taslakId.Value);
        if (taslak == null)
            return;

        _db.SatisDetaylari.RemoveRange(taslak.SatisDetaylari);
        _db.Satislar.Remove(taslak);
    }

    private async Task ApplySatisStokCikisiAsync(Satis satis, string aciklama)
    {
        foreach (var detail in satis.SatisDetaylari)
        {
            var urun = await _db.Urunler.FindAsync(detail.UrunId);
            if (urun != null)
                _stokService.StokCikisYap(urun, detail.Miktar, aciklama);
        }
    }

    private async Task ApplySatisStokGirisiAsync(Satis satis, string aciklama)
    {
        foreach (var detail in satis.SatisDetaylari)
        {
            var urun = await _db.Urunler.FindAsync(detail.UrunId);
            if (urun != null)
                _stokService.StokGirisYap(urun, detail.Miktar, aciklama);
        }
    }

    private async Task ApplySatisOdemeEtkisiAsync(Satis satis, int? musteriId, string? aciklama)
    {
        if (satis.OdemeTipi == OdemeTipi.Pesin)
        {
            if (musteriId.HasValue)
            {
                var pesinMusteri = await _db.Musteriler.FindAsync(musteriId.Value);
                satis.CariId = pesinMusteri?.CariId;
            }

            _kasaService.KasaGelirEkle(satis.ToplamTutar, "Satış", $"Peşin Satış #{satis.Id} (Net: {satis.ToplamTutar:N2} ₺)");
            return;
        }

        if (!musteriId.HasValue)
            return;

        var musteri = await _db.Musteriler.FindAsync(musteriId.Value);
        if (musteri == null)
            return;

        satis.CariId = musteri.CariId;

        // Önce avanstan düş (FIFO). Kalan tutar veresiye olarak yazılır.
        var avansKullanildi = await _veresiyeService.AvansDusCoreAsync(
            musteri.CariId ?? 0,
            satis.ToplamTutar,
            $"Satış #{satis.Id} avans kullanımı",
            _httpContextAccessor.HttpContext?.User?.Identity?.Name);

        if (avansKullanildi.KalanTutar > 0)
        {
            // Avans tamamen karşılamadı — kalan kısmı yeni veresiye olarak açılır.
            _db.Veresiyeler.Add(CreateSatisVeresiye(satis, musteri, aciklama, avansKullanildi.KalanTutar));
        }
        // else: avans tüm tutarı karşıladı, yeni veresiye açılmaz.
    }

    private async Task RevertSatisYanEtkileriAsync(Satis satis)
    {
        await ApplySatisStokGirisiAsync(satis, $"Satış Düzeltme Geri Alımı #{satis.Id}");

        if (satis.OdemeTipi == OdemeTipi.Pesin)
        {
            _kasaService.KasaGiderCik(satis.ToplamTutar, "Satış Düzeltme", $"Satış Düzeltme İadesi #{satis.Id}");
            return;
        }

        if (satis.OdemeTipi == OdemeTipi.Veresiye && satis.Veresiye != null)
            _db.Veresiyeler.Remove(satis.Veresiye);
    }

    private async Task UpdateSatisCariAsync(Satis satis, int? musteriId)
    {
        satis.MusteriId = musteriId;
        if (!musteriId.HasValue)
        {
            satis.CariId = null;
            return;
        }

        var seciliMusteri = await _db.Musteriler.FindAsync(musteriId.Value);
        satis.CariId = seciliMusteri?.CariId;
    }

    private static Veresiye CreateSatisVeresiye(Satis satis, Musteri musteri, string? aciklama, decimal? tutarOverride = null)
    {
        return new Veresiye
        {
            CariId = musteri.CariId,
            MusteriId = musteri.Id,
            SatisId = satis.Id,
            Tutar = tutarOverride ?? satis.ToplamTutar,
            Aciklama = aciklama?.Trim() ?? $"Satış #{satis.Id}",
            Tarih = DateTime.UtcNow,
            OdenmeDurumu = OdenmeDurumu.Bekliyor
        };
    }

    private static ServiceResult? ValidateIadeMiktari(SatisDetay detay, int iadeMiktari)
    {
        var oncedenIadeMiktari = detay.SatisIadeDetaylari?.Sum(x => x.IadeMiktar) ?? 0;
        if (iadeMiktari <= detay.Miktar - oncedenIadeMiktari)
            return null;

        return ServiceResult.Failure($"'{detay.Urun.Ad}' için iade miktarı satılan miktardan fazla olamaz.");
    }

    private bool IsTooLarge(decimal value) => value > MaxNumeric18_2 || value < -MaxNumeric18_2;

    private async Task<ServiceResult> TamIptalAsync(int satisId, string? neden)
    {
        var satis = await GetByIdAsync(satisId);
        if (satis == null)
            return ServiceResult.Failure("Satış bulunamadı.");
        if (satis.IptalEdildi)
            return ServiceResult.Failure("Bu satış zaten iptal edilmiş.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Adım 1: Satışı iptal işaretle
            satis.IptalEdildi = true;
            satis.IptalTarihi = DateTime.UtcNow;
            satis.IptalNedeni = neden?.Trim();

            // Adım 2: Her ürün için stok geri al
            await ApplySatisStokGirisiAsync(satis, $"Satış İptali #{satis.Id}");

            // Adım 3: Peşin → kasaya gider (iade) / Veresiye → veresiye sil + borç düşür
            if (satis.OdemeTipi == OdemeTipi.Pesin)
            {
                _kasaService.KasaGiderCik(satis.ToplamTutar, "Satış İptali", $"Satış İptali #{satis.Id}");
            }
            else if (satis.OdemeTipi == OdemeTipi.Veresiye && satis.Veresiye != null)
            {
                _db.Veresiyeler.Remove(satis.Veresiye);
            }

            // Adım 4: Audit log hazırla
            _auditLog.LogHazirla("Satis", satis.Id, "Guncellendi", yeniDeger: satis, aciklama: "Satış tamamen iptal edildi.");

            // Adım 5: SaveChanges + Commit
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success("Satış başarıyla iptal edildi.");
    }
}
