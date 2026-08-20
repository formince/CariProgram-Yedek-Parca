using CariErinc.Data;
using CariErinc.Helpers;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class VeresiyeService : IVeresiyeService
{
    private readonly AppDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly IKasaService _kasaService;

    public VeresiyeService(AppDbContext db, IAuditLogService auditLog, IKasaService kasaService)
    {
        _db = db;
        _auditLog = auditLog;
        _kasaService = kasaService;
    }

    public async Task<VeresiyeIndexVM> GetPagedListAsync(int page = 1, int? musteriId = null, OdenmeDurumu? durum = null, DateTime? baslangic = null, DateTime? bitis = null)
    {
        var query = ApplyFilters(_db.Veresiyeler.Include(v => v.Musteri).Include(v => v.Odemeler).AsQueryable(), musteriId, durum, baslangic, bitis);
        var pagedResult = await query.OrderByDescending(v => v.Tarih).ToPagedListAsync(page, 30);

        return new VeresiyeIndexVM
        {
            Veresiyeler = pagedResult.Items,
            MusteriId = musteriId,
            Durum = durum,
            Baslangic = baslangic?.ToString("yyyy-MM-dd"),
            Bitis = bitis?.ToString("yyyy-MM-dd"),
            CurrentPage = pagedResult.CurrentPage,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount,
            TotalPages = pagedResult.TotalPages
        };
    }

    public async Task<List<Veresiye>> GetAllAsync(int? musteriId = null, OdenmeDurumu? durum = null, DateTime? baslangic = null, DateTime? bitis = null)
    {
        var query = ApplyFilters(_db.Veresiyeler.Include(v => v.Musteri).Include(v => v.Odemeler).AsQueryable(), musteriId, durum, baslangic, bitis);
        return await query.OrderByDescending(v => v.Tarih).ToListAsync();
    }

    public async Task<Veresiye?> GetByIdAsync(int id)
    {
        return await _db.Veresiyeler
            .Include(v => v.Musteri)
            .Include(v => v.Odemeler)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<ServiceResult> SaveAsync(VeresiyeVM vm)
    {
        return vm.Id == 0
            ? await CreateAsync(vm)
            : await UpdateAsync(vm);
    }

    public async Task<ServiceResult> SilAsync(int id)
    {
        var veresiye = await _db.Veresiyeler
            .Include(v => v.Musteri)
            .Include(v => v.Odemeler)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (veresiye == null)
            return ServiceResult.Failure("Veresiye kaydı bulunamadı.");

        if (veresiye.Odemeler.Any())
            return ServiceResult.Failure("Bu veresiye üzerinde ödeme kaydı bulunduğu için silinemez. Önce ödemeleri silmelisiniz.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Veresiyeler.Remove(veresiye);
            
            _auditLog.LogHazirla("Veresiye", id, "Silindi", aciklama: $"Veresiye Silindi: {veresiye.Musteri.Ad} {veresiye.Musteri.Soyad}");
            
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success("Veresiye kaydı başarıyla silindi.");
    }

    public async Task<ServiceResult> OdemeAlAsync(int veresiyeId, decimal tutar, string? aciklama, string? kullaniciId, VeresiyeOdemeTipi odemeTipi = VeresiyeOdemeTipi.Nakit)
    {
        // Adım 1: Validasyon
        if (tutar <= 0)
            return ServiceResult.Failure("Ödeme tutarı 0'dan büyük olmalıdır.");

        var veresiye = await _db.Veresiyeler.Include(v => v.Odemeler).Include(v => v.Musteri).FirstOrDefaultAsync(v => v.Id == veresiyeId);
        if (veresiye == null)
            return ServiceResult.Failure("Veresiye bulunamadı.");

        // Avans veresiyesine ödeme alınmaz (kavramsal olarak avans = müşterinin bakiyesi).
        if (veresiye.Tip == VeresiyeTipi.Avans)
            return ServiceResult.Failure("Avans kaydına ödeme alınamaz. Bunun yerine Cari Detay > Avans Kullan ile bakiyeden düşün.");

        var kalanBorc = GetKalanBorc(veresiye);
        var anaBorcaYazilacak = Math.Min(tutar, kalanBorc);
        var avansaYazilacak = tutar - anaBorcaYazilacak;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Adım 2: Ana borca yazılacak kısım
            if (anaBorcaYazilacak > 0)
            {
                var odeme = CreateOdemeKaydi(veresiye.Id, anaBorcaYazilacak, aciklama, kullaniciId, odemeTipi);
                veresiye.Odemeler.Add(odeme);
                _db.VeresiyeOdemeler.Add(odeme);

                veresiye.OdenmeDurumu = CalculatePaymentStatus(veresiye.Tutar, GetOdenenToplam(veresiye));
                _db.Veresiyeler.Update(veresiye);
            }

            // Adım 3: Fazla tutar avans olarak yeni veresiye oluşturur
            Veresiye? avansVeresiye = null;
            if (avansaYazilacak > 0)
            {
                avansVeresiye = new Veresiye
                {
                    CariId = veresiye.CariId,
                    MusteriId = veresiye.MusteriId,
                    Tutar = avansaYazilacak,
                    Tarih = DateTime.UtcNow,
                    OdenmeDurumu = OdenmeDurumu.Bekliyor,
                    Tip = VeresiyeTipi.Avans,
                    Aciklama = string.IsNullOrWhiteSpace(aciklama)
                        ? $"Veresiye #{veresiye.Id} ödeme fazlası"
                        : $"Veresiye #{veresiye.Id} ödeme fazlası — {aciklama.Trim()}"
                };
                _db.Veresiyeler.Add(avansVeresiye);
            }

            // Adım 4: Kasaya tek seferde toplam tutar yazılır
            _kasaService.KasaGelirEkle(
                tutar,
                "Veresiye Ödeme",
                $"Müşteri: {veresiye.Musteri.Ad} {veresiye.Musteri.Soyad} - Veresiye #{veresiye.Id}" +
                (avansaYazilacak > 0 ? $" (avans: {avansaYazilacak:N2} ₺)" : string.Empty));

            // Adım 5: Audit log
            _auditLog.LogHazirla(
                "Veresiye",
                veresiye.Id,
                "Guncellendi",
                yeniDeger: veresiye,
                aciklama: $"Veresiye Ödemesi Alındı: {tutar:N2} ₺" +
                          (avansaYazilacak > 0 ? $" (avansa yazılan: {avansaYazilacak:N2} ₺)" : string.Empty));

            // Adım 6: SaveChanges + Commit
            await _db.SaveChangesAsync();

            // Yeni oluşan avans veresiyenin audit log'u (kendi ID'si ile)
            if (avansVeresiye != null && avansVeresiye.Id > 0)
            {
                _auditLog.LogHazirla(
                    "Veresiye",
                    avansVeresiye.Id,
                    "Eklendi",
                    yeniDeger: avansVeresiye,
                    aciklama: $"Avans oluştu (Veresiye #{veresiye.Id} ödeme fazlası): {avansaYazilacak:N2} ₺");
                await _db.SaveChangesAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return avansaYazilacak > 0
            ? ServiceResult.Success($"Ödeme alındı. {anaBorcaYazilacak:N2} ₺ borca, {avansaYazilacak:N2} ₺ avansa yazıldı.")
            : ServiceResult.Success("Ödeme başarıyla alındı.");
    }

    public async Task<ServiceResult> KompleKapatAsync(List<int> veresiyeIds, decimal odenenTutar, string? kullaniciId, VeresiyeOdemeTipi odemeTipi = VeresiyeOdemeTipi.Nakit)
    {
        if (veresiyeIds == null || !veresiyeIds.Any())
            return ServiceResult.Failure("Seçili veresiye bulunamadı.");
        if (odenenTutar <= 0)
            return ServiceResult.Failure("Ödeme tutarı 0'dan büyük olmalıdır.");

        var veresiyeler = await _db.Veresiyeler
            .Include(v => v.Odemeler)
            .Include(v => v.Musteri)
            .Where(v => veresiyeIds.Contains(v.Id))
            .ToListAsync();

        if (!veresiyeler.Any())
            return ServiceResult.Failure("Veresiye kayıtları bulunamadı.");

        var toplamKalan = veresiyeler.Sum(GetKalanBorc);
        var anaBorcaYazilacak = Math.Min(odenenTutar, toplamKalan);
        var avansaYazilacak = odenenTutar - anaBorcaYazilacak;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Aynı müşteri borçları sırayla kapanır, kasa tek toplamı alır.
            decimal kalanOdeme = anaBorcaYazilacak;
            foreach (var v in veresiyeler.OrderBy(x => x.Tarih))
            {
                if (kalanOdeme <= 0)
                    break;

                var kalan = GetKalanBorc(v);
                if (kalan <= 0)
                    continue;

                var buVeresiyeOdenen = Math.Min(kalanOdeme, kalan);
                v.Odemeler.Add(CreateOdemeKaydi(v.Id, buVeresiyeOdenen, "Komple kapatma", kullaniciId, odemeTipi));

                v.OdenmeDurumu = buVeresiyeOdenen >= kalan ? OdenmeDurumu.Odendi : OdenmeDurumu.KismiOdendi;
                kalanOdeme -= buVeresiyeOdenen;
            }

            // Fazla tutar avans olarak yeni veresiye oluşturur (ilk cariye bağlanır)
            Veresiye? avansVeresiye = null;
            if (avansaYazilacak > 0)
            {
                var ilkVeresiye = veresiyeler.First();
                avansVeresiye = new Veresiye
                {
                    CariId = ilkVeresiye.CariId,
                    MusteriId = ilkVeresiye.MusteriId,
                    Tutar = avansaYazilacak,
                    Tarih = DateTime.UtcNow,
                    OdenmeDurumu = OdenmeDurumu.Bekliyor,
                    Tip = VeresiyeTipi.Avans,
                    Aciklama = $"Toplu tahsilat fazlası ({veresiyeIds.Count} kayıt)"
                };
                _db.Veresiyeler.Add(avansVeresiye);
            }

            _kasaService.KasaGelirEkle(odenenTutar, "Veresiye Ödeme", $"Müşteri: {veresiyeler.First().Musteri.Ad} {veresiyeler.First().Musteri.Soyad} - Komple Kapatma ({veresiyeIds.Count} kayıt)" + (avansaYazilacak > 0 ? $" (avans: {avansaYazilacak:N2} ₺)" : string.Empty));

            _auditLog.LogHazirla("Veresiye", veresiyeler.First().Id, "Guncellendi", aciklama: $"Komple Kapatma: {odenenTutar:N2} ₺ ({veresiyeIds.Count} kayıt)" + (avansaYazilacak > 0 ? $" (avansa yazılan: {avansaYazilacak:N2} ₺)" : string.Empty));

            await _db.SaveChangesAsync();

            if (avansVeresiye != null && avansVeresiye.Id > 0)
            {
                _auditLog.LogHazirla("Veresiye", avansVeresiye.Id, "Eklendi", yeniDeger: avansVeresiye, aciklama: $"Avans oluştu (toplu tahsilat fazlası): {avansaYazilacak:N2} ₺");
                await _db.SaveChangesAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return avansaYazilacak > 0
            ? ServiceResult.Success($"{veresiyeIds.Count} veresiye kapatıldı. Toplam: {odenenTutar:N2} ₺ (avansa yazılan: {avansaYazilacak:N2} ₺).")
            : ServiceResult.Success($"{veresiyeIds.Count} veresiye komple kapatıldı. Toplam: {odenenTutar:N2} ₺");
    }

    public async Task<ServiceResult> AvansEkleAsync(int cariId, decimal tutar, string? aciklama, string? kullaniciId, VeresiyeOdemeTipi odemeTipi = VeresiyeOdemeTipi.Nakit)
    {
        if (tutar <= 0)
            return ServiceResult.Failure("Avans tutarı 0'dan büyük olmalıdır.");

        var cari = await _db.Cariler.FirstOrDefaultAsync(c => c.Id == cariId);

        if (cari == null)
            return ServiceResult.Failure("Cari bulunamadı.");

        // Avans sadece müşteri rolü olan cariler için mantıklı.
        var musteri = await _db.Musteriler.FirstOrDefaultAsync(m => m.CariId == cariId);
        if (musteri == null)
            return ServiceResult.Failure("Avans eklemek için carinin müşteri kaydı olmalı. Önce 'Müşteri' rolünü aktive edin.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var avans = new Veresiye
            {
                CariId = cari.Id,
                MusteriId = musteri.Id,
                Tutar = tutar,
                Tarih = DateTime.UtcNow,
                OdenmeDurumu = OdenmeDurumu.Bekliyor,
                Tip = VeresiyeTipi.Avans,
                Aciklama = aciklama?.Trim()
            };
            _db.Veresiyeler.Add(avans);

            _kasaService.KasaGelirEkle(
                tutar,
                "Cari Avans",
                $"Cari: {cari.Ad} - Avans yatırdı (kullanici: {kullaniciId ?? "sistem"})");

            await _db.SaveChangesAsync();

            _auditLog.LogHazirla(
                "Veresiye",
                avans.Id,
                "Eklendi",
                yeniDeger: avans,
                aciklama: $"Manuel avans eklendi: {tutar:N2} ₺");
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success($"{tutar:N2} ₺ avans olarak hesaba eklendi.");
    }

    /// <summary>
    /// Carinin avanslarından (Tip=Avans) FIFO sırayla düşer. Kalan tutarı döner.
    /// Bu metot transaction yönetmez — çağıran taraf bir transaction içinde olmalıdır.
    /// Kasaya para GİRMEZ — sadece müşterinin kendi bakiyesinden düşer.
    /// </summary>
    public async Task<AvansDusResult> AvansDusCoreAsync(int cariId, decimal tutar, string? aciklama, string? kullaniciId)
    {
        var result = new AvansDusResult();
        if (tutar <= 0 || cariId <= 0) return result;

        var avansVeresiyeler = await _db.Veresiyeler
            .Include(v => v.Odemeler)
            .Where(v => v.CariId == cariId
                && v.Tip == VeresiyeTipi.Avans
                && v.OdenmeDurumu != OdenmeDurumu.Iptal)
            .OrderBy(v => v.Tarih)
            .ThenBy(v => v.Id)
            .ToListAsync();

        decimal kalanTutar = tutar;

        foreach (var avans in avansVeresiyeler)
        {
            if (kalanTutar <= 0) break;

            var avansKalan = avans.Tutar - avans.Odemeler.Sum(o => o.OdemeTutari);
            if (avansKalan <= 0) continue;

            var dusulecek = Math.Min(kalanTutar, avansKalan);

            // Avans veresiyesine "kullanım" kaydı ekle (VeresiyeOdeme tablosunu yeniden kullanıyoruz —
            // semantik olarak "para alındı" değil ama Tutar/Sum hesabı aynı şekilde çalışıyor).
            var odeme = CreateOdemeKaydi(
                avans.Id,
                dusulecek,
                aciklama?.Trim(),
                kullaniciId,
                VeresiyeOdemeTipi.Nakit);
            avans.Odemeler.Add(odeme);
            _db.VeresiyeOdemeler.Add(odeme);

            avans.OdenmeDurumu = CalculatePaymentStatus(avans.Tutar, avans.Odemeler.Sum(o => o.OdemeTutari));
            _db.Veresiyeler.Update(avans);

            result.KullanilanAvanslar.Add((avans.Id, dusulecek));
            result.ToplamDusulen += dusulecek;
            kalanTutar -= dusulecek;
        }

        result.KalanTutar = kalanTutar;
        return result;
    }

    private IQueryable<Veresiye> ApplyFilters(IQueryable<Veresiye> query, int? musteriId, OdenmeDurumu? durum, DateTime? baslangic, DateTime? bitis)
    {
        if (musteriId.HasValue && musteriId.Value > 0)
            query = query.Where(v => v.MusteriId == musteriId.Value);

        if (durum.HasValue)
            query = query.Where(v => v.OdenmeDurumu == durum.Value);

        if (baslangic.HasValue)
            query = query.Where(v => v.Tarih >= baslangic.Value.Date);

        if (bitis.HasValue)
            query = query.Where(v => v.Tarih < bitis.Value.Date.AddDays(1));

        return query;
    }

    private async Task<ServiceResult> CreateAsync(VeresiyeVM vm)
    {
        var musteri = await _db.Musteriler.FindAsync(vm.MusteriId);
        if (musteri == null)
            return ServiceResult.Failure("Müşteri bulunamadı.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Önce avanstan düş (FIFO). Kalan tutar veresiye olarak yazılır.
            var avansSonuc = await AvansDusCoreAsync(
                musteri.CariId ?? 0,
                vm.Tutar,
                vm.Aciklama?.Trim() ?? "Manuel veresiye avans kullanımı",
                null);

            if (avansSonuc.KalanTutar <= 0)
            {
                // Avans tamamen karşıladı — yeni veresiye açılmaz, sadece avans kullanıldı.
                _auditLog.LogHazirla(
                    "Veresiye",
                    0,
                    "Eklendi",
                    aciklama: $"Veresiye talebi ({vm.Tutar:N2} ₺) avans tarafından karşılandı: {musteri.Ad} {musteri.Soyad}");
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return ServiceResult.Success($"Veresiye avanstan karşılandı. Kullanılan avans: {avansSonuc.ToplamDusulen:N2} ₺");
            }

            var veresiye = new Veresiye
            {
                CariId = musteri.CariId,
                MusteriId = vm.MusteriId,
                Tutar = avansSonuc.KalanTutar,
                Aciklama = vm.Aciklama?.Trim(),
                Tarih = DateTime.UtcNow,
                OdenmeDurumu = OdenmeDurumu.Bekliyor,
                Tip = vm.Tip
            };

            _db.Veresiyeler.Add(veresiye);

            _auditLog.LogHazirla("Veresiye", veresiye.Id, "Eklendi", yeniDeger: veresiye, aciklama: $"Veresiye Borç Eklendi: {musteri.Ad} {musteri.Soyad}" + (avansSonuc.ToplamDusulen > 0 ? $" (avans düşüldü: {avansSonuc.ToplamDusulen:N2} ₺)" : string.Empty));

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success("Veresiye başarıyla eklendi.");
    }

    private async Task<ServiceResult> UpdateAsync(VeresiyeVM vm)
    {
        var veresiye = await _db.Veresiyeler
            .Include(v => v.Musteri)
            .Include(v => v.Odemeler)
            .FirstOrDefaultAsync(v => v.Id == vm.Id);

        if (veresiye == null)
            return ServiceResult.Failure("Veresiye kaydı bulunamadı.");

        var odenenToplam = veresiye.Odemeler.Sum(o => o.OdemeTutari);
        if (vm.Tutar < odenenToplam)
            return ServiceResult.Failure($"Veresiye tutarı, halihazırda yapılmış ödemelerin toplamından ({odenenToplam:N2} ₺) az olamaz.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            veresiye.Tutar = vm.Tutar;
            veresiye.Aciklama = vm.Aciklama?.Trim();
            veresiye.Tip = vm.Tip;
            veresiye.CariId = veresiye.Musteri.CariId;
            veresiye.OdenmeDurumu = CalculatePaymentStatus(vm.Tutar, odenenToplam);

            _db.Veresiyeler.Update(veresiye);
            
            _auditLog.LogHazirla("Veresiye", veresiye.Id, "Guncellendi", yeniDeger: veresiye, aciklama: $"Veresiye Düzeltildi: {veresiye.Musteri.Ad} {veresiye.Musteri.Soyad}");
            
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success("Veresiye kaydı başarıyla düzeltildi.");
    }

    private static decimal GetOdenenToplam(Veresiye veresiye) => veresiye.Odemeler.Sum(o => o.OdemeTutari);

    private static decimal GetKalanBorc(Veresiye veresiye) => veresiye.Tutar - GetOdenenToplam(veresiye);

    private static ServiceResult? ValidateOdemeTutari(decimal kalanBorc, decimal tutar)
    {
        if (tutar <= 0)
            return ServiceResult.Failure("Ödeme tutarı 0'dan büyük olmalıdır.");

        if (tutar > kalanBorc)
            return ServiceResult.Failure($"Ödeme tutarı kalan borçtan ({kalanBorc:N2} ₺) fazla olamaz.");

        return null;
    }

    private static VeresiyeOdeme CreateOdemeKaydi(
        int veresiyeId,
        decimal odemeTutari,
        string? aciklama,
        string? kullaniciId,
        VeresiyeOdemeTipi odemeTipi)
    {
        return new VeresiyeOdeme
        {
            VeresiyeId = veresiyeId,
            OdemeTutari = odemeTutari,
            OdemeTarihi = DateTime.UtcNow,
            Aciklama = aciklama?.Trim(),
            KullaniciId = kullaniciId,
            OdemeTipi = odemeTipi
        };
    }

    private static OdenmeDurumu CalculatePaymentStatus(decimal toplamTutar, decimal odenenToplam)
    {
        if (odenenToplam >= toplamTutar)
            return OdenmeDurumu.Odendi;

        if (odenenToplam > 0)
            return OdenmeDurumu.KismiOdendi;

        return OdenmeDurumu.Bekliyor;
    }
}

/// <summary>
/// AvansDusCoreAsync'in döndüğü sonuç: hangi avanslardan ne kadar düşüldü, kalan ne kadar.
/// </summary>
public class AvansDusResult
{
    public List<(int VeresiyeId, decimal Tutar)> KullanilanAvanslar { get; } = new();
    public decimal ToplamDusulen { get; set; }
    public decimal KalanTutar { get; set; }
    public bool Tamamlandi => KalanTutar <= 0;
}
