using CariErinc.Data;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Controllers;

[Authorize]
public class StokController : BaseController
{
    private readonly IStokService _stokService;
    private readonly AppDbContext _db;

    public StokController(IStokService stokService, AppDbContext db)
    {
        _stokService = stokService;
        _db = db;
    }

    private void SetUrunListesi(StokHareketVM vm)
    {
        var urunler = _db.Urunler.OrderBy(u => u.Ad).ToList();
        var items = new List<SelectListItem> { new("-- Ürün Seçin --", "") };
        items.AddRange(urunler.Select(u => new SelectListItem(u.Ad, u.Id.ToString())));
        vm.UrunListesi = new SelectList(items, "Value", "Text", vm.UrunId > 0 ? vm.UrunId.ToString() : "");
    }

    public async Task<IActionResult> Index(int? urunId, string? baslangic, string? bitis, int page = 1)
    {
        ViewData["Title"] = "Stok Hareketleri";

        var parsedBaslangic = ParseDate(baslangic);
        var parsedBitis = ParseDate(bitis);

        var vm = await _stokService.GetPagedListAsync(page, urunId, parsedBaslangic, parsedBitis);

        var urunler = await _db.Urunler.OrderBy(u => u.Ad).ToListAsync();
        ViewBag.UrunListesi = new SelectList(
            new List<SelectListItem> { new("-- Tüm Ürünler --", "") }
                .Concat(urunler.Select(u => new SelectListItem(u.Ad, u.Id.ToString()))),
            "Value", "Text", urunId?.ToString());

        ViewBag.UrunId = urunId;
        ViewBag.Baslangic = parsedBaslangic?.ToString("yyyy-MM-dd");
        ViewBag.Bitis = parsedBitis?.ToString("yyyy-MM-dd");

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id, int? urunId)
    {
        if (id.HasValue && id.Value > 0)
        {
            ViewData["Title"] = "Stok Hareketi Düzenle";
            var vm = await _stokService.GetHareketVmAsync(id.Value);
            if (vm == null)
                return RedirectToAction(nameof(Index));

            SetUrunListesi(vm);
            return View(vm);
        }

        ViewData["Title"] = "Stok Hareketi Gir";
        var newVm = new StokHareketVM { UrunId = urunId ?? 0 };
        SetUrunListesi(newVm);
        return View(newVm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(StokHareketVM vm)
    {
        SetUrunListesi(vm);
        
        if (vm.Id > 0 && !vm.Tarih.HasValue)
            ModelState.AddModelError(nameof(vm.Tarih), "Tarih gereklidir.");

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = vm.Id > 0 ? "Stok Hareketi Düzenle" : "Stok Hareketi Gir";
            return View("Form", vm);
        }

        var result = await _stokService.SaveAsync(vm);
        if (result.IsSuccess)
        {
            TempBasarili(result.Message);
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", result.Message);
        ViewData["Title"] = vm.Id > 0 ? "Stok Hareketi Düzenle" : "Stok Hareketi Gir";
        return View("Form", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        var result = await _stokService.SilAsync(id);
        return Json(new { success = result.IsSuccess, message = result.Message });
    }
}
