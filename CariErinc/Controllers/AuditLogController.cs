using CariErinc.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CariErinc.Controllers;

[Authorize]
public class AuditLogController : Controller
{
    private readonly IAuditLogService _service;

    public AuditLogController(IAuditLogService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index(string? tablo, DateTime? baslangic, DateTime? bitis, int page = 1)
    {
        ViewData["Title"] = "İşlem Geçmişi";
        int pageSize = 30;
        var result = await _service.GetPagedLogsAsync(page, pageSize, tablo, null, baslangic, bitis);
        
        ViewBag.Tablo = tablo;
        ViewBag.Baslangic = baslangic?.ToString("yyyy-MM-dd");
        ViewBag.Bitis = bitis?.ToString("yyyy-MM-dd");

        return View(result);
    }
}
