using CariErinc.Data;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CariErinc.Controllers;

[Authorize]
public class UrunController : BaseController
{
    private readonly IUrunService _urunService;
    private readonly IUrunFiyatService _fiyatService;
    private readonly IAyarService _ayarService;
    private readonly ILookupService _lookupService;

    public UrunController(IUrunService urunService, IUrunFiyatService fiyatService, IAyarService ayarService, ILookupService lookupService)
    {
        _urunService = urunService;
        _fiyatService = fiyatService;
        _ayarService = ayarService;
        _lookupService = lookupService;
    }


    public async Task<IActionResult> Index(string? arama, string? kategori, int? tedarikciId, string? stokDurumu, int page = 1)
    {
        ViewData["Title"] = "Ürünler";
        var vm = await _urunService.GetPagedListAsync(page, arama, kategori, tedarikciId, stokDurumu);

        // Filtreler için listeler
        ViewBag.Kategoriler = await _lookupService.GetUrunKategorileriAsync(kategori);
        ViewBag.TedarikciListesi = await _lookupService.GetTedarikcilerAsync(tedarikciId);

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id)
    {
        if (id.HasValue && id.Value > 0)
        {
            ViewData["Title"] = "Ürün Düzenle";
            var urun = await _urunService.GetByIdAsync(id.Value);
            if (urun == null) return RedirectToAction(nameof(Index));

            var vm = new UrunVM
            {
                Id = urun.Id,
                Ad = urun.Ad,
                Barkod = urun.Barkod,
                Kategori = urun.Kategori,
                BirimFiyat = urun.BirimFiyat,
                KdvOrani = urun.KdvOrani,
                AlisFiyati = urun.AlisFiyati,
                StokAdedi = urun.StokAdedi,
                MinStokUyari = urun.MinStokUyari,
                TedarikciId = urun.TedarikciId
            };
            
            ViewBag.TedarikciListesi = await _lookupService.GetTedarikcilerAsync(vm.TedarikciId);
            await PopulateKdvViewBagAsync(_lookupService, vm.KdvOrani);
            return View(vm);
        }

        ViewData["Title"] = "Yeni Ürün";
        var newVm = new UrunVM { MinStokUyari = 5 };
        ViewBag.TedarikciListesi = await _lookupService.GetTedarikcilerAsync();
        await PopulateKdvViewBagAsync(_lookupService);
        return View(newVm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(UrunVM vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.TedarikciListesi = await _lookupService.GetTedarikcilerAsync(vm.TedarikciId);
            await PopulateKdvViewBagAsync(_lookupService, vm.KdvOrani);
            ViewData["Title"] = vm.Id == 0 ? "Yeni Ürün" : "Ürün Düzenle";
            return View("Form", vm);
        }

        if (vm.Id > 0) // Güncelleme ise fiyat kontrolü yap
        {
            var eskiUrun = await _urunService.GetByIdAsync(vm.Id);
            if (eskiUrun != null && Math.Abs(eskiUrun.AlisFiyati - vm.AlisFiyati) > 0.01m)
            {
                var priceResult = await _fiyatService.UpdateAlisFiyatiAsync(
                    urunId: vm.Id,
                    yeniFiyat: vm.AlisFiyati,
                    neden: "ManuelDuzenleme",
                    kullanici: User.Identity?.Name ?? "Bilinmiyor"
                );

                if (!priceResult.IsSuccess)
                {
                    ViewBag.TedarikciListesi = await _lookupService.GetTedarikcilerAsync(vm.TedarikciId);
                    await PopulateKdvViewBagAsync(_lookupService, vm.KdvOrani);
                    ModelState.AddModelError("", priceResult.Message);
                    ViewData["Title"] = vm.Id == 0 ? "Yeni Ürün" : "Ürün Düzenle";
                    return View("Form", vm);
                }
            }
        }

        var result = await _urunService.SaveAsync(vm);
        if (result.IsSuccess)
        {
            TempBasarili(result.Message);
            return RedirectToAction(nameof(Index));
        }
        
        ViewBag.TedarikciListesi = await _lookupService.GetTedarikcilerAsync(vm.TedarikciId);
        await PopulateKdvViewBagAsync(_lookupService, vm.KdvOrani);
        ModelState.AddModelError("", result.Message);
        ViewData["Title"] = vm.Id == 0 ? "Yeni Ürün" : "Ürün Düzenle";
        return View("Form", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        ViewData["Title"] = "Ürün Detay";
        var urun = await _urunService.GetByIdAsync(id);
        if (urun == null)
            return RedirectToAction(nameof(Index));

        ViewBag.Hareketler = await _urunService.GetSonStokHareketleriAsync(id, 20);
        return View(urun);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        var result = await _urunService.SilAsync(id);
        return Json(new { success = result.IsSuccess, message = result.Message });
    }

    [HttpGet]
    [Route("api/urun/{id}/fiyat-gecmisi")]
    public async Task<IActionResult> GetFiyatGecmisi(int id)
    {
        try
        {
            var gecmis = await _fiyatService.GetFiyatGecmisiAsync(id);
            return Ok(gecmis);
        }
        catch
        {
            return StatusCode(500, new { message = "Fiyat geçmişi yüklenirken hata oluştu" });
        }
    }
}
