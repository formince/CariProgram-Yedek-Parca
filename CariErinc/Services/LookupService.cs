using CariErinc.Data;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Services;

public class LookupService : ILookupService
{
    private readonly AppDbContext _db;
    private readonly IAyarService _ayarService;

    public LookupService(AppDbContext db, IAyarService ayarService)
    {
        _db = db;
        _ayarService = ayarService;
    }

    public async Task<int> GetVarsayilanKdvAsync()
    {
        var oranlar = (await _ayarService.GetKdvOranlariListeAsync()).ToList();
        var varsayilan = int.TryParse(await _ayarService.GetAsync("VarsayilanKdv"), out var k) ? k : 20;
        return oranlar.Contains(varsayilan) ? varsayilan : (oranlar.FirstOrDefault());
    }

    public async Task<SelectList> GetKdvOranlariAsync(int currentKdv)
    {
        var oranlar = (await _ayarService.GetKdvOranlariListeAsync()).ToList();
        
        if (!oranlar.Contains(currentKdv))
            oranlar.Add(currentKdv);
            
        oranlar.Sort();
        
        return new SelectList(
            oranlar.Select(o => new SelectListItem(o == 0 ? "%0 (KDV Yok)" : $"%{o}", o.ToString())),
            "Value", "Text", currentKdv.ToString());
    }

    public async Task<SelectList> GetTedarikcilerAsync(int? currentSelected = null)
    {
        var tedarikciler = await _db.Tedarikciler
            .Include(t => t.Cari)
            .Where(t => t.Cari != null && ((t.Cari.Rol & CariRol.Tedarikci) == CariRol.Tedarikci))
            .OrderBy(t => t.Cari!.Ad)
            .ToListAsync();
        
        var list = new List<SelectListItem> { new("-- Cari Seçin --", "") };
        list.AddRange(tedarikciler.Select(t => new SelectListItem(t.Cari!.Ad, t.Id.ToString())));
        
        return new SelectList(list, "Value", "Text", currentSelected?.ToString());
    }

    public async Task<SelectList> GetUrunKategorileriAsync(string? currentSelected = null)
    {
        var kategoriler = await _db.Urunler
            .Where(u => !string.IsNullOrEmpty(u.Kategori))
            .Select(u => u.Kategori)
            .Distinct()
            .OrderBy(k => k)
            .ToListAsync();
            
        return new SelectList(kategoriler, currentSelected);
    }

    public async Task<SelectList> GetMusteriSelectListAsync(int? currentSelected = null, string emptyText = "-- Müşteri Seçin --")
    {
        var musteriler = await _db.Musteriler
            .Include(m => m.Cari)
            .Where(m => m.Cari != null && ((m.Cari.Rol & CariRol.Musteri) == CariRol.Musteri))
            .OrderBy(m => m.Cari!.Ad)
            .ToListAsync();
        var list = new List<SelectListItem> { new(emptyText, "") };
        list.AddRange(musteriler.Select(m => new SelectListItem(m.Cari!.Ad, m.Id.ToString())));
        
        return new SelectList(list, "Value", "Text", currentSelected?.ToString() ?? "");
    }

    public async Task<SelectList> GetUrunSelectListAsync(int? currentSelected = null)
    {
        var urunler = await _db.Urunler.OrderBy(u => u.Ad).ToListAsync();
        return new SelectList(urunler.Select(u => new SelectListItem(
            string.IsNullOrEmpty(u.Barkod) ? u.Ad : $"{u.Ad} ({u.Barkod})", 
            u.Id.ToString())), "Value", "Text", currentSelected?.ToString() ?? "");
    }
}
