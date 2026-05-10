using System.Text.Json;
using CariErinc.Data;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Controllers;

[Authorize]
public class SatisController : BaseController
{
    private readonly ISatisService _satisService;
    private readonly ILookupService _lookupService;
    private readonly IUrunService _urunService;
    private readonly AppDbContext _db;

    public SatisController(ISatisService satisService, ILookupService lookupService, IUrunService urunService, AppDbContext db)
    {
        _satisService = satisService;
        _lookupService = lookupService;
        _urunService = urunService;
        _db = db;
    }

    public async Task<IActionResult> Index(int? musteriId, string? tip, DateTime? baslangic, DateTime? bitis, bool dahilIptaller = false,
        int page = 1)
    {
        ViewData["Title"] = "Satışlar";

        OdemeTipi? tipEnum = tip switch
        {
            "Pesin" => OdemeTipi.Pesin,
            "Veresiye" => OdemeTipi.Veresiye,
            _ => null
        };

        var vm = await _satisService.GetPagedListAsync(page, musteriId, tipEnum, baslangic, bitis, dahilIptaller);

        ViewBag.Musteriler = await _lookupService.GetMusteriSelectListAsync(musteriId, "-- Tüm Müşteriler --");

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id)
    {
        if (id.HasValue && id.Value > 0)
        {
            ViewData["Title"] = "Satışı Düzenle";
            var satis = await _satisService.GetForEditAsync(id.Value);

            if (satis == null || satis.IptalEdildi)
            {
                TempHata("Düzenlenebilir satış bulunamadı.");
                return RedirectToAction(nameof(Index));
            }

            var vm = new SatisVM
            {
                Id = satis.Id,
                MusteriId = satis.MusteriId,
                OdemeTipi = satis.OdemeTipi,
                Aciklama = satis.Aciklama,
                GenelIndirimOrani = satis.GenelIndirimOrani,
                GenelIndirimTutari = satis.GenelIndirimTutari,
                GenelIndirimModu = satis.GenelIndirimHesapModu,
                HedefToplam = satis.GenelIndirimHedefToplam,
                Satirlar = satis.SatisDetaylari.Select(d => new SatisDetaySatirVM
                {
                    UrunId = d.UrunId,
                    Miktar = d.Miktar,
                    BirimFiyat = d.BirimFiyat,
                    IndirimOrani = d.IndirimOrani,
                    KdvOrani = d.KdvOrani
                }).ToList()
            };

            if (satis.MusteriId.HasValue)
            {
                var musteri = await _db.Musteriler.Include(m => m.Cari).FirstOrDefaultAsync(m => m.Id == satis.MusteriId.Value);
                if (musteri != null)
                    ViewBag.SeciliMusteriAd = musteri.Cari?.Ad ?? $"{musteri.Ad} {musteri.Soyad}".Trim();
            }

            var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var taslakUrunler = satis.SatisDetaylari.Select(d => new
            {
                id = d.UrunId,
                ad = d.Urun?.Ad ?? "Urun #" + d.UrunId,
                barkod = d.Urun?.Barkod ?? "",
                birimFiyat = d.BirimFiyat,
                kdvOrani = d.KdvOrani,
                stokAdedi = d.Urun?.StokAdedi ?? 0
            }).ToList();
            ViewBag.Urunler = JsonSerializer.Serialize(taslakUrunler, jsonOpts);
            
            await PopulateKdvViewBagAsync(_lookupService, vm.Satirlar.FirstOrDefault()?.KdvOrani);
            await PopulateFormViewBags(vm.MusteriId);
            return View(vm);
        }

        ViewData["Title"] = "Yeni Satış";
        await PopulateKdvViewBagAsync(_lookupService);
        var newVm = new SatisVM { Satirlar = new List<SatisDetaySatirVM> { new() } };

        await PopulateFormViewBags(newVm.MusteriId);
        return View(newVm);
    }

