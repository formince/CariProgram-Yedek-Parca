using CariErinc.Services;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CariErinc.Controllers;

[Authorize]
public class AyarlarController : BaseController
{
    private readonly IAyarService _ayarService;

    public AyarlarController(IAyarService ayarService)
    {
        _ayarService = ayarService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (YetkiKontrol() is { } r) return r;
        ViewData["Title"] = "İşletme Ayarları";

        var vm = new IsletmeAyarlarVM
        {
            DukkanAdi = await _ayarService.GetAsync("DukkanAdi") ?? "Kırtasiye Dükkanı",
            IsletmeTipi = await _ayarService.GetAsync("IsletmeTipi") ?? "",
            Adres = await _ayarService.GetAsync("Adres") ?? "",
            Telefon = await _ayarService.GetAsync("Telefon") ?? "",
            VarsayilanKdv = int.TryParse(await _ayarService.GetAsync("VarsayilanKdv"), out var k) ? k : 20,
            KdvOranlariMetni = await _ayarService.GetAsync(KdvOranlariAyarlari.Anahtar) ?? "0,1,8,10,20"
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(IsletmeAyarlarVM vm)
    {
        if (YetkiKontrol() is { } r) return r;
        if (!KdvOranlariAyarlari.TryParseKayit(vm.KdvOranlariMetni, out var kdvListe, out var kdvHata))
            ModelState.AddModelError(nameof(vm.KdvOranlariMetni), kdvHata!);

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "İşletme Ayarları";
            return View(vm);
        }

        await _ayarService.SetAsync("DukkanAdi", vm.DukkanAdi.Trim());
        await _ayarService.SetAsync("IsletmeTipi", vm.IsletmeTipi?.Trim() ?? "");
        await _ayarService.SetAsync("Adres", vm.Adres?.Trim() ?? "");
        await _ayarService.SetAsync("Telefon", vm.Telefon?.Trim() ?? "");
        await _ayarService.SetAsync("VarsayilanKdv", vm.VarsayilanKdv.ToString());
        await _ayarService.SetAsync(KdvOranlariAyarlari.Anahtar, string.Join(',', kdvListe));

        TempBasarili("İşletme ayarları kaydedildi.");
        return RedirectToAction(nameof(Index));
    }
}
