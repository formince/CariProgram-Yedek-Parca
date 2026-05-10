using CariErinc.Data;
using CariErinc.Helpers;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using CariErinc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Controllers;

[Authorize]
public class AlisController : BaseController
{
    private readonly IAlisService _alisService;
    private readonly AppDbContext _db;
    private readonly ILookupService _lookupService;

    public AlisController(IAlisService alisService, AppDbContext db, ILookupService lookupService)
    {
        _alisService = alisService;
        _db = db;
        _lookupService = lookupService;
    }

    // Removed local VarsayilanKdvViewBagAyarlaAsync, using base.PopulateKdvViewBagAsync

    private async Task SetSelectListelerAsync(AlisVM vm)
    {
        vm.TedarikciListesi = await _lookupService.GetTedarikcilerAsync(vm.TedarikciId > 0 ? vm.TedarikciId : null);
        var urunler = await _db.Urunler.OrderBy(u => u.Ad).ToListAsync();
        vm.UrunListesi = new SelectList(urunler.Select(u => new SelectListItem(u.Ad, u.Id.ToString())), "Value", "Text");
    }

    public async Task<IActionResult> Index(int? tedarikciId, string? baslangic, string? bitis, int page = 1)
    {
        ViewData["Title"] = "Alışlar";

        var parsedBaslangic = ParseDate(baslangic);
        var parsedBitis = ParseDate(bitis);

        var vm = await _alisService.GetPagedListAsync(page, tedarikciId, parsedBaslangic, parsedBitis);

        ViewBag.Tedarikciler = await _lookupService.GetTedarikcilerAsync(tedarikciId);

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id)
    {
        await PopulateKdvViewBagAsync(_lookupService);

        if (id.HasValue && id.Value > 0)
        {
            var alis = await _alisService.GetByIdAsync(id.Value);
            if (alis == null)
                return RedirectToAction(nameof(Index));

            if (alis.AlisOdemeleri.Count > 0)
            {
                TempHata("Bu alışa toptancı ödemesi kaydı girilmiş; düzenleme yapılamaz.");
                return RedirectToAction(nameof(Detail), new { id = id.Value });
            }

            var editVm = new AlisVM
            {
                AlisId = alis.Id,
                TedarikciId = alis.TedarikciId,
                Aciklama = alis.Aciklama,
                OdemeTipi = alis.OdemeTipi,
                VadeTarihi = alis.VadeTarihi,
                Satirlar = alis.AlisDetaylari.Select(d => new AlisDetaySatirVM
                {
                    UrunId = d.UrunId,
                    UrunAdiGoster = d.Urun?.Ad,
                    Miktar = d.Miktar,
                    BirimFiyat = d.BirimFiyat,
                    Iskonto1 = d.Iskonto1,
                    Iskonto2 = d.Iskonto2,
                    KdvOrani = d.KdvOrani,
                    Barkod = d.Urun?.Barkod
                }).ToList()
            };

            if (!editVm.Satirlar.Any())
                editVm.Satirlar.Add(new AlisDetaySatirVM());

            ViewData["Title"] = "Alış Düzenle";
            await SetSelectListelerAsync(editVm);
            return View(editVm);
        }

        ViewData["Title"] = "Yeni Alış";
        var vm = new AlisVM { Satirlar = new List<AlisDetaySatirVM> { new() } };
        await SetSelectListelerAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AlisVM vm)
    {
        await PopulateKdvViewBagAsync(_lookupService);
        await SetSelectListelerAsync(vm);

        if (!ModelState.IsValid)
        {
            await AlisSatirUrunAdlariDoldurAsync(vm);
            ViewData["Title"] = vm.AlisId > 0 ? "Alış Düzenle" : "Yeni Alış";
            return View("Form", vm);
        }

        var result = await _alisService.SaveAsync(vm);
        if (result.IsSuccess)
        {
            TempBasarili(result.Message);
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", result.Message);
        await AlisSatirUrunAdlariDoldurAsync(vm);
        ViewData["Title"] = vm.AlisId > 0 ? "Alış Düzenle" : "Yeni Alış";
        return View("Form", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        var result = await _alisService.SilAsync(id);
        return Json(new { success = result.IsSuccess, message = result.Message });
    }

    private async Task AlisSatirUrunAdlariDoldurAsync(AlisVM vm)
    {
        var ids = vm.Satirlar.Where(s => s.UrunId > 0).Select(s => s.UrunId).Distinct().ToList();
        if (ids.Count == 0) return;
        var data = await _db.Urunler.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.Ad, u.Barkod });
        foreach (var s in vm.Satirlar)
        {
            if (s.UrunId > 0 && data.TryGetValue(s.UrunId, out var urun))
            {
                s.UrunAdiGoster = urun.Ad;
                s.Barkod = urun.Barkod;
            }
        }
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        ViewData["Title"] = "Alış Detay";
        var alis = await _alisService.GetByIdAsync(id);
        if (alis == null)
            return RedirectToAction(nameof(Index));
        return View(alis);
    }

    public async Task<IActionResult> VadeliAciklar(int? tedarikciId)
    {
        ViewData["Title"] = "Vadeli Açık Alışlar";
        var liste = await _alisService.GetVadeliAcikAlislarAsync(tedarikciId);

        ViewBag.Tedarikciler = await _lookupService.GetTedarikcilerAsync(tedarikciId);

        ViewBag.TedarikciId = tedarikciId;
        return View(liste);
    }

    [HttpGet]
    public async Task<IActionResult> Odeme(int id)
    {
        ViewData["Title"] = "Toptancı Ödemesi";
        var alis = await _alisService.GetByIdAsync(id);
        if (alis == null || alis.OdemeTipi != AlisOdemeTipi.Vadeli || AlisBorcHesaplayici.IsTamOdendi(alis))
            return RedirectToAction(nameof(VadeliAciklar));

        return View(alis);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Odeme(int alisId, decimal tutar, string? aciklama)
    {
        var result = await _alisService.OdemeYapAsync(alisId, tutar, aciklama);
        if (result.IsSuccess)
        {
            TempBasarili(result.Message);
            return RedirectToAction(nameof(VadeliAciklar));
        }

        TempHata(result.Message);
        return RedirectToAction(nameof(Odeme), new { id = alisId });
    }
}
