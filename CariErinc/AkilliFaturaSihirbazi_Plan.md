# Akıllı Fatura Sihirbazı — Uygulama Planı
**CariErinc Projesi | v2.0 — Güncellenmiş & Hata Giderilmiş**

---

## 1. NuGet Paketleri

| Paket | Amaç | Ücretsiz mi? |
|---|---|---|
| `System.Xml.Linq` | E-Fatura XML (UBL 2.1) parse | ✅ .NET yerleşik |
| `Mscc.GenerativeAI` | Gemini API — fatura görseli analizi | ✅ Ücretsiz tier var |
| `Microsoft.EntityFrameworkCore` | FaturaEslesme tablosu yönetimi | ✅ Açık kaynak |

> ❌ **FuzzySharp kaldırıldı** — Gemini zaten doğru ürün adını getiriyor. Kalan vakalarda `string.Contains()` yeterli, fazladan bağımlılık gereksiz.

### Model Seçimi

```json
// appsettings.json
"Gemini": {
  "ApiKey": "AIza...",
  "Model": "gemini-3-flash-preview"
}
```

> ⚠️ `gemini-3-flash-preview` ücretsiz ama hâlâ preview statüsünde. Sorun yaşanırsa stabil alternatif olarak `gemini-2.5-flash` kullanılabilir. Her ikisi de ücretsiz tier'a sahip.
> 
> ⚠️ `gemini-2.0-flash` ve `gemini-2.0-flash-lite` **1 Haziran 2026'da kapatılıyor** — kullanılmamalı.

---

## 2. Veri Modeli

### FaturaEslesme Tablosu

Sistemin "öğrenmesi" bu tablo sayesinde olur. Her onaylanan eşleşme buraya yazılır, bir dahaki faturada otomatik tanınır.

```csharp
public class FaturaEslesme
{
    public int Id { get; set; }
    public int TedarikciId { get; set; }
    public string FaturaUrunAdi { get; set; }   // Faturada yazan ham metin
    public int SistemUrunId { get; set; }        // Bizim sistemdeki karşılığı
    public int KullaniciId { get; set; }         // [YENİ] Kim eşleştirdi
    public int EslesmeSkoru { get; set; }        // [YENİ] 0-100, AI güven skoru
    public bool ManuelMi { get; set; }           // [YENİ] AI mi, insan mı düzeltti
    public DateTime KayitTarihi { get; set; }
}
```

> ✅ `ManuelMi = true` kayıtlar AI'dan daha güvenilir. Eşleştirme sırasında önce insan onaylı kayıtlara bakılmalı.

### Duplikat Fatura Kontrolü

Aynı fatura iki kez yüklenirse stok ve borç iki kez artar. Fatura numarasına göre kontrol zorunlu:

```csharp
var faturaNo = xmlDoc.Descendants(cbc + "ID").FirstOrDefault()?.Value;
var zatenVar = await _db.Alislar.AnyAsync(a => a.FaturaNo == faturaNo);
if (zatenVar)
    return BadRequest("Bu fatura daha önce sisteme işlendi.");
```

---

## 3. Servis Katmanı

### IFaturaAnalizService

```csharp
public interface IFaturaAnalizService
{
    // Dosya tipine göre (XML / Görüntü) ayrıştırıcıyı seçer
    Task<FaturaAnalizSonucVM> AnalizEtAsync(IFormFile dosya);

    // Geçmiş eşleşmelere göre ürünleri sistemdekilerle bağlar
    Task<List<FaturaSatirVM>> UrunleriEsletAsync(List<FaturaHamSatir> hamSatirlar);
}
```

### XML Parser — GİB Namespace Uyarısı

Türkiye e-fatura şeması standart UBL'den farklı namespace kullanır. Yanlış yazılırsa tüm parser `null` döner, saatler kaybedilir.

```csharp
// DOĞRU — GİB namespace'leri
XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

var faturaNo = xmlDoc.Descendants(cbc + "ID").FirstOrDefault()?.Value;
var tarih    = xmlDoc.Descendants(cbc + "IssueDate").FirstOrDefault()?.Value;

var urunAdi  = satir.Element(cac + "Item")
                    ?.Element(cbc + "Name")?.Value;
var miktar   = satir.Element(cbc + "InvoicedQuantity")?.Value;
var fiyat    = satir.Element(cac + "Price")
                    ?.Element(cbc + "PriceAmount")?.Value;
```

### Görüntü Parser — Gemini Prompt

Tutarlı JSON dönmesi için prompt kalitesi kritik:

```csharp
var prompt = """
Bu bir tedarikçi faturasıdır. Sadece aşağıdaki JSON formatında yanıt ver, başka hiçbir şey yazma:
{
  "faturaNo": "...",
  "tarih": "YYYY-MM-DD",
  "satirlar": [
    { "urunAdi": "...", "miktar": 0, "birimFiyat": 0.0, "kdvOrani": 18 }
  ]
}
""";
```

