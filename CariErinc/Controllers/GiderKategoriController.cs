using CariErinc.Models;
using CariErinc.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CariErinc.Controllers;

[Authorize]
public class GiderKategoriController : BaseController
{
    private readonly IGiderKategoriService _service;

    public GiderKategoriController(IGiderKategoriService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Gider Kategorileri";
        var kategoriler = await _service.GetAllAsync();
        return View(kategoriler);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id)
    {
        if (id.HasValue && id.Value > 0)
        {
            var kategori = await _service.GetByIdAsync(id.Value);
            if (kategori == null) return NotFound();
            ViewData["Title"] = "Kategoriyi Düzenle";
            return View(kategori);
        }

        ViewData["Title"] = "Yeni Kategori Ekle";
        return View(new GiderKategori { Tip = KasaHareketTipi.Gider });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(GiderKategori kategori)
    {
        var result = await _service.SaveAsync(kategori);
        if (result.IsSuccess)
        {
            TempBasarili(result.Message);
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", result.Message);
        ViewData["Title"] = kategori.Id == 0 ? "Yeni Kategori Ekle" : "Kategoriyi Düzenle";
        return View("Form", kategori);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        var result = await _service.SilAsync(id);
        return Json(new { success = result.IsSuccess, message = result.Message });
    }
}
