using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CariErinc.Controllers;

[Authorize]
public class CariController : BaseController
{
    private readonly ICariService _cariService;
    private readonly IVeresiyeService _veresiyeService;

    public CariController(ICariService cariService, IVeresiyeService veresiyeService)
    {
        _cariService = cariService;
        _veresiyeService = veresiyeService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? arama)
    {
        ViewData["Title"] = "Cariler";
        var vm = await _cariService.GetIndexVMAsync(arama);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        if (id <= 0) return RedirectToAction(nameof(Index));
        var vm = await _cariService.GetDetayVMAsync(id);
        if (vm == null) return RedirectToAction(nameof(Index));
        ViewData["Title"] = $"Cari: {vm.Ad}";
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Ekstre(int id, int page = 1, string? baslangic = null, string? bitis = null)
    {
        if (id <= 0) return RedirectToAction(nameof(Index));
        var parsedBaslangic = ParseDate(baslangic);
        var parsedBitis = ParseDate(bitis);
        var vm = await _cariService.GetEkstreVMAsync(id, page, parsedBaslangic, parsedBitis);
        if (vm == null)
            return RedirectToAction(nameof(Index));
        ViewData["Title"] = $"Hareket raporu — {vm.Ad}";
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id, string? rol = null)
    {
        if (id.HasValue && id.Value > 0)
        {
            var vm = await _cariService.GetFormVMAsync(id.Value);
            if (vm == null)
            {
                TempHata("Cari bulunamadı.");
                return RedirectToAction(nameof(Index));
            }

            ViewData["Title"] = "Cari Düzenle";
            return View(vm);
        }

        ViewData["Title"] = "Yeni Cari";
        var vmYeni = new CariFormVM
        {
            AktifMi = true,
            MusteriRol = string.Equals(rol, "musteri", StringComparison.OrdinalIgnoreCase),
            TedarikciRol = string.Equals(rol, "tedarikci", StringComparison.OrdinalIgnoreCase)
        };
        return View(vmYeni);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CariFormVM vm)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = vm.Id > 0 ? "Cari Düzenle" : "Yeni Cari";
            return View("Form", vm);
        }

        var result = await _cariService.SaveAsync(vm);
        if (result.IsSuccess)
        {
            TempBasarili(result.Message);
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", result.Message);
        ViewData["Title"] = vm.Id > 0 ? "Cari Düzenle" : "Yeni Cari";
        return View("Form", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        if (id <= 0)
            return Json(new { success = false, message = "Geçersiz cari." });

        var result = await _cariService.SilAsync(id);
        return Json(new { success = result.IsSuccess, message = result.Message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TopluAlacakTahsilat(int cariId, string veresiyeIds, decimal tutar, string? aciklama)
    {
        if (!ModelState.IsValid)
        {
            TempHata("Gönderilen veri geçersiz.");
            return RedirectToAction(nameof(Detail), new { id = cariId });
        }

        if (cariId <= 0)
        {
            TempHata("Geçersiz cari.");
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(veresiyeIds))
        {
            TempHata("Tahsilat için en az bir kayıt seçmelisiniz.");
            return RedirectToAction(nameof(Detail), new { id = cariId });
        }

        var idList = veresiyeIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var parsed) ? parsed : 0)
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (!idList.Any())
        {
            TempHata("Tahsilat için geçerli kayıt seçilmedi.");
            return RedirectToAction(nameof(Detail), new { id = cariId });
        }

        var result = await _veresiyeService.KompleKapatAsync(idList, tutar, User.Identity?.Name);
        if (result.IsSuccess)
            TempBasarili(string.IsNullOrWhiteSpace(aciklama) ? result.Message : $"{result.Message} ({aciklama.Trim()})");
        else
            TempHata(result.Message);

        return RedirectToAction(nameof(Detail), new { id = cariId });
    }
}
