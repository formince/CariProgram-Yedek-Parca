using System.Globalization;
using Microsoft.AspNetCore.Mvc;

namespace CariErinc.Controllers;

[Controller]
public abstract class BaseController : Controller
{
    protected IActionResult? YetkiKontrol(string yetki = "is_admin", string deger = "true")
    {
        if (!User.HasClaim(yetki, deger))
            return RedirectToAction("Yetkisiz", "Auth");
        return null;
    }

    protected void TempBasarili(string mesaj) => TempData["Success"] = mesaj;
    protected void TempHata(string mesaj) => TempData["Error"] = mesaj;

    protected static DateTime? ParseDate(string? str) =>
        DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    protected static DateTime ParseDateOrDefault(string? str, DateTime @default) =>
        ParseDate(str) ?? @default;

    protected async Task PopulateKdvViewBagAsync(CariErinc.Services.Interfaces.ILookupService lookupService, int? currentKdv = null)
    {
        int kdv = currentKdv ?? await lookupService.GetVarsayilanKdvAsync();
        ViewBag.VarsayilanKdv = kdv;
        ViewBag.KdvListesi = await lookupService.GetKdvOranlariAsync(kdv);
    }
}
