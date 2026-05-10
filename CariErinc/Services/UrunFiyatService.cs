using CariErinc.Data;
using CariErinc.Models;
using CariErinc.Helpers;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class UrunFiyatService : IUrunFiyatService
{
    private readonly AppDbContext _db;

    public UrunFiyatService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<UrunFiyatUpdateResult>> UpdateAlisFiyatiAsync(
        int urunId,
        decimal yeniFiyat,
        string neden,
        string kullanici,
        int? alisId = null)
    {
        // Ürünü getir
        var urun = await _db.Urunler.FindAsync(urunId);
        if (urun == null)
            return ServiceResult<UrunFiyatUpdateResult>.Failure($"Ürün ID {urunId} bulunamadı.");

        // Validation
        if (yeniFiyat < 0)
            return ServiceResult<UrunFiyatUpdateResult>.Failure("Alış fiyatı 0'dan küçük olamaz.");

        // Değişim kontrolü
        if (Math.Abs(urun.AlisFiyati - yeniFiyat) < 0.01m) // Decimal floating point check
        {
            return ServiceResult<UrunFiyatUpdateResult>.Success(new UrunFiyatUpdateResult { IsChanged = false }, "Fiyat değişimi yok.");
        }

        // Audit log kaydı
        var audit = new UrunFiyatAudit
        {
            UrunId = urunId,
            EskiFiyat = urun.AlisFiyati,
            YeniFiyat = yeniFiyat,
            Neden = neden,
            KullaniciAdi = kullanici,
            AlisId = alisId,
            Tarih = DateTime.UtcNow
        };

        _db.UrunFiyatAuditlari.Add(audit);

        // Ürün fiyatını güncelle
        urun.AlisFiyati = yeniFiyat;
        urun.SonAlisTarihi = DateTime.UtcNow;
        urun.GuncellenmeTarihi = DateTime.UtcNow;

        _db.Urunler.Update(urun);
        await _db.SaveChangesAsync();

        var resultData = new UrunFiyatUpdateResult
        {
            IsChanged = true,
            OldPrice = audit.EskiFiyat,
            NewPrice = yeniFiyat,
            ChangedAt = audit.Tarih
        };

        return ServiceResult<UrunFiyatUpdateResult>.Success(resultData, "Ürün fiyatı başarıyla güncellendi.");
    }

    public async Task<List<UrunFiyatAudit>> GetFiyatGecmisiAsync(int urunId)
    {
        return await _db.UrunFiyatAuditlari
            .Where(a => a.UrunId == urunId)
            .OrderByDescending(a => a.Tarih)
            .ToListAsync();
    }

    public async Task<UrunFiyatAudit?> GetSonFiyatDegisimAsync(int urunId)
    {
        return await _db.UrunFiyatAuditlari
            .Where(a => a.UrunId == urunId)
            .OrderByDescending(a => a.Tarih)
            .FirstOrDefaultAsync();
    }
}
