using CariErinc.Data;
using CariErinc.Helpers;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Controllers;

[Authorize]
public class RolController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IYetkiCacheService _yetkiCache;

    public RolController(AppDbContext db, IYetkiCacheService yetkiCache)
    {
        _db = db;
        _yetkiCache = yetkiCache;
    }

    public async Task<IActionResult> Index()
    {
        if (YetkiKontrol() is { } r) return r;
        ViewData["Title"] = "Roller";
        var roller = await _db.Roller
            .Include(r => r.KullaniciRoller)
            .Include(r => r.Yetkiler)
            .OrderBy(r => r.Ad)
            .ToListAsync();
        return View(roller);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id)
    {
        if (YetkiKontrol() is { } r) return r;

        if (id.HasValue && id.Value > 0)
        {
            var rol = await _db.Roller.FindAsync(id.Value);
            if (rol is null) return RedirectToAction(nameof(Index));
            
            ViewData["Title"] = $"Rol Düzenle: {rol.Ad}";
            return View(new RolVM { Id = rol.Id, Ad = rol.Ad, Aciklama = rol.Aciklama, IsAdmin = rol.IsAdmin });
        }

        ViewData["Title"] = "Yeni Rol";
        return View(new RolVM());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(RolVM vm)
    {
        if (YetkiKontrol() is { } r) return r;
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = vm.Id > 0 ? "Rol Düzenle" : "Yeni Rol";
            return View("Form", vm);
        }

        if (vm.Id == 0) // Create
        {
            _db.Roller.Add(new Rol
            {
                Ad = vm.Ad.Trim(),
                Aciklama = vm.Aciklama?.Trim(),
                IsAdmin = vm.IsAdmin
            });
            TempBasarili("Rol oluşturuldu.");
        }
        else // Edit
        {
            var rol = await _db.Roller.FindAsync(vm.Id);
            if (rol is null) return RedirectToAction(nameof(Index));

            rol.Ad = vm.Ad.Trim();
            rol.Aciklama = vm.Aciklama?.Trim();
            rol.IsAdmin = vm.IsAdmin;
            _yetkiCache.InvalidateRol(rol.Id);
            TempBasarili("Rol güncellendi.");
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        if (YetkiKontrol() is { } r) return r;
        var rol = await _db.Roller.FindAsync(id);
        if (rol is null) return Json(new { success = false, message = "Rol bulunamadı." });
        if (rol.IsAdmin) return Json(new { success = false, message = "Admin rolü silinemez." });

        _db.Roller.Remove(rol);
        await _db.SaveChangesAsync();
        _yetkiCache.InvalidateRol(id);
        
        return Json(new { success = true, message = "Rol başarıyla silindi." });
    }

    // ── Yetki Düzenleme ──────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Yetkiler(int id)
    {
        if (YetkiKontrol() is { } r) return r;
        var rol = await _db.Roller.Include(r => r.Yetkiler).FirstOrDefaultAsync(r => r.Id == id);
        if (rol is null) return RedirectToAction(nameof(Index));

        ViewData["Title"] = $"Yetkiler: {rol.Ad}";

        var kesfedilen = RouteKesfedici.Tara();

        var atanmis = rol.Yetkiler
            .Select(y => y.ControllerAdi + "/" + y.ActionAdi)
            .ToHashSet();

        var sidebarAyarlari = rol.Yetkiler.ToDictionary(
            y => y.ControllerAdi + "/" + y.ActionAdi,
            y => new RolYetkiSatirVM
            {
                SidebarGrubu = y.SidebarGrubu ?? "",
                SidebarGoruntuAdi = y.SidebarGoruntuAdi ?? "",
                SidebarSira = y.SidebarSira
            });

        var gruplari = kesfedilen
            .GroupBy(k => k.Controller)
            .Select(g => new ControllerGrubu
            {
                Controller = g.Key,
                Actions = g.Select(x => x.Action).OrderBy(a => a).ToList()
            })
            .OrderBy(g => g.Controller)
            .ToList();

        var vm = new RolYetkiDuzenleVM
        {
            RolId = rol.Id,
            RolAdi = rol.Ad,
            IsAdmin = rol.IsAdmin,
            AtanmisYetkiler = atanmis,
            ControllerGruplari = gruplari,
            SidebarAyarlari = sidebarAyarlari
        };
        return View(vm);
    }

    /// <summary>Checkbox toggle: yetki ekle/kaldır (AJAX).</summary>
    [HttpPost]
    public async Task<IActionResult> YetkiToggle([FromBody] YetkiToggleRequest req)
    {
        if (!User.HasClaim("is_admin", "true"))
            return Forbid();

        var rol = await _db.Roller.Include(r => r.Yetkiler)
            .FirstOrDefaultAsync(r => r.Id == req.RolId);
        if (rol is null) return NotFound();

        var mevcut = rol.Yetkiler
            .FirstOrDefault(y => y.ControllerAdi == req.Controller && y.ActionAdi == req.Action);

        if (req.Ekle)
        {
            if (mevcut is null)
            {
                rol.Yetkiler.Add(new RolYetki
                {
                    RolId = req.RolId,
                    ControllerAdi = req.Controller,
                    ActionAdi = req.Action
                });
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            if (mevcut is not null)
            {
                _db.RolYetkiler.Remove(mevcut);
                await _db.SaveChangesAsync();
            }
        }

        _yetkiCache.InvalidateRol(req.RolId);
        return Ok(new { basarili = true });
    }

    /// <summary>Sidebar ayarlarını güncelle (AJAX).</summary>
    [HttpPost]
    public async Task<IActionResult> SidebarAyarGuncelle([FromBody] SidebarAyarRequest req)
    {
        if (!User.HasClaim("is_admin", "true"))
            return Forbid();

        var yetki = await _db.RolYetkiler
            .FirstOrDefaultAsync(y => y.RolId == req.RolId
                && y.ControllerAdi == req.Controller
                && y.ActionAdi == req.Action);

        if (yetki is null) return NotFound();

        yetki.SidebarGrubu = string.IsNullOrWhiteSpace(req.SidebarGrubu) ? null : req.SidebarGrubu.Trim().ToUpper();
        yetki.SidebarGoruntuAdi = string.IsNullOrWhiteSpace(req.SidebarGoruntuAdi) ? null : req.SidebarGoruntuAdi.Trim();
        yetki.SidebarSira = req.SidebarSira;
        await _db.SaveChangesAsync();
        _yetkiCache.InvalidateRol(req.RolId);
        return Ok(new { basarili = true });
    }
}

public record YetkiToggleRequest(int RolId, string Controller, string Action, bool Ekle);
public record SidebarAyarRequest(int RolId, string Controller, string Action,
    string SidebarGrubu, string SidebarGoruntuAdi, int SidebarSira);
