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
public class VeresiyeController : BaseController
{
    private readonly IVeresiyeService _veresiyeService;
    private readonly AppDbContext _db;

    public VeresiyeController(IVeresiyeService veresiyeService, AppDbContext db)
    {
        _veresiyeService = veresiyeService;
        _db = db;
    }

    private void SetMusteriListesi(VeresiyeVM vm)
    {
        var musteriler = _db.Musteriler.OrderBy(m => m.Ad).ToList();
        var items = new List<SelectListItem> { new("-- Müşteri Seçin --", "") };
        items.AddRange(musteriler.Select(m => new SelectListItem($"{m.Ad} {m.Soyad}".Trim(), m.Id.ToString())));
        vm.MusteriListesi = new SelectList(items, "Value", "Text", vm.MusteriId > 0 ? vm.MusteriId.ToString() : "");
    }

    public async Task<IActionResult> Index(int? musteriId, string? durum, string? baslangic, string? bitis, int page = 1)
    {
        ViewData["Title"] = "Veresiye";
        OdenmeDurumu? durumEnum = durum switch
        {
            "Bekliyor" => OdenmeDurumu.Bekliyor,
            "KismiOdendi" => OdenmeDurumu.KismiOdendi,
            "Odendi" => OdenmeDurumu.Odendi,
            _ => null
        };

        var parsedBaslangic = ParseDate(baslangic);
        var parsedBitis = ParseDate(bitis);

        var vm = await _veresiyeService.GetPagedListAsync(page, musteriId, durumEnum, parsedBaslangic, parsedBitis);

        var musteriler = await _db.Musteriler.OrderBy(m => m.Ad).ToListAsync();
        ViewBag.MusteriListesi = new SelectList(
            new List<SelectListItem> { new("-- Tüm Müşteriler --", "") }
                .Concat(musteriler.Select(m => new SelectListItem($"{m.Ad} {m.Soyad}".Trim(), m.Id.ToString()))),
            "Value", "Text", musteriId?.ToString());

        ViewBag.MusteriId = musteriId;
        ViewBag.Durum = durum ?? "";

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id, int? musteriId)
    {
        if (id.HasValue && id.Value > 0)
        {
            ViewData["Title"] = "Veresiye Düzenle";
            var veresiye = await _veresiyeService.GetByIdAsync(id.Value);
            if (veresiye == null) return RedirectToAction(nameof(Index));

            var vm = new VeresiyeVM
            {
                Id = veresiye.Id,
                MusteriId = veresiye.MusteriId,
                Tutar = veresiye.Tutar,
                Aciklama = veresiye.Aciklama,
                Tip = veresiye.Tip
            };
            SetMusteriListesi(vm);
            return View(vm);
        }

        ViewData["Title"] = "Yeni Veresiye";
        var newVm = new VeresiyeVM { MusteriId = musteriId ?? 0 };
        SetMusteriListesi(newVm);
        return View(newVm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(VeresiyeVM vm)
    {
        if (!ModelState.IsValid)
        {
            SetMusteriListesi(vm);
            ViewData["Title"] = vm.Id == 0 ? "Yeni Veresiye" : "Veresiye Düzenle";
            return View("Form", vm);
        }

        var result = await _veresiyeService.SaveAsync(vm);
        if (result.IsSuccess)
        {
            TempBasarili(result.Message);
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", result.Message);
        SetMusteriListesi(vm);
        ViewData["Title"] = vm.Id == 0 ? "Yeni Veresiye" : "Veresiye Düzenle";
        return View("Form", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        var result = await _veresiyeService.SilAsync(id);
        return Json(new { success = result.IsSuccess, message = result.Message });
    }

    [HttpGet]
    public async Task<IActionResult> Odeme(int id)
    {
        ViewData["Title"] = "Ödeme Al";
        var veresiye = await _veresiyeService.GetByIdAsync(id);
        if (veresiye == null)
            return RedirectToAction(nameof(Index));

        ViewBag.KalanBorc = veresiye.Tutar - veresiye.Odemeler.Sum(o => o.OdemeTutari);
        return View(veresiye);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var veresiye = await _db.Veresiyeler
            .Include(v => v.Musteri)
            .Include(v => v.Satis)
                .ThenInclude(s => s != null ? s.SatisDetaylari : null)
            .Include(v => v.Odemeler)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (veresiye == null)
            return RedirectToAction(nameof(Index));

        ViewBag.KalanBorc = veresiye.Tutar - veresiye.Odemeler.Sum(o => o.OdemeTutari);
        return View(veresiye);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Odeme(int id, decimal tutar, string? aciklama, string? odemeTipi)
    {
        var kullaniciId = User.Identity?.Name;
        var tip = odemeTipi == "Hesap" ? CariErinc.Models.VeresiyeOdemeTipi.Hesap : CariErinc.Models.VeresiyeOdemeTipi.Nakit;
        var result = await _veresiyeService.OdemeAlAsync(id, tutar, aciklama, kullaniciId, tip);
        if (result.IsSuccess)
            TempBasarili(result.Message);
        else
            TempHata(result.Message);
        return RedirectToAction(nameof(Odeme), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KompleKapat(int musteriId, string veresiyeIds, decimal odenenTutar)
    {
        var cariId = await _db.Musteriler
            .AsNoTracking()
            .Where(m => m.Id == musteriId)
            .Select(m => m.CariId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(veresiyeIds) || odenenTutar <= 0)
        {
            TempHata("Geçersiz işlem.");
            return cariId.HasValue
                ? RedirectToAction("Detail", "Cari", new { id = cariId.Value })
                : RedirectToAction("Index", "Cari");
        }

        var idList = veresiyeIds.Split(',').Select(int.Parse).ToList();
        var kullaniciId = User.Identity?.Name;
        var result = await _veresiyeService.KompleKapatAsync(idList, odenenTutar, kullaniciId);
        if (result.IsSuccess)
            TempBasarili(result.Message);
        else
            TempHata(result.Message);

        return cariId.HasValue
            ? RedirectToAction("Detail", "Cari", new { id = cariId.Value })
            : RedirectToAction("Index", "Cari");
    }
}
