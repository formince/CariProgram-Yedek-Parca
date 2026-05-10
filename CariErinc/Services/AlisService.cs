using CariErinc.Data;
using CariErinc.Helpers;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class AlisService : IAlisService
{
    private sealed record AlisSatirIslem(Urun Urun, int Miktar, decimal Maliyet);

    private readonly AppDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly IAyarService _ayarService;
    private readonly IKasaService _kasaService;
    private readonly IStokService _stokService;
    private readonly IUrunFiyatService _fiyatService;

    public AlisService(AppDbContext db, IAuditLogService auditLog, IAyarService ayarService,
        IKasaService kasaService, IStokService stokService, IUrunFiyatService fiyatService)
    {
        _db = db;
        _auditLog = auditLog;
        _ayarService = ayarService;
        _kasaService = kasaService;
        _stokService = stokService;
        _fiyatService = fiyatService;
    }

    // ========================================================================
    // SORGULAMA METOTLARI
    // ========================================================================

    public async Task<AlisIndexVM> GetPagedListAsync(int page, int? tedarikciId, DateTime? baslangic, DateTime? bitis)
    {
        int pageSize = 30;
        var query = ApplyFilters(BuildBaseQuery(), tedarikciId, baslangic, bitis);
        var pagedResult = await query.ToPagedListAsync(page, pageSize);

        return new AlisIndexVM
        {
            Alislar = pagedResult.Items,
            TedarikciId = tedarikciId,
            Baslangic = baslangic?.ToString("yyyy-MM-dd"),
            Bitis = bitis?.ToString("yyyy-MM-dd"),
            CurrentPage = pagedResult.CurrentPage,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount,
            TotalPages = pagedResult.TotalPages
        };
    }

    public async Task<Alis?> GetByIdAsync(int id)
    {
        return await _db.Alislar
            .Include(a => a.Tedarikci)
            .Include(a => a.AlisOdemeleri)
            .Include(a => a.AlisDetaylari).ThenInclude(ad => ad.Urun)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<Alis>> GetVadeliAcikAlislarAsync(int? tedarikciId = null)
    {
        var query = _db.Alislar.Include(a => a.Tedarikci)
            .Where(a => a.OdemeTipi == AlisOdemeTipi.Vadeli && a.ToplamTutar > a.OdenenTutar);

        if (tedarikciId.HasValue && tedarikciId.Value > 0)
            query = query.Where(a => a.TedarikciId == tedarikciId.Value);

        return await query.OrderByDescending(a => a.Tarih).ToListAsync();
    }

    // ========================================================================
    // ANA İŞLEM METOTLARI
    // ========================================================================

    public async Task<ServiceResult> SaveAsync(AlisVM vm)
    {
        return !vm.AlisId.HasValue || vm.AlisId.Value == 0
            ? await AlisYapAsync(vm)
            : await AlisGuncelleAsync(vm);
    }

    public async Task<ServiceResult> SilAsync(int id) => await AlisSilAsync(id);

    /// <summary>Yeni alış kaydı oluşturur; stok girişi, kasa/borç ve fiyat geçmişi işlerini yapar.</summary>
    public async Task<ServiceResult> AlisYapAsync(AlisVM vm)
    {
        var validasyon = await ValidateAlisAsync(vm);
        if (!validasyon.IsSuccess) return validasyon;

        var (tedarikci, satirlar) = validasyon.Value!;
        var alis = PrepareAlis(vm);
        alis.CariId = tedarikci.CariId;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Alislar.Add(alis);
            await _db.SaveChangesAsync(); // ID almak için

            alis.ToplamTutar = await AlisSatirlariniIsleAsync(alis, satirlar, tedarikci.Ad, "OtomatikFatura");
            AlisOdemeDurumuGuncelle(alis);
            ApplyOdemeVeBorc(alis, tedarikci, vm.OdemeTipi, vm.Aciklama);

            _auditLog.LogHazirla("Alis", alis.Id, "Eklendi", yeniDeger: alis,
                aciklama: $"Tedarikçiden {(vm.OdemeTipi == AlisOdemeTipi.Nakit ? "Nakit" : "Vadeli")} Alış: {tedarikci.Ad}");

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success("Alış başarıyla kaydedildi.");
    }

    /// <summary>Mevcut alışı günceller; eski stok/kasa etkilerini geri alır, yenilerini uygular.</summary>
    public async Task<ServiceResult> AlisGuncelleAsync(AlisVM vm)
    {
        if (!vm.AlisId.HasValue || vm.AlisId.Value <= 0)
            return ServiceResult.Failure("Geçersiz alış kaydı.");

        var satirlar = FiltreleGecerliSatirlar(vm);
        if (!satirlar.Any()) return ServiceResult.Failure("En az bir geçerli ürün satırı ekleyin.");

        var alis = await LoadAlisForEditAsync(vm.AlisId.Value);
        if (alis == null) return ServiceResult.Failure("Alış kaydı bulunamadı.");
        if (alis.AlisOdemeleri.Count > 0) return ServiceResult.Failure("Ödeme yapılmış alışlar düzenlenemez.");
        if (vm.OdemeTipi != alis.OdemeTipi) return ServiceResult.Failure("Ödeme tipi değiştirilemez.");

        var yeniTedarikci = await _db.Tedarikciler.FindAsync(vm.TedarikciId);
        if (yeniTedarikci == null) return ServiceResult.Failure("Tedarikçi bulunamadı.");

        var eskiSatirMiktarlari = BuildSatirMiktarMap(alis.AlisDetaylari);
        var yeniSatirMiktarlari = BuildSatirMiktarMap(satirlar);

        var stokHatasi = await ValidateStokAzalislariAsync(eskiSatirMiktarlari, yeniSatirMiktarlari);
        if (stokHatasi != null) return ServiceResult.Failure(stokHatasi);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            UndoAlisEtki(alis);
            _db.AlisDetaylari.RemoveRange(alis.AlisDetaylari.ToList());

            alis.TedarikciId = vm.TedarikciId;
            alis.CariId = yeniTedarikci.CariId;
            alis.Aciklama = vm.Aciklama?.Trim();
            alis.VadeTarihi = vm.VadeTarihi.HasValue ? DateTime.SpecifyKind(vm.VadeTarihi.Value.Date, DateTimeKind.Utc) : null;

            alis.ToplamTutar = await AlisSatirlariniGuncelleAsync(alis, satirlar, "OtomatikFaturaDuzeltme");
            await StokFarklariniUygulaAsync(eskiSatirMiktarlari, yeniSatirMiktarlari, alis, yeniTedarikci.Ad);
            ApplyOdemeVeBorc(alis, yeniTedarikci, vm.OdemeTipi, vm.Aciklama);
            AlisOdemeDurumuGuncelle(alis);

            _auditLog.LogHazirla("Alis", alis.Id, "Guncellendi", yeniDeger: alis, aciklama: "Alış fişi düzeltildi.");

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success("Alış güncellendi; stok, kasa ve borç yeniden hesaplandı.");
    }

    /// <summary>Alış kaydını siler; stokları geri alır, kasa/borç iadesi yapar.</summary>
    public async Task<ServiceResult> AlisSilAsync(int id)
    {
        var alis = await LoadAlisForEditAsync(id);
        if (alis == null) return ServiceResult.Failure("Alış kaydı bulunamadı.");
        if (alis.AlisOdemeleri.Count > 0)
            return ServiceResult.Failure("Bu alışa ödeme yapılmış; önce ödemeleri geri alın veya muhasebe onayı alın.");

        var stokHatasi = await StokYeterlilikKontroluAsync(alis);
        if (stokHatasi != null) return ServiceResult.Failure(stokHatasi);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            await StoklariGeriAlAsync(alis);
            UndoAlisEtki(alis);
            _db.Alislar.Remove(alis);

            _auditLog.LogHazirla("Alis", id, "Silindi", aciklama: "Alış fişi iptal edildi.");

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success("Alış iptal edildi; stok, kasa ve borç kayıtları güncellendi.");
    }

    /// <summary>Vadeli alışa ödeme yapar; tedarikçi borcunu ve kasa dengesini günceller.</summary>
    public async Task<ServiceResult> OdemeYapAsync(int alisId, decimal tutar, string? aciklama)
    {
        var alis = await GetByIdAsync(alisId);
        if (alis == null) return ServiceResult.Failure("Alış kaydı bulunamadı.");
        var kalanBorc = AlisBorcHesaplayici.CalculateKalanBorc(alis);
        var odemeValidasyonu = ValidateOdemeTutari(tutar, kalanBorc);
        if (odemeValidasyonu != null) return odemeValidasyonu;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            alis.OdenenTutar += tutar;
            AlisBorcHesaplayici.RecalculateDurum(alis);

            _db.AlisOdemeleri.Add(CreateAlisOdeme(alis.Id, tutar, aciklama));

            _kasaService.KasaGiderCik(tutar, "Alış Ödemesi", $"Tedarikçi: {alis.Tedarikci.Ad} - Alış #{alis.Id}");

            _auditLog.LogHazirla("Alis", alisId, "Guncellendi", yeniDeger: alis,
                aciklama: $"Toptancı Ödemesi: {tutar:N2} ₺");

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success("Ödeme başarıyla kaydedildi.");
    }

    // ========================================================================
    // ORTAK YARDIMCI METOTLAR (DRY)
    // ========================================================================

    /// <summary>Yeni alış satırlarını işler; stok girişi, fiyat geçmişi ve detay kaydı yapar. Toplam tutarı döner.</summary>
    private async Task<decimal> AlisSatirlariniIsleAsync(Alis alis, List<AlisDetaySatirVM> satirlar,
        string tedarikciAd, string fiyatKayitTipi)
    {
        var (toplamTutar, satirIslemleri) = await AlisDetaylariniHazirlaAsync(alis, satirlar);

        foreach (var satirIslem in satirIslemleri)
            _stokService.StokGirisYap(satirIslem.Urun, satirIslem.Miktar, $"Alış - {tedarikciAd}");

        await UpdateAlisFiyatlariAsync(satirIslemleri, fiyatKayitTipi, alis.Id);
        return toplamTutar;
    }

    /// <summary>Alış güncellemede satırları yeniden oluşturur; stok farkları ayrı adımda uygulanır.</summary>
    private async Task<decimal> AlisSatirlariniGuncelleAsync(Alis alis, List<AlisDetaySatirVM> satirlar, string fiyatKayitTipi)
    {
        var (toplamTutar, satirIslemleri) = await AlisDetaylariniHazirlaAsync(alis, satirlar);
        await UpdateAlisFiyatlariAsync(satirIslemleri, fiyatKayitTipi, alis.Id);
        return toplamTutar;
    }

    /// <summary>Satır detaylarını ve toplamı hesaplar; stok etkisi uygulamaz.</summary>
    private async Task<(decimal ToplamTutar, List<AlisSatirIslem> SatirIslemleri)> AlisDetaylariniHazirlaAsync(
        Alis alis, List<AlisDetaySatirVM> satirlar)
    {
        var varsayilanKdv = await _ayarService.GetVarsayilanKdvOraniAsync();
        decimal toplamTutar = 0m;
        var satirIslemleri = new List<AlisSatirIslem>();

        foreach (var satir in satirlar)
        {
            var urun = await _db.Urunler.FindAsync(satir.UrunId)
                ?? throw new InvalidOperationException($"Ürün bulunamadı: {satir.UrunId}");

            UrunBilgileriniGuncelle(urun, satir);

            var (alisDetay, satirNet) = AlisDetayHesapla(satir, urun, varsayilanKdv);
            alis.AlisDetaylari.Add(alisDetay);
            toplamTutar += satirNet;

            var maliyet = NetAlisBirimMaliyetKdvsiz(satir.BirimFiyat, satir.Iskonto1, satir.Iskonto2);
            satirIslemleri.Add(new AlisSatirIslem(urun, satir.Miktar, maliyet));
        }

        return (toplamTutar, satirIslemleri);
    }

    private async Task UpdateAlisFiyatlariAsync(List<AlisSatirIslem> satirIslemleri, string fiyatKayitTipi, int? alisId)
    {
        foreach (var satirIslem in satirIslemleri.GroupBy(x => x.Urun.Id).Select(g => g.Last()))
            await _fiyatService.UpdateAlisFiyatiAsync(satirIslem.Urun.Id, satirIslem.Maliyet, fiyatKayitTipi, "Sistem", alisId);
    }

    private static Dictionary<int, int> BuildSatirMiktarMap(IEnumerable<AlisDetay> satirlar)
    {
        return satirlar
            .GroupBy(s => s.UrunId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Miktar));
    }

    private static Dictionary<int, int> BuildSatirMiktarMap(IEnumerable<AlisDetaySatirVM> satirlar)
    {
        return satirlar
            .GroupBy(s => s.UrunId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Miktar));
    }

    private async Task<string?> ValidateStokAzalislariAsync(
        Dictionary<int, int> eskiSatirMiktarlari,
        Dictionary<int, int> yeniSatirMiktarlari)
    {
        var urunIds = eskiSatirMiktarlari.Keys.Union(yeniSatirMiktarlari.Keys).ToList();
        var urunler = await _db.Urunler
            .Where(u => urunIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        foreach (var urunId in urunIds)
        {
            var eskiMiktar = eskiSatirMiktarlari.GetValueOrDefault(urunId);
            var yeniMiktar = yeniSatirMiktarlari.GetValueOrDefault(urunId);
            var azalis = eskiMiktar - yeniMiktar;
            if (azalis <= 0) continue;

            if (!urunler.TryGetValue(urunId, out var urun))
                continue;

            if (urun.StokAdedi < azalis)
                return $"\"{urun.Ad}\" için yeterli stok yok (azalış {azalis} adet, mevcut {urun.StokAdedi}). Önce satışları kontrol edin.";
        }

        return null;
    }

    private async Task StokFarklariniUygulaAsync(
        Dictionary<int, int> eskiSatirMiktarlari,
        Dictionary<int, int> yeniSatirMiktarlari,
        Alis alis,
        string tedarikciAd)
    {
        var urunIds = eskiSatirMiktarlari.Keys.Union(yeniSatirMiktarlari.Keys).ToList();
        var urunler = await _db.Urunler
            .Where(u => urunIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        foreach (var urunId in urunIds)
        {
            if (!urunler.TryGetValue(urunId, out var urun))
                continue;

            var eskiMiktar = eskiSatirMiktarlari.GetValueOrDefault(urunId);
            var yeniMiktar = yeniSatirMiktarlari.GetValueOrDefault(urunId);
            var fark = yeniMiktar - eskiMiktar;
            if (fark == 0) continue;

            if (fark > 0)
            {
                _stokService.StokGirisYap(urun, fark, $"Alış düzeltme +{fark} adet #{alis.Id} - {tedarikciAd}");
                continue;
            }

            _stokService.StokCikisYap(urun, Math.Abs(fark), $"Alış düzeltme -{Math.Abs(fark)} adet #{alis.Id} - {tedarikciAd}");
        }
    }

    /// <summary>Alış satırlarındaki stokların geri alınabilir olup olmadığını kontrol eder. Hata mesajı veya null döner.</summary>
    private async Task<string?> StokYeterlilikKontroluAsync(Alis alis)
    {
        foreach (var d in alis.AlisDetaylari)
        {
            var urun = await _db.Urunler.FindAsync(d.UrunId);
            if (urun == null) continue;
            if (urun.StokAdedi < d.Miktar)
                return $"\"{urun.Ad}\" için rafta yeterli stok yok (iptal {d.Miktar} adet, mevcut {urun.StokAdedi}). Önce satışları kontrol edin.";
        }
        return null;
    }

    /// <summary>Alışa ait tüm stok girişlerini geri alır (stok çıkışı yapar).</summary>
    private async Task StoklariGeriAlAsync(Alis alis)
    {
        foreach (var d in alis.AlisDetaylari.ToList())
        {
            var urun = _db.Urunler.Local.FirstOrDefault(u => u.Id == d.UrunId)
                       ?? await _db.Urunler.FindAsync(d.UrunId);
            if (urun == null) continue;
            _stokService.StokCikisYap(urun, d.Miktar, $"Alış #{alis.Id} iptal — {alis.Tedarikci.Ad}");
        }
    }

    /// <summary>Alışın kasa veya borç etkisini geri alır.</summary>
    private void UndoAlisEtki(Alis alis)
    {
        if (alis.OdemeTipi == AlisOdemeTipi.Nakit)
            _kasaService.KasaGelirEkle(alis.ToplamTutar, "Alış İadesi",
                $"Alış #{alis.Id} iptal — {alis.Tedarikci.Ad}");
    }

    /// <summary>Ödeme tipine göre kasa çıkışı veya tedarikçi borcu uygular.</summary>
    private void ApplyOdemeVeBorc(Alis alis, Tedarikci tedarikci, AlisOdemeTipi odemeTipi, string? aciklama)
    {
        if (odemeTipi == AlisOdemeTipi.Nakit)
        {
            _kasaService.KasaGiderCik(alis.ToplamTutar, "Alış", aciklama ?? $"Alış - {tedarikci.Ad}");
        }
    }

    /// <summary>Ödeme durumunu ve tutarlarını ödeme tipine göre günceller.</summary>
    private static void AlisOdemeDurumuGuncelle(Alis alis)
    {
        AlisBorcHesaplayici.SetInitialDurum(alis);
    }

    private static ServiceResult? ValidateOdemeTutari(decimal tutar, decimal kalanBorc)
    {
        if (tutar <= 0)
            return ServiceResult.Failure("Ödeme tutarı 0'dan büyük olmalıdır.");

        if (tutar > kalanBorc)
            return ServiceResult.Failure($"Ödeme tutarı kalan borçtan ({kalanBorc:N2} ₺) fazla olamaz.");

        return null;
    }

    private static AlisOdeme CreateAlisOdeme(int alisId, decimal tutar, string? aciklama)
    {
        return new AlisOdeme
        {
            AlisId = alisId,
            OdemeTutari = tutar,
            OdemeTarihi = DateTime.UtcNow,
            Aciklama = aciklama?.Trim()
        };
    }

    /// <summary>Satırdaki barkod ve KDV bilgisini ürüne işler.</summary>
    private static void UrunBilgileriniGuncelle(Urun urun, AlisDetaySatirVM satir)
    {
        if (!string.IsNullOrWhiteSpace(satir.Barkod) && urun.Barkod != satir.Barkod.Trim())
            urun.Barkod = satir.Barkod.Trim();

        urun.KdvOrani = satir.KdvOrani;
    }

    /// <summary>Bir satırın KDV dahil net tutarını ve AlisDetay nesnesini hesaplar.</summary>
    private static (AlisDetay detay, decimal satirNet) AlisDetayHesapla(
        AlisDetaySatirVM satir, Urun urun, int varsayilanKdv)
    {
        // 0 KDV satır bazında bilinçli bir seçim olabilir; fallback'e düşürme.
        var kdvOrani = satir.KdvOrani >= 0 ? satir.KdvOrani
            : urun.KdvOrani > 0 ? urun.KdvOrani
            : varsayilanKdv;

        var satirBrut = satir.Miktar * satir.BirimFiyat;
        var indirimli = satirBrut * (1 - satir.Iskonto1 / 100m) * (1 - satir.Iskonto2 / 100m);
        var kdvTutari = Math.Round(indirimli * kdvOrani / 100m, 2);

        var detay = new AlisDetay
        {
            UrunId = satir.UrunId,
            Miktar = satir.Miktar,
            BirimFiyat = satir.BirimFiyat,
            Iskonto1 = satir.Iskonto1,
            Iskonto2 = satir.Iskonto2,
            KdvOrani = kdvOrani,
            KdvTutari = kdvTutari
        };

        return (detay, indirimli + kdvTutari);
    }

    /// <summary>İskontolar sonrası KDV hariç birim maliyeti hesaplar.</summary>
    private static decimal NetAlisBirimMaliyetKdvsiz(decimal listeBirimFiyat,
        decimal iskonto1Yuzde, decimal iskonto2Yuzde)
    {
        var net = listeBirimFiyat * (1 - iskonto1Yuzde / 100m) * (1 - iskonto2Yuzde / 100m);
        return Math.Round(net, 2, MidpointRounding.AwayFromZero);
    }

    // ========================================================================
    // VALIDASYON & HAZIRLIK METOTLARI
    // ========================================================================

    private async Task<ServiceResult<(Tedarikci tedarikci, List<AlisDetaySatirVM> satirlar)>> ValidateAlisAsync(AlisVM vm)
    {
        var satirlar = FiltreleGecerliSatirlar(vm);
        if (!satirlar.Any())
            return ServiceResult<(Tedarikci, List<AlisDetaySatirVM>)>.Failure("En az bir geçerli ürün satırı ekleyin.");

        var tedarikci = await _db.Tedarikciler.FindAsync(vm.TedarikciId);
        if (tedarikci == null)
            return ServiceResult<(Tedarikci, List<AlisDetaySatirVM>)>.Failure("Tedarikçi bulunamadı.");

        return ServiceResult<(Tedarikci, List<AlisDetaySatirVM>)>.Success((tedarikci, satirlar));
    }

    private static List<AlisDetaySatirVM> FiltreleGecerliSatirlar(AlisVM vm)
    {
        return vm.Satirlar?
            .Where(s => s.UrunId > 0 && s.Miktar > 0 && s.BirimFiyat > 0)
            .ToList() ?? new List<AlisDetaySatirVM>();
    }

    private static Alis PrepareAlis(AlisVM vm) => new()
    {
        TedarikciId = vm.TedarikciId,
        Tarih = DateTime.UtcNow,
        Aciklama = vm.Aciklama?.Trim(),
        OdemeTipi = vm.OdemeTipi,
        VadeTarihi = vm.VadeTarihi.HasValue
            ? DateTime.SpecifyKind(vm.VadeTarihi.Value.Date, DateTimeKind.Utc)
            : null,
        ToplamTutar = 0
    };

    private async Task<Alis?> LoadAlisForEditAsync(int id)
    {
        return await _db.Alislar
            .Include(a => a.Tedarikci)
            .Include(a => a.AlisDetaylari)
            .Include(a => a.AlisOdemeleri)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    // ========================================================================
    // QUERY BUILDER
    // ========================================================================

    private IQueryable<Alis> BuildBaseQuery()
    {
        return _db.Alislar
            .Include(a => a.Tedarikci)
            .Include(a => a.AlisOdemeleri)
            .OrderByDescending(a => a.Tarih)
            .AsQueryable();
    }

    private IQueryable<Alis> ApplyFilters(IQueryable<Alis> query, int? tedarikciId,
        DateTime? baslangic, DateTime? bitis)
    {
        if (tedarikciId.HasValue && tedarikciId.Value > 0)
            query = query.Where(a => a.TedarikciId == tedarikciId.Value);
        if (baslangic.HasValue)
            query = query.Where(a => a.Tarih >= baslangic.Value.Date);
        if (bitis.HasValue)
            query = query.Where(a => a.Tarih < bitis.Value.Date.AddDays(1));

        return query;
    }
}