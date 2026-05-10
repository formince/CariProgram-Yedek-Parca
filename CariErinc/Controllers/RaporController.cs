using ClosedXML.Excel;
using CariErinc.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CariErinc.Controllers;

[Authorize]
public class RaporController : BaseController
{
    private readonly IRaporService _raporService;

    public RaporController(IRaporService raporService)
    {
        _raporService = raporService;
    }

    [HttpGet]
    [Route("rapor/gunluk")]
    public async Task<IActionResult> Gunluk(DateTime? tarih)
    {
        ViewData["Title"] = "Günlük Satış Raporu";
        var vm = await _raporService.GetGunlukSatisAsync(tarih);
        ViewBag.SecilenTarih = vm.Tarih.ToString("yyyy-MM-dd");
        return View("GunlukSatis", vm);
    }

    [HttpGet]
    [Route("rapor/gunluk/excel")]
    public async Task<IActionResult> GunlukExcel(DateTime? tarih)
    {
        var vm = await _raporService.GetGunlukSatisAsync(tarih);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Günlük Satış");

        ws.Cell(1, 1).Value = $"Günlük Satış Raporu — {vm.Tarih:dd.MM.yyyy}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 5).Merge();

        ws.Cell(3, 1).Value = "Toplam Gelir"; ws.Cell(3, 2).Value = (double)vm.ToplamGelir;
        ws.Cell(4, 1).Value = "Toplam Gider"; ws.Cell(4, 2).Value = (double)vm.ToplamGider;
        ws.Cell(5, 1).Value = "Net Kâr";      ws.Cell(5, 2).Value = (double)vm.NetKar;
        ws.Cell(5, 1).Style.Font.Bold = true; ws.Cell(5, 2).Style.Font.Bold = true;

        int row = 7;
        var baslik = new[] { "Saat", "Kategori", "Açıklama", "Tutar (₺)" };
        for (int i = 0; i < baslik.Length; i++)
        {
            ws.Cell(row, i + 1).Value = baslik[i];
            ws.Cell(row, i + 1).Style.Font.Bold = true;
            ws.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        }
        row++;
        foreach (var h in vm.KasaHareketler)
        {
            ws.Cell(row, 1).Value = h.Tarih.ToLocalTime().ToString("HH:mm");
            ws.Cell(row, 2).Value = h.Kategori;
            ws.Cell(row, 3).Value = h.Aciklama ?? "";
            ws.Cell(row, 4).Value = (double)h.Tutar;
            row++;
        }

        ws.Columns().AdjustToContents();
        return ExcelFile(wb, $"Gunluk-Satis-{vm.Tarih:yyyyMMdd}.xlsx");
    }

    [HttpGet]
    [Route("rapor/aylik")]
    public async Task<IActionResult> Aylik(int? yil, int? ay)
    {
        ViewData["Title"] = "Aylık Gelir/Gider";
        var vm = await _raporService.GetAylikRaporAsync(yil, ay);
        ViewBag.SecilenYil = vm.Yil;
        ViewBag.SecilenAy = vm.Ay;
        return View("AylikGelirGider", vm);
    }

    [HttpGet]
    [Route("rapor/aylik/excel")]
    public async Task<IActionResult> AylikExcel(int? yil, int? ay)
    {
        var vm = await _raporService.GetAylikRaporAsync(yil, ay);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Aylık Özet");

        ws.Cell(1, 1).Value = $"Aylık Rapor — {vm.Yil}/{vm.Ay:D2}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 4).Merge();

        int row = 3;
        ws.Cell(row, 1).Value = "Aylık Gelir";    ws.Cell(row, 2).Value = (double)vm.AylikGelir;
        ws.Cell(row + 1, 1).Value = "Aylık Gider"; ws.Cell(row + 1, 2).Value = (double)vm.AylikGider;
        ws.Cell(row + 2, 1).Value = "Net Bakiye";  ws.Cell(row + 2, 2).Value = (double)vm.NetBakiye;
        ws.Cell(row + 2, 1).Style.Font.Bold = true;
        ws.Cell(row + 2, 2).Style.Font.Bold = true;

        row += 5;
        ws.Cell(row, 1).Value = "Gün";
        ws.Cell(row, 2).Value = "Gelir (₺)";
        ws.Cell(row, 3).Value = "Gider (₺)";
        ws.Row(row).Style.Font.Bold = true;
        ws.Row(row).Style.Fill.BackgroundColor = XLColor.LightGray;
        row++;
        foreach (var g in vm.GunlukOzetler)
        {
            ws.Cell(row, 1).Value = g.Tarih.ToString("dd.MM.yyyy");
            ws.Cell(row, 2).Value = (double)g.Gelir;
            ws.Cell(row, 3).Value = (double)g.Gider;
            row++;
        }

        ws.Columns().AdjustToContents();
        return ExcelFile(wb, $"Aylik-Rapor-{vm.Yil}-{vm.Ay:D2}.xlsx");
    }

    [HttpGet]
    [Route("rapor/stok-uyari")]
    public async Task<IActionResult> StokUyari()
    {
        ViewData["Title"] = "Stok Uyarı Raporu";
        var vm = await _raporService.GetStokUyariAsync();
        return View("StokUyari", vm);
    }

    [HttpGet]
    [Route("rapor/stok-uyari/excel")]
    public async Task<IActionResult> StokUyariExcel()
    {
        var vm = await _raporService.GetStokUyariAsync();
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Stok Uyarı");

        ws.Cell(1, 1).Value = "Stok Uyarı Raporu";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        var baslik = new[] { "Barkod", "Ürün Adı", "Kategori", "Mevcut Stok", "Min. Stok", "Eksik" };
        for (int i = 0; i < baslik.Length; i++)
        {
            ws.Cell(3, i + 1).Value = baslik[i];
            ws.Cell(3, i + 1).Style.Font.Bold = true;
            ws.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightSalmon;
        }

        int row = 4;
        foreach (var u in vm.KritikUrunler)
        {
            ws.Cell(row, 1).Value = u.Barkod;
            ws.Cell(row, 2).Value = u.Ad;
            ws.Cell(row, 3).Value = u.Kategori;
            ws.Cell(row, 4).Value = u.StokAdedi;
            ws.Cell(row, 5).Value = u.MinStokUyari;
            ws.Cell(row, 6).Value = u.MinStokUyari - u.StokAdedi;
            if (u.StokAdedi == 0)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.LightPink;
            row++;
        }

        ws.Columns().AdjustToContents();
        return ExcelFile(wb, $"Stok-Uyari-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpGet]
    [Route("rapor/veresiye")]
    public async Task<IActionResult> Veresiye()
    {
        ViewData["Title"] = "Veresiye Borç Listesi";
        var vm = await _raporService.GetVeresiyeRaporAsync();
        return View("VeresiyeListesi", vm);
    }

    [HttpGet]
    [Route("rapor/veresiye/excel")]
    public async Task<IActionResult> VeresiyeExcel()
    {
        var vm = await _raporService.GetVeresiyeRaporAsync();
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Veresiye Listesi");

        ws.Cell(1, 1).Value = "Veresiye Borç Listesi";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        var baslik = new[] { "Müşteri", "Tarih", "Toplam Borç (₺)", "Ödenen (₺)", "Kalan (₺)", "Durum" };
        for (int i = 0; i < baslik.Length; i++)
        {
            ws.Cell(3, i + 1).Value = baslik[i];
            ws.Cell(3, i + 1).Style.Font.Bold = true;
            ws.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        int row = 4;
        foreach (var v in vm.AcikVeresiyeler)
        {
            var odenenTutar = v.Odemeler?.Sum(o => o.OdemeTutari) ?? 0;
            ws.Cell(row, 1).Value = v.Musteri?.Ad + " " + v.Musteri?.Soyad;
            ws.Cell(row, 2).Value = v.Tarih.ToLocalTime().ToString("dd.MM.yyyy");
            ws.Cell(row, 3).Value = (double)v.Tutar;
            ws.Cell(row, 4).Value = (double)odenenTutar;
            ws.Cell(row, 5).Value = (double)(v.Tutar - odenenTutar);
            ws.Cell(row, 6).Value = v.OdenmeDurumu.ToString();
            if (v.Tutar - odenenTutar > 0)
                ws.Cell(row, 5).Style.Font.FontColor = XLColor.Red;
            row++;
        }

        ws.Columns().AdjustToContents();
        return ExcelFile(wb, $"Veresiye-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpGet]
    [Route("rapor/kar-zarar")]
    public async Task<IActionResult> KarZarar(DateTime? baslangic, DateTime? bitis)
    {
        ViewData["Title"] = "Kâr/Zarar Raporu";

        var start = baslangic.HasValue
            ? DateTime.SpecifyKind(baslangic.Value, DateTimeKind.Utc)
            : new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var end = bitis.HasValue
            ? DateTime.SpecifyKind(bitis.Value, DateTimeKind.Utc)
            : DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        var vm = await _raporService.GetKarZararAsync(start, end);
        return View(vm);
    }

    [HttpGet]
    [Route("rapor/kar-zarar/excel")]
    public async Task<IActionResult> KarZararExcel(DateTime? baslangic, DateTime? bitis)
    {
        var start = baslangic.HasValue
            ? DateTime.SpecifyKind(baslangic.Value, DateTimeKind.Utc)
            : new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var end = bitis.HasValue
            ? DateTime.SpecifyKind(bitis.Value, DateTimeKind.Utc)
            : DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        var vm = await _raporService.GetKarZararAsync(start, end);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Kâr-Zarar");

        ws.Cell(1, 1).Value = $"Kâr/Zarar Raporu — {start:dd.MM.yyyy} / {end:dd.MM.yyyy}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 3).Merge();

        int row = 3;
        void AddRow(string label, decimal value, bool bold = false)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = (double)value;
            if (bold) { ws.Cell(row, 1).Style.Font.Bold = true; ws.Cell(row, 2).Style.Font.Bold = true; }
            row++;
        }

        AddRow("Net Satış Tutarı", vm.NetSatisTutari);
        AddRow("Satış Maliyeti (COGS)", vm.SatisMaliyeti);
        AddRow("Brüt Kâr", vm.BrutKar, true);
        AddRow("Toplam Gider", vm.ToplamGider);
        AddRow("Net Kâr / Zarar", vm.NetKar, true);

        ws.Columns().AdjustToContents();
        return ExcelFile(wb, $"Kar-Zarar-{start:yyyyMMdd}-{end:yyyyMMdd}.xlsx");
    }

    private static FileContentResult ExcelFile(XLWorkbook wb, string fileName)
    {
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return new FileContentResult(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        {
            FileDownloadName = fileName
        };
    }
}