### Gemini Rate Limit Guard

Ücretsiz tier dakikada 15 istek sınırı var. Hızlı yüklemelerde API 429 hatası döner:

```csharp
private DateTime _lastGeminiCall = DateTime.MinValue;

private async Task RateLimitBekle()
{
    var fark = (DateTime.Now - _lastGeminiCall).TotalMilliseconds;
    if (fark < 4100) await Task.Delay((int)(4100 - fark));
    _lastGeminiCall = DateTime.Now;
}
```

---

## 4. UI/UX Akışı — Sihirbaz Adımları

Sihirbaz, mevcut Alış ekranında bir **Bootstrap Modal** olarak çalışır.

| Adım | Ekran | Kullanıcı Aksiyonu |
|---|---|---|
| 1 — Yükleme | Modal açılır | XML veya fotoğraf sürükle-bırak |
| 2 — Analiz | Yükleniyor animasyonu | Bekle (AI işliyor) |
| 3 — Eşleştirme | Sol: fatura / Sağ: sistem ürünleri | Yeşil=tamam, Sarı=onayla, Kırmızı=ekle |
| 4 — Inline Ürün Ekleme | Mini form (modal üstünde açılır) | Fiyat ve kategori gir, kaydet |
| 5 — Onay | Özet tablo | Kaydet → Alış sayfasına aktar |

### Durum Renk Sistemi

- 🟢 **Yeşil** — Tam eşleşme: barkod veya geçmiş kayıttan bulundu
- 🟡 **Sarı** — Öneri: AI benzetti, kullanıcı onayı bekliyor
- 🔴 **Kırmızı** — Bilinmiyor: ilk kez görülen ürün, `(+)` ile eklenecek

### Adım 5 — Alış Sayfasına Veri Aktarımı

```csharp
// FaturaAnalizController — Onayla aksiyonu
TempData["FaturaVeri"] = JsonSerializer.Serialize(onaylananSatirlar);
return RedirectToAction("YeniAlis", "Alis");

// AlisController — YeniAlis aksiyonu
if (TempData["FaturaVeri"] is string json)
{
    var satirlar = JsonSerializer.Deserialize<List<AlisSatirVM>>(json);
    ViewBag.OtomatikSatirlar = satirlar;
}
```

---

## 5. Uygulama Sıralaması

Her adım çalışır durumdayken bir sonrakine geç.

| Sıra | Görev | Neden Bu Sıra? |
|---|---|---|
| 1 | `FaturaEslesme` migration | Tablo olmadan hiçbir şey çalışmaz |
| 2 | XML Parser + GİB namespace | Hatasız sonuç, AI'a gerek yok |
| 3 | XML ile uçtan uca test | Çalışan temel olmadan görüntü parser yazma |
| 4 | Gemini Image Parser | XML çalıştıktan sonra ekle |
| 5 | `FaturaAnalizController` (AJAX) | İki parser hazır olunca bağla |
| 6 | `faturaWizard.js` — Modal UI | Backend hazırken frontend yaz |
| 7 | Alış sayfasına entegrasyon | TempData ile bağlantı kur |

---

## 6. Hata ve İstisna Yönetimi

| Durum | Davranış |
|---|---|
| Gemini API erişilemiyor | "AI analizi şu an kullanılamıyor, XML yükleyin veya manuel girin" uyarısı |
| Geçersiz XML formatı | "Geçerli bir e-fatura XML dosyası yükleyin" uyarısı |
| Rate limit (429) | 4 saniye bekle, otomatik tekrar dene (max 3 deneme) |
| Duplikat fatura | "Bu fatura daha önce sisteme işlendi" uyarısı, kayıt yapılmaz |
| Görüntü okunamadı | "Fotoğraf net değil, lütfen tekrar çekin" yönlendirmesi |

---

## 7. Modül Bağımsızlığı

Bu modül tamamen izole edilmiştir. İstenildiğinde şu 4 bileşen silinerek tamamen kaldırılabilir, ana sisteme etkisi yoktur:

- `Services/FaturaAnalizService.cs`
- `Controllers/FaturaAnalizController.cs`
- `wwwroot/js/faturaWizard.js`
- İlgili migration dosyası

---

## 8. API Anahtarı Kurulumu

```json
// appsettings.json
{
  "Gemini": {
    "ApiKey": "AIza...",
    "Model": "gemini-3-flash-preview"
  }
}
```

API anahtarı almak için: **aistudio.google.com** → ücretsiz hesap → API Key oluştur.

> ⚠️ API anahtarını Git'e commit etme. `appsettings.json` dosyasını `.gitignore`'a ekle veya **User Secrets** kullan:
> ```bash
> dotnet user-secrets set "Gemini:ApiKey" "AIza..."
> ```