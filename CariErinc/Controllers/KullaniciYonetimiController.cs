using CariErinc.Data;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Controllers;

[Authorize]
public class KullaniciYonetimiController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IYetkiCacheService _yetkiCache;

    public KullaniciYonetimiController(AppDbContext db, IYetkiCacheService yetkiCache)
    {
        _db = db;
        _yetkiCache = yetkiCache;
    }

    public async Task<IActionResult> Index()
    {
        if (YetkiKontrol() is { } r) return r;
        ViewData["Title"] = "Kullanıcı Yönetimi";

        var kullanicilar = await _db.Kullanicilar
            .Include(k => k.KullaniciRoller)
                .ThenInclude(kr => kr.Rol)
            .OrderBy(k => k.KullaniciAdi)
            .ToListAsync();

        var vm = kullanicilar.Select(k => new KullaniciIndexVM
        {
            Id = k.Id,
            KullaniciAdi = k.KullaniciAdi,
            AktifMi = k.AktifMi,
            Roller = k.KullaniciRoller.Select(kr => kr.Rol.Ad).ToList(),
            OlusturulmaTarihi = k.OlusturulmaTarihi
        }).ToList();

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id)
    {
        if (YetkiKontrol() is { } r) return r;

        if (id.HasValue && id.Value > 0)
        {
            var k = await _db.Kullanicilar.Include(x => x.KullaniciRoller).FirstOrDefaultAsync(x => x.Id == id);
            if (k is null) return RedirectToAction(nameof(Index));

            ViewData["Title"] = $"Kullanıcı: {k.KullaniciAdi}";
            var vm = new KullaniciYonetimVM
            {
                Id = k.Id,
                KullaniciAdi = k.KullaniciAdi,
                AktifMi = k.AktifMi,
                SeciliRolIds = k.KullaniciRoller.Select(kr => kr.RolId).ToList()
            };
            return View(await BuildVm(vm));
        }

        ViewData["Title"] = "Yeni Kullanıcı";
        return View(await BuildVm(new KullaniciYonetimVM()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(KullaniciYonetimVM vm)
    {
        if (YetkiKontrol() is { } r) return r;

        // Edit modunda şifre boşsa validasyon hatası verme
        if (vm.Id > 0 && string.IsNullOrEmpty(vm.Sifre))
            ModelState.Remove("Sifre");
        else if (vm.Id == 0 && string.IsNullOrWhiteSpace(vm.Sifre))
            ModelState.AddModelError("Sifre", "Yeni kullanıcı için şifre zorunludur.");

        if (!ModelState.IsValid)
            return View("Form", await BuildVm(vm));

        if (vm.Id == 0) // Create
        {
            if (await _db.Kullanicilar.AnyAsync(k => k.KullaniciAdi == vm.KullaniciAdi))
            {
                ModelState.AddModelError("KullaniciAdi", "Bu kullanıcı adı zaten kullanılmakta.");
                return View("Form", await BuildVm(vm));
            }

            var kullanici = new Kullanici
            {
                KullaniciAdi = vm.KullaniciAdi.Trim(),
                SifreHash = BCrypt.Net.BCrypt.HashPassword(vm.Sifre!),
                AktifMi = vm.AktifMi,
                OlusturulmaTarihi = DateTime.UtcNow
            };
            _db.Kullanicilar.Add(kullanici);
            await _db.SaveChangesAsync();

            foreach (var rolId in vm.SeciliRolIds)
                _db.KullaniciRoller.Add(new KullaniciRol { KullaniciId = kullanici.Id, RolId = rolId });
            
            TempBasarili($"Kullanıcı '{kullanici.KullaniciAdi}' oluşturuldu.");
        }
        else // Edit
        {
            var k = await _db.Kullanicilar.Include(x => x.KullaniciRoller).FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (k is null) return RedirectToAction(nameof(Index));

            if (await _db.Kullanicilar.AnyAsync(x => x.KullaniciAdi == vm.KullaniciAdi && x.Id != vm.Id))
            {
                ModelState.AddModelError("KullaniciAdi", "Bu kullanıcı adı başka bir hesapta kullanılıyor.");
                return View("Form", await BuildVm(vm));
            }

            k.KullaniciAdi = vm.KullaniciAdi.Trim();
            k.AktifMi = vm.AktifMi;
            if (!string.IsNullOrWhiteSpace(vm.Sifre))
                k.SifreHash = BCrypt.Net.BCrypt.HashPassword(vm.Sifre);

            _db.KullaniciRoller.RemoveRange(k.KullaniciRoller);
            foreach (var rolId in vm.SeciliRolIds)
                _db.KullaniciRoller.Add(new KullaniciRol { KullaniciId = k.Id, RolId = rolId });

            TempBasarili("Kullanıcı güncellendi.");
        }

        await _db.SaveChangesAsync();
        _yetkiCache.InvalidateAll();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        if (YetkiKontrol() is { } r) return r;

        var aktifKullanici = User.FindFirst("kullanici_id")?.Value;
        if (aktifKullanici == id.ToString())
            return Json(new { success = false, message = "Kendi hesabınızı silemezsiniz." });

        var k = await _db.Kullanicilar.FindAsync(id);
        if (k is null) return Json(new { success = false, message = "Kullanıcı bulunamadı." });

        _db.Kullanicilar.Remove(k);
        await _db.SaveChangesAsync();
        _yetkiCache.InvalidateAll();
        
        return Json(new { success = true, message = $"Kullanıcı '{k.KullaniciAdi}' silindi." });
    }

    private async Task<KullaniciYonetimVM> BuildVm(KullaniciYonetimVM vm)
    {
        var roller = await _db.Roller.OrderBy(r => r.Ad).ToListAsync();
        vm.RolListesi = roller.Select(r => new SelectListItem(r.Ad, r.Id.ToString())).ToList();
        return vm;
    }
}