    private async Task PopulateFormViewBags(int? musteriId)
    {
        var tumUrunler = await _urunService.GetAllAsync();
        var urunFiyatlari = tumUrunler.ToDictionary(u => u.Id, u => new { birimFiyat = u.BirimFiyat, kdvOrani = u.KdvOrani });
        ViewData["UrunFiyatlari"] = JsonSerializer.Serialize(urunFiyatlari, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SatisVM vm)
    {
        await PopulateKdvViewBagAsync(_lookupService);
        if (!ModelState.IsValid)
        {
            if (vm.Id > 0) return RedirectToAction(nameof(Form), new { id = vm.Id });
            
            await PopulateFormViewBags(vm.MusteriId);
            return View("Form", vm);
        }

        var result = await _satisService.SaveAsync(vm);
        if (result.IsSuccess)
        {
            TempBasarili(result.Message);
            return RedirectToAction(nameof(Index));
        }

        if (vm.Id > 0)
        {
            TempHata(result.Message);
            return RedirectToAction(nameof(Form), new { id = vm.Id });
        }

        await PopulateFormViewBags(vm.MusteriId);
        ModelState.AddModelError("", result.Message);
        return View("Form", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        ViewData["Title"] = "Satış Detay";
        var satis = await _satisService.GetByIdAsync(id);
        if (satis == null)
            return RedirectToAction(nameof(Index));
        return View(satis);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id)
    {
        var result = await _satisService.SilAsync(id);
        return Json(new { success = result.IsSuccess, message = result.Message });
    }

    private async Task SetHizliSatisViewBag(SatisVM vm)
    {
        if (vm.MusteriId.HasValue && vm.MusteriId.Value > 0)
        {
            var musteri = await _db.Musteriler.Include(m => m.Cari).FirstOrDefaultAsync(m => m.Id == vm.MusteriId.Value);
            if (musteri != null)
                ViewBag.SeciliMusteriAd = musteri.Cari?.Ad ?? $"{musteri.Ad} {musteri.Soyad}".Trim();
        }

        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        if (vm.Satirlar is { Count: > 0 })
        {
            var ids = vm.Satirlar.Select(s => s.UrunId).Where(id => id > 0).Distinct().ToList();
            var taslakUrunler = await _urunService.GetByIdsAsync(ids);
            ViewBag.Urunler = JsonSerializer.Serialize(taslakUrunler.Select(u => new { u.Id, u.Ad, u.Barkod, u.BirimFiyat, u.KdvOrani, u.StokAdedi }), jsonOpts);
        }
        else
        {
            ViewBag.Urunler = "[]";
        }

        ViewBag.TaslaklarCount = (await _satisService.GetTaslaklarAsync()).Count;
    }

    [HttpGet]
    public async Task<IActionResult> HizliSatis(SatisVM? vm = null)
    {
        ViewData["Title"] = "Hızlı Satış";
        if (vm == null || vm.Satirlar == null)
        {
            vm = new SatisVM
            {
                Satirlar = new List<SatisDetaySatirVM>()
            };
        }

        await SetHizliSatisViewBag(vm);
 
        await PopulateKdvViewBagAsync(_lookupService);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> MusteriAra(string? q, int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 50);
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 1)
            return Json(Array.Empty<object>());

        var pattern = $"%{q.Trim()}%";
        var musteriler = await _db.Musteriler
            .AsNoTracking()
            .Include(m => m.Cari)
            .Where(m => m.Cari != null
                && ((m.Cari.Rol & CariRol.Musteri) == CariRol.Musteri)
                && (EF.Functions.ILike(m.Cari.Ad, pattern)
                    || (m.Cari.Telefon != null && EF.Functions.ILike(m.Cari.Telefon, pattern))))
            .OrderBy(m => m.Cari!.Ad)
            .Take(limit)
            .ToListAsync();

        var liste = musteriler.Select(m => new { id = m.Id, ad = m.Cari!.Ad }).ToList();

        return Json(liste);
    }

    [HttpGet]
    public async Task<IActionResult> UrunAraMetin(string? q, int limit = 30)
    {
        limit = Math.Clamp(limit, 1, 100);
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Json(Array.Empty<object>());

        var liste = await _urunService.SearchAsync(q, limit);
        var sonMap = await _urunService.GetSonAlisBilgileriAsync(liste.Select(x => x.Id).ToList());

        var sonuclar = liste.Select(u => new
        {
            id = u.Id,
            ad = u.Ad,
            barkod = u.Barkod,
            birimFiyat = u.BirimFiyat,
            alisFiyati = u.AlisFiyati,
            stokAdedi = u.StokAdedi,
            kdvOrani = u.KdvOrani,
            sonAlis = sonMap.TryGetValue(u.Id, out var sa)
                ? new { birimFiyat = sa.ListeFiyati, iskonto1 = sa.Iskonto1, iskonto2 = sa.Iskonto2 }
                : (object?)null
        }).ToList();

        return Json(sonuclar);
    }

    [HttpGet]
    public async Task<IActionResult> BarkodAra(string barkod)
    {
        if (string.IsNullOrWhiteSpace(barkod))
            return Json(null);

        var urun = await _urunService.GetByBarkodAsync(barkod);
        if (urun == null)
            return Json(null);

        var sonMap = await _urunService.GetSonAlisBilgileriAsync(new List<int> { urun.Id });
        object? sonAlis = null;
        if (sonMap.TryGetValue(urun.Id, out var sa))
            sonAlis = new { birimFiyat = sa.ListeFiyati, iskonto1 = sa.Iskonto1, iskonto2 = sa.Iskonto2 };

        return Json(new
        {
            id = urun.Id,
            ad = urun.Ad,
            barkod = urun.Barkod,
            birimFiyat = urun.BirimFiyat,
            alisFiyati = urun.AlisFiyati,
            stokAdedi = urun.StokAdedi,
            kdvOrani = urun.KdvOrani,
            sonAlis
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HizliSatisTamamla(
        List<SatisDetaySatirVM> satirlar,
        OdemeTipi odemeTipi,
        int? musteriId,
        GenelIndirimModu genelIndirimModu,
        decimal genelIndirimOrani,
        decimal genelIndirimTutari,
        decimal hedefToplam,
        int? taslakId,
        string? aciklama)
    {
        var vm = new SatisVM
        {
            Satirlar = satirlar?.Where(s => s.UrunId > 0 && s.Miktar > 0 && s.BirimFiyat > 0).ToList() ?? new List<SatisDetaySatirVM>(),
            OdemeTipi = odemeTipi,
            MusteriId = musteriId,
            GenelIndirimModu = genelIndirimModu,
            GenelIndirimOrani = genelIndirimOrani,
            GenelIndirimTutari = genelIndirimTutari,
            HedefToplam = hedefToplam,
            TaslakId = taslakId,
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim()
        };

        if (!vm.Satirlar.Any())
        {
            TempHata("Sepette ürün bulunmuyor.");
            return RedirectToAction(nameof(HizliSatis));
        }

        var result = await _satisService.SaveAsync(vm);
        if (result.IsSuccess)
            TempBasarili(result.Message);
        else
            TempHata(result.Message);
        return RedirectToAction("HizliSatis");
    }

    [HttpGet]
    public async Task<IActionResult> Fis(int id)
    {
        var satis = await _satisService.GetByIdAsync(id);
        if (satis == null)
            return RedirectToAction(nameof(Index));
        return View(satis);
    }


    [HttpGet]
    public async Task<IActionResult> Iade(int id)
    {
        ViewData["Title"] = "Kısmi iade al";
        var satis = await _satisService.GetByIdAsync(id);
        if (satis == null || satis.IptalEdildi)
            return RedirectToAction(nameof(Index));

        var vm = new SatisIadeVM
        {
            SatisId = id,
            Satirlar = satis.SatisDetaylari.Select(d => new SatisIadeDetaySatirVM
            {
                SatisDetayId = d.Id,
                UrunAd = d.Urun?.Ad ?? "Bilinmeyen Ürün",
                SatilanMiktar = d.Miktar,
                OncekiIadeMiktar = d.SatisIadeDetaylari?.Sum(x => x.IadeMiktar) ?? 0,
                BirimFiyat = d.BirimFiyat
            }).ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Iade(SatisIadeVM vm)
    {
        var result = await _satisService.KismiIadeAsync(vm);
        if (result.IsSuccess)
        {
            TempBasarili(result.Message);
            return RedirectToAction(nameof(Detail), new { id = vm.SatisId });
        }
        TempHata(result.Message);

        var satis = await _satisService.GetByIdAsync(vm.SatisId);
        if (satis != null)
        {
            foreach (var s in vm.Satirlar)
            {
                var d = satis.SatisDetaylari.FirstOrDefault(x => x.Id == s.SatisDetayId);
                if (d != null)
                {
                    s.UrunAd = d.Urun?.Ad ?? "";
                    s.SatilanMiktar = d.Miktar;
                    s.OncekiIadeMiktar = d.SatisIadeDetaylari?.Sum(x => x.IadeMiktar) ?? 0;
                }
            }
        }
        return View(vm);
    }

    [HttpPost]
    public async Task<JsonResult> TaslakKaydet([FromBody] SatisVM vm)
    {
        if (vm == null) return Json(new { basarili = false, mesaj = "Geçersiz veri" });
        var result = await _satisService.TaslakKaydetAsync(vm);
        return Json(new { basarili = result.IsSuccess, mesaj = result.Message, taslakId = result.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Taslaklar()
    {
        ViewData["Title"] = "Bekleyen Sepetler";
        var taslaklar = await _satisService.GetTaslaklarAsync();
        return View(taslaklar);
    }

    [HttpGet]
    public async Task<IActionResult> TaslakYukle(int id)
    {
        var vm = await _satisService.TaslagiYukleAsync(id);
        if (vm == null)
        {
            TempHata("Taslak sepet bulunamadı.");
            return RedirectToAction(nameof(HizliSatis));
        }
        ViewData["Title"] = "Hızlı Satış";
        await SetHizliSatisViewBag(vm);
        return View("HizliSatis", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TaslakSil(int id)
    {
        var result = await _satisService.TaslakSilAsync(id);
        return Json(new { success = result.IsSuccess, message = result.Message });
    }
}
