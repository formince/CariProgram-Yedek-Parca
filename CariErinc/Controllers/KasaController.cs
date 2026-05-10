using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CariErinc.Controllers;

[Authorize]
public class KasaController : BaseController
{
    private readonly IKasaService _kasaService;
    private readonly IGiderKategoriService _kategoriService;

    public KasaController(IKasaService kasaService, IGiderKategoriService kategoriService)
    {
        _kasaService = kasaService;
        _kategoriService = kategoriService;
    }

    public async Task<IActionResult> Index(DateTime? baslangic, DateTime? bitis, int page = 1, string? search = null, int? kategoriId = null, KasaHareketTipi? tip = null)
    {
        ViewData["Title"] = "Kasa";
        var vm = await _kasaService.GetKasaVerileriAsync(baslangic, bitis, page, search, kategoriId, tip);
        
        ViewBag.Baslangic = baslangic?.ToString("yyyy-MM-dd");
        ViewBag.Bitis = bitis?.ToString("yyyy-MM-dd");
        ViewBag.Search = search;
        ViewBag.KategoriId = kategoriId;
        ViewBag.Tip = tip;
        
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id)
    {
        // Kasa hareketleri genelde düzenlenmez, sadece yeni kayıt eklenir.
        // Ancak mimari uyum için Form adını kullanıyoruz.
        ViewData["Title"] = "Manuel Kayıt Ekle";
        var kategoriler = await _kategoriService.GetAllAsync();
        ViewBag.KategoriListesi = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(kategoriler, "Id", "Ad");
        
        var vm = new KasaVM { Tarih = DateTime.Today };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(KasaVM vm)
    {
        if (!ModelState.IsValid)
        {
            var kategoriler = await _kategoriService.GetAllAsync();
            ViewBag.KategoriListesi = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(kategoriler, "Id", "Ad", vm.GiderKategoriId);
            ViewData["Title"] = "Manuel Kayıt Ekle";
            return View("Form", vm);
        }

        var result = await _kasaService.SaveAsync(vm);
        if (result.IsSuccess)
        {
            TempBasarili(result.Message);
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", result.Message);
        var kats = await _kategoriService.GetAllAsync();
        ViewBag.KategoriListesi = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(kats, "Id", "Ad", vm.GiderKategoriId);
        ViewData["Title"] = "Manuel Kayıt Ekle";
        return View("Form", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        var result = await _kasaService.SilAsync(id);
        return Json(new { success = result.IsSuccess, message = result.Message });
    }
}
