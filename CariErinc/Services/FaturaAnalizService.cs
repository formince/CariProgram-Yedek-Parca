using System.Xml.Linq;
using CariErinc.Data;
using CariErinc.Models;
using CariErinc.Services.Interfaces;
using CariErinc.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace CariErinc.Services;

public class FaturaAnalizService : IFaturaAnalizService
{
    private readonly AppDbContext _db;

    public FaturaAnalizService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<FaturaAnalizSonucVM> AnalizEtAsync(IFormFile dosya)
    {
        if (dosya.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return await AnalizEtXmlAsync(dosya);
        }

        // TODO: Fas 2'de Image/PDF eklenecek
        throw new NotSupportedException("Şu an sadece XML dosyaları desteklenmektedir.");
    }

    private async Task<FaturaAnalizSonucVM> AnalizEtXmlAsync(IFormFile dosya)
    {
        using var stream = dosya.OpenReadStream();
        XDocument xmlDoc = XDocument.Load(stream);

        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var sonuc = new FaturaAnalizSonucVM();

        // 1. Header Bilgileri
        sonuc.FaturaNo = xmlDoc.Descendants(cbc + "ID").FirstOrDefault()?.Value;
        var tarihStr = xmlDoc.Descendants(cbc + "IssueDate").FirstOrDefault()?.Value;
        if (DateTime.TryParse(tarihStr, out var tarih)) sonuc.Tarih = tarih;

        // Tedarikçi Bilgileri
        var supplier = xmlDoc.Descendants(cac + "AccountingSupplierParty").FirstOrDefault();
        if (supplier != null)
        {
            sonuc.TedarikciVkn = supplier.Descendants(cbc + "ID")
                .FirstOrDefault(x => x.Attribute("schemeID")?.Value == "VKN" || x.Attribute("schemeID")?.Value == "TCKN")?.Value;
            
            sonuc.TedarikciUnvan = supplier.Descendants(cac + "PartyName").FirstOrDefault()?.Element(cbc + "Name")?.Value 
                                   ?? supplier.Descendants(cac + "Person").Select(p => p.Element(cbc + "FirstName")?.Value + " " + p.Element(cbc + "FamilyName")?.Value).FirstOrDefault();
        }

        // Duplikat Kontrolü
        if (!string.IsNullOrEmpty(sonuc.FaturaNo))
        {
            sonuc.ZatenKayitliMi = await _db.Alislar.AnyAsync(a => a.FaturaNo == sonuc.FaturaNo);
        }

        // 2. Satır Bilgileri
        var satirlar = xmlDoc.Descendants(cac + "InvoiceLine");
        foreach (var xSatir in satirlar)
        {
            var taxSubtotal = xSatir.Element(cac + "TaxTotal")?.Element(cac + "TaxSubtotal");
            var allowance = xSatir.Elements(cac + "AllowanceCharge").FirstOrDefault(a => a.Element(cbc + "ChargeIndicator")?.Value == "false");

            var vmSatir = new FaturaSatirAnalizVM
            {
                FaturaUrunAdi = xSatir.Element(cac + "Item")?.Element(cbc + "Name")?.Value ?? "Bilinmeyen Ürün",
                Barkod = xSatir.Element(cac + "Item")?.Element(cac + "SellersItemIdentification")?.Element(cbc + "ID")?.Value 
                         ?? xSatir.Element(cac + "Item")?.Element(cac + "StandardItemIdentification")?.Element(cbc + "ID")?.Value,
                Miktar = (int)ParseDecimal(xSatir.Element(cbc + "InvoicedQuantity")?.Value),
                BirimFiyat = ParseDecimal(xSatir.Element(cac + "Price")?.Element(cbc + "PriceAmount")?.Value),
                KdvOrani = (int)ParseDecimal(taxSubtotal?.Element(cbc + "Percent")?.Value ?? xSatir.Element(cac + "TaxTotal")?.Descendants(cbc + "Percent").FirstOrDefault()?.Value),
                IskontoTutari = ParseDecimal(allowance?.Element(cbc + "Amount")?.Value),
                IskontoOrani = ParseDecimal(allowance?.Element(cbc + "MultiplierFactorNumeric")?.Value) * 100
            };

            sonuc.Satirlar.Add(vmSatir);
        }

        // 3. Eşleştirme Motorunu Çalıştır
        // Tedarikçiyi VKN/Unvan üzerinden bul veya şimdilik null geç
        var dbTedarikci = await _db.Tedarikciler.FirstOrDefaultAsync(t => t.Ad == sonuc.TedarikciUnvan);
        
        await UrunleriEsletAsync(sonuc.Satirlar, dbTedarikci?.Id ?? 0);

        return sonuc;
    }

    private decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        // XML standard decimal point is always '.'
        if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }
        return 0;
    }

    public async Task<List<FaturaSatirAnalizVM>> UrunleriEsletAsync(List<FaturaSatirAnalizVM> satirlar, int tedarikciId)
    {
        foreach (var satir in satirlar)
        {
            // 1. Yol: Barkod ile kesinkes bul
            if (!string.IsNullOrEmpty(satir.Barkod))
            {
                var urun = await _db.Urunler.FirstOrDefaultAsync(u => u.Barkod == satir.Barkod);
                if (urun != null)
                {
                    satir.SistemUrunId = urun.Id;
                    satir.SistemUrunAdi = urun.Ad;
                    satir.Durum = EslesmeTipi.Tam;
                    satir.GuvenSkoru = 100;
                    continue;
                }
            }

            // 2. Yol: Geçmiş Hafızadan (FaturaEslesme) bak
            var eslesme = await _db.FaturaEslesmeleri
                .Include(f => f.SistemUrun)
                .FirstOrDefaultAsync(f => f.TedarikciId == tedarikciId && f.FaturaUrunAdi == satir.FaturaUrunAdi);
            
            if (eslesme != null)
            {
                satir.SistemUrunId = eslesme.SistemUrunId;
                satir.SistemUrunAdi = eslesme.SistemUrun.Ad;
                satir.Durum = eslesme.ManuelMi ? EslesmeTipi.Tam : EslesmeTipi.Oneri;
                satir.GuvenSkoru = 90;
                continue;
            }

            // 3. Yol: İsim Benzerliği (Simple string match for now)
            var benzerUrun = await _db.Urunler.FirstOrDefaultAsync(u => u.Ad.Contains(satir.FaturaUrunAdi) || satir.FaturaUrunAdi.Contains(u.Ad));
            if (benzerUrun != null)
            {
                satir.SistemUrunId = benzerUrun.Id;
                satir.SistemUrunAdi = benzerUrun.Ad;
                satir.Durum = EslesmeTipi.Oneri;
                satir.GuvenSkoru = 70;
                continue;
            }

            // Bulunamadı
            satir.Durum = EslesmeTipi.Yok;
            satir.GuvenSkoru = 0;
        }

        return satirlar;
    }
}
