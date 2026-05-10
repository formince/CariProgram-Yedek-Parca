using CariErinc.Data;
using CariErinc.Models;
using CariErinc.Helpers;
using CariErinc.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class GiderKategoriService : IGiderKategoriService
{
    private readonly AppDbContext _db;

    public GiderKategoriService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<GiderKategori>> GetAllAsync(KasaHareketTipi? tip = null) 
    {
        var query = _db.GiderKategoriler.Where(k => k.AktifMi);
        if (tip.HasValue)
            query = query.Where(k => k.Tip == tip.Value);
            
        return await query.OrderBy(k => k.Ad).ToListAsync();
    }

    public async Task<GiderKategori?> GetByIdAsync(int id) 
    {
        return await _db.GiderKategoriler.FindAsync(id);
    }

    public async Task<ServiceResult> SaveAsync(GiderKategori kategori)
    {
        if (string.IsNullOrWhiteSpace(kategori.Ad))
            return ServiceResult.Failure("Kategori adı boş olamaz.");

        kategori.Ad = kategori.Ad.Trim();

        if (kategori.Id == 0) // Ekleme
        {
            kategori.SilinebilirMi = true; 
            kategori.AktifMi = true;
            _db.GiderKategoriler.Add(kategori);
            await _db.SaveChangesAsync();
            return ServiceResult.Success("Kategori başarıyla eklendi.");
        }
        else // Güncelleme
        {
            var existing = await _db.GiderKategoriler.FindAsync(kategori.Id);
            if (existing == null)
                return ServiceResult.Failure("Kategori bulunamadı.");

            existing.Ad = kategori.Ad;
            existing.AktifMi = kategori.AktifMi;
            
            _db.GiderKategoriler.Update(existing);
            await _db.SaveChangesAsync();
            return ServiceResult.Success("Kategori başarıyla güncellendi.");
        }
    }

    public async Task<ServiceResult> SilAsync(int id)
    {
        var kategori = await _db.GiderKategoriler.FindAsync(id);
        if (kategori == null)
            return ServiceResult.Failure("Kategori bulunamadı.");

        if (!kategori.SilinebilirMi)
            return ServiceResult.Failure("Sistem kategorileri silinemez.");

        _db.GiderKategoriler.Remove(kategori);
        await _db.SaveChangesAsync();
        return ServiceResult.Success("Kategori başarıyla silindi.");
    }
}
