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
        var veresiye = await _db.Veresiyeler.Include(v => v.Odemeler).Include(v => v.Musteri).FirstOrDefaultAsync(v => v.Id == veresiyeId);
        if (veresiye == null)
            return ServiceResult.Failure("Veresiye bulunamadı.");

        var odemeValidasyonu = ValidateOdemeTutari(GetKalanBorc(veresiye), tutar);
        if (odemeValidasyonu != null)
            return odemeValidasyonu;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Adım 2: Ödeme kaydı oluştur ve veresiyeye ekle
            var odeme = CreateOdemeKaydi(veresiye.Id, tutar, aciklama, kullaniciId, odemeTipi);

            veresiye.Odemeler.Add(odeme);
            _db.VeresiyeOdemeler.Add(odeme);

            // Adım 3: Ödeme durumunu güncelle (bekliyor → kısmi → ödendi)
            var yeniOdenenToplam = GetOdenenToplam(veresiye);
            veresiye.OdenmeDurumu = CalculatePaymentStatus(veresiye.Tutar, yeniOdenenToplam);

            _db.Veresiyeler.Update(veresiye);

            // Adım 4: Kasaya gelir ekle
            _kasaService.KasaGelirEkle(tutar, "Veresiye Ödeme", $"Müşteri: {veresiye.Musteri.Ad} {veresiye.Musteri.Soyad} - Veresiye #{veresiye.Id}");

            // Adım 5: Audit log hazırla
            _auditLog.LogHazirla("Veresiye", veresiye.Id, "Guncellendi", yeniDeger: veresiye, aciklama: $"Veresiye Ödemesi Alındı: {tutar:N2} ₺");

            // Adım 6: SaveChanges + Commit
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success("Ödeme başarıyla alındı.");
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
        var toplamOdemeValidasyonu = ValidateOdemeTutari(toplamKalan, odenenTutar);
        if (toplamOdemeValidasyonu != null)
            return toplamOdemeValidasyonu;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Aynı müşteri borçları sırayla kapanır, kasa tek toplamı alır.
            decimal kalanOdeme = odenenTutar;
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

            _kasaService.KasaGelirEkle(odenenTutar, "Veresiye Ödeme", $"Müşteri: {veresiyeler.First().Musteri.Ad} {veresiyeler.First().Musteri.Soyad} - Komple Kapatma ({veresiyeIds.Count} kayıt)");
            
            _auditLog.LogHazirla("Veresiye", veresiyeler.First().Id, "Guncellendi", aciklama: $"Komple Kapatma: {odenenTutar:N2} ₺ ({veresiyeIds.Count} kayıt)");
            
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ServiceResult.Success($"{veresiyeIds.Count} veresiye komple kapatıldı. Toplam: {odenenTutar:N2} ₺");
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

        var veresiye = new Veresiye
        {
            CariId = musteri.CariId,
            MusteriId = vm.MusteriId,
            Tutar = vm.Tutar,
            Aciklama = vm.Aciklama?.Trim(),
            Tarih = DateTime.UtcNow,
            OdenmeDurumu = OdenmeDurumu.Bekliyor,
            Tip = vm.Tip
        };

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Veresiyeler.Add(veresiye);
            
            _auditLog.LogHazirla("Veresiye", veresiye.Id, "Eklendi", yeniDeger: veresiye, aciklama: $"Veresiye Borç Eklendi: {musteri.Ad} {musteri.Soyad}");
            
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
