using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CariErinc.Controllers;

[Authorize]
public class FaturaAnalizController : BaseController
{
    private readonly IFaturaAnalizService _faturaAnalizService;
    private readonly IUrunService _urunService;

    public FaturaAnalizController(IFaturaAnalizService faturaAnalizService, IUrunService urunService)
    {
        _faturaAnalizService = faturaAnalizService;
        _urunService = urunService;
    }

    [HttpPost]
    public async Task<IActionResult> AnalyzeXml(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return Json(new { success = false, message = "Lütfen bir dosya seçin." });

        if (!file.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return Json(new { success = false, message = "Sadece XML dosyaları desteklenmektedir." });

        try
        {
            var result = await _faturaAnalizService.AnalizEtAsync(file);
            return Json(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Analiz hatası: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> QuickCreateProduct([FromBody] UrunVM vm)
    {
        if (!ModelState.IsValid)
            return Json(new { success = false, message = "Lütfen tüm zorunlu alanları doldurun." });

        var result = await _urunService.SaveAsync(vm);
        if (result.IsSuccess)
        {
            // Yeni eklenen ürünü bulup geri dönelim (Eşleştirme için Id lazım)
            var urun = await _urunService.GetByBarkodAsync(vm.Barkod ?? "");
            if (urun == null) // Barkod yoksa ismiyle ara (riskli ama id için gerekli)
            {
                var candidates = await _urunService.GetAllAsync(vm.Ad);
                urun = candidates.OrderByDescending(u => u.Id).FirstOrDefault();
            }

            return Json(new { 
                success = true, 
                urunId = urun?.Id, 
                urunAdi = urun?.Ad,
                message = "Ürün başarıyla eklendi." 
            });
        }

        return Json(new { success = false, message = result.Message });
    }
}
