# 🚀 CariErinc - Kapsamlı Teknik Mimari ve Geliştirme Rehberi

CariErinc; perakende işletmeleri (kırtasiye, market, hırdavat vb.) için geliştirilmiş, yüksek performans odaklı, modern mimariye sahip tam kapsamlı bir ticari otomasyon ve cari takip sistemidir. Bu döküman, projenin "Tek Gerçek Kaynak" (Source of Truth) rehberidir.

---

## 🏛️ 1. Mimari Yapı ve Teknik Katmanlar (Architecture)

Proje, **Clean Architecture** prensiplerinden esinlenerek yapılandırılmış, sürdürülebilirliği ve test edilebilirliği en üst düzeyde tutan katmanlı bir yapıda inşa edilmiştir.

### 1.1 Katmanların Detaylı Tanımı

| Katman | Sorumluluk Alanı | İçerdiği Bileşenler |
|--------|-----------------|---------------------|
| **Presentation (Web)** | Kullanıcı etkileşimi, sayfa render etme ve giriş güvenliği. | `Controllers`, `Razor Views`, `Middlewares`, `TagHelpers`. |
| **Service (Logical)** | İş mantığının (Business Logic) kalbi. Hesaplamalar ve orkestrasyon. | `ISatisService`, `IStokService`, `IAlisService`, `IRaporService`. |
| **Repository (DAL)** | Veritabanı ile doğrudan iletişim kuran katman. | `Repositories/Interfaces`, `Repositories/Implementations`. |
| **Data (Persistence)** | Veritabanı bağlantısı ve şema yönetimi. | `AppDbContext`, `Migrations`, `SeedData`. |
| **Models (Domain)** | Veritabanı tablolarının sınıfsal karşılıkları. | `Entities`, `Enums`. |
| **ViewModels (DTO)** | UI katmanına veri taşımak için özelleştirilmiş sınıflar. | `SatisVM`, `StokVM`, `KullaniciYonetimVM`. |

### 1.2 İstek Yaşam Döngüsü (Request Lifecycle)
Bir kullanıcı bir butona tıkladığında sistemde şu süreçler işler:
1. **Routing:** İstek, ilgili `Controller/Action` ikilisine yönlendirilir.
2. **Middleware:** `YetkiMiddleware` isteği yakalar, kullanıcının bu aksiyona yetkisi olup olmadığını kontrol eder.
3. **Controller:** Gelen veriyi (varsa) `ViewModel` ile karşılar ve ilgili `Service` metodunu çağırır.
4. **Service:** Gerekli hesaplamaları (örneğin satışta stok kontrolü ve kâr hesabı) yapar ve `Repository` üzerinden DB işlemini başlatır.
5. **Persistence:** EF Core üzerinden PostgreSQL'e veri yazılır.
6. **Logging:** İşlem başarılıysa `AuditLogService` aracılığıyla kayıt tutulur.
7. **Response:** Sonuç kullanıcıya `Redirect`, `View` veya `Json` olarak iletilir.

---

## ⚙️ 2. Unified Architecture Standartları (Standart Geliştirme Modeli)

Projenin en ayırt edici özelliği, tüm veri yönetim modüllerinde uygulanan **Unified (Birleşik) Mimari** standartlarıdır.

### 2.1 Tek Form Standartı (Unified Form GET)
Projede `Create.cshtml` ve `Edit.cshtml` ayrımı kaldırılmıştır. Bunun yerine her modülde tek bir `Form(int? id)` eylemi bulunur:
- **Ekleme Senaryosu:** `id` boştur, yeni bir nesne oluşturulur.
- **Düzenleme Senaryosu:** `id` doludur, veritabanından çekilen veri form alanlarına basılır.
- **Dinamik UI:** View içerisinde `isEdit` değişkeni ile "Kaydet" veya "Güncelle" etiketleri otomatik değişir.

### 2.2 Merkezi Save Standartı (Unified Save POST)
Ekleme ve güncelleme mantığı servis katmanında tek bir `SaveAsync` metodunda birleştirilmiştir:
```csharp
// Örnek Servis Mantığı
public async Task<(bool, string)> SaveAsync(UrunVM vm) {
    if (vm.Id == 0) {
        // Yeni Ürün Ekleme Mantığı
    } else {
        // Mevcut Ürün Güncelleme Mantığı
    }
}
```
Bu sayede iş kuralları ve validasyonlar tek bir merkezden yönetilerek kod tekrarı %50 oranında azaltılmıştır.

### 2.3 AJAX Silme Mekanizması (Unified Delete/Sil)
Silme işlemleri sayfa yenilemeden, SweetAlert2 ve AJAX kullanılarak modern bir şekilde gerçekleştirilir:
- **Tetikleyici:** `.btn-delete` sınıfı.
- **JS Altyapısı:** `wwwroot/js/site.js` içindeki `initDeleteButtons` fonksiyonu.
- **Öznitelikler:** `data-url` (silme endpoint'i), `data-title` (uyarı başlığı), `data-text` (uyarı mesajı).
- **Sonuç:** Sunucudan dönen JSON cevabına göre satır UI'dan kaldırılır.

---

## 📊 3. Modül Bazlı İş Kuralları (Business Rules)

### 3.1 Satış ve Hızlı Satış (Sales Logic)
- **Hızlı Satış:** Tamamen JavaScript (`satis-hizli.js`) ile yönetilen dinamik bir yapıdır. Sepet işlemleri tarayıcı tarafında yapılır ve tek seferde sunucuya gönderilir.
- **KDV Hesaplama:** Her satış kaleminin KDV'si satır bazlı hesaplanır.
- **İskonto Dağılımı:** Genel indirim, sepet tutarına göre satırlara ağırlıklı olarak dağıtılır (bu, doğru kâr/zarar analizi için kritiktir).
- **Taslak Sepetler:** Bekleyen satışlar veritabanında saklanabilir ve sonra geri yüklenebilir.

### 3.2 Stok ve Maliyet Yönetimi (Stock & COGS)
- **Stok Hareket:** Her işlem (`Satis`, `Alis`, `Iade`) `StokHareket` tablosuna bir kayıt düşer.
- **Maliyet Takibi (COGS):** Satılan her ürünün maliyeti, satış anındaki alış fiyatı üzerinden kaydedilir. Bu, geçmişe dönük fiyat değişimlerinin kâr raporunu bozmasını engeller.
- **Kritik Stok:** Ürün kartındaki `MinStokUyari` seviyesi altına düşen ürünler dashboard üzerinde vurgulanır.

### 3.3 Finans ve Cari Takip (Finance & Credit)
- **Veresiye Mantığı:** Bir satışın ödeme tipi "Veresiye" ise, `Veresiyeler` tablosunda borç kaydı açılır.
- **Parçalı Ödeme:** Bir borca birden fazla tarihli ödeme yapılabilir. Kalan borç her ödemede dinamik hesaplanır.
- **Kasa Entegrasyonu:** Her nakit giriş/çıkış hareketi `KasaHareketleri` tablosunda merkezi olarak toplanır.

---

## 🛡️ 4. Sistem Güvenliği ve Middleware

### 4.1 Yetkilendirme (RBAC)
Sistem, Aksiyon Bazlı Yetkilendirme (Action-Based Authorization) kullanır:
- **Middleware:** `YetkiMiddleware` her isteği kullanıcının rolleriyle karşılaştırır.
- **Ccache:** Yetkiler performans için `IYetkiCacheService` (MemoryCache) üzerinde tutulur.
- **Admin Muafiyeti:** `is_admin == true` olan kullanıcılar tüm kontrolleri atlar.

### 4.2 Şifreleme ve Veri Güvenliği
- **Hash:** Şifreler `BCrypt.Net-Next` ile 12 round saltlanarak saklanır.
- **XSRF:** Tüm POST işlemlerinde `ValidateAntiForgeryToken` zorunludur.
- **SQL Injection:** Tüm veritabanı iletişimi `EF Core` ve `Parameterized Queries` (Npgsql) üzerinden yapılarak bu atak önlenir.

---

## 🛠️ 5. Teknik "Gotcha" ve Çözümler (Technical Debt & Fixes)

### 5.1 PostgreSQL UTC Sorunu
Npgsql 6.0+ versiyonlarında karşılaşılan `DateTime` çakışmalarını önlemek için:
- **Helper:** `Helpers/DateTimeUtcFilter.cs` sınıfı tüm tarih filtrelemeleri için tek merkezdir.
- **Standard:** Kod tarafında her zaman `DateTime.UtcNow` kullanılmalıdır.

### 5.2 Yuvarlama Kusurları (Rounding Issues)
Para birimi hesaplamalarında oluşan `0.01 ₺` sapmalarını gidermek için:
- **Çözüm:** Satış anında hesaplanan net tutar `SatirNetTutarHedef` alanı ile sunucuya taşınır ve hesaplamalar bu hedef tutar üzerinden tersine işletilir.

### 5.3 Denetim İzi (Audit Logging)
Kritik veri değişimleri (`Create`, `Update`, `Delete`) için:
- `AuditLogs` tablosu devrededir.
- Güncellemelerde eski nesne ve yeni nesne JSON olarak saklanır.

---

## 🤖 6. AI Asistan Geliştirme Rehberi (AI Workflow Rules)

**Projeye dahil olan AI asistanları için uyulması ZORUNLU kurallar:**

1.  **Repository Katmanını Atlanamaz:** `Controller` içinde asla `_db.Users` gibi doğrudan DB erişimi yapma.
2.  **Unified CRUD Standartı:** Yeni modül eklerken `Create/Edit` sayfaları oluşturma. `Form.cshtml` yapısını uygula.
3.  **AJAX Standartı:** Silme düğmelerini form-post ile yapma. `.btn-delete` ve `site.js` altyapısını kullan.
4.  **UTC Tarih Kuralı:** `DateTime.Now` gördüğün yeri `DateTime.UtcNow` yap. PostgreSQL buna ihtiyaç duyar.
5.  **Hata Yönetimi:** Servislerden her zaman `(bool success, string message)` şeklinde geri bildirim dön.
6.  **Naming:** Metod isimlerinde asenkron yapıya uygun olarak `...Async` takısını kullan.
7.  **Zengin Estetik:** UI dokunuşlarında `site.css` içindeki modern temaya (gradient, shadow, rounded) sadık kal.

---

## 📂 7. Klasör Yapısı (Folder Structure)

```text
CariErinc/
├── Controllers/       # HTTP İsteklerini karşılayan kontrolcüler
├── Services/          # İş mantığı (Business Logic) katmanı
├── Repositories/      # Veritabanı erişim (Data Access) katmanı
├── Data/              # EF Core Context ve Migrations
├── Models/            # Veritabanı Entity ve Enum sınıf tanımları
├── ViewModels/        # UI için özelleştirilmiş veri taşıma objeleri
├── Helpers/           # Tarih filtreleme vb. yardımcı sınıflar
├── Middleware/        # Yetki ve Hata middleware katmanları
├── wwwroot/           # JS, CSS ve Görsel varlıklar
└── Views/             # Razor template'leri
```

### 7.1 Kritik Dosyalar
- `wwwroot/js/site.js`: Global AJAX ve UI fonksiyonları.
- `wwwroot/css/site.css`: Projenin modern tasarım dili CSS kuralları.
- `Helpers/DateTimeUtcFilter.cs`: PostgreSQL tarih uyumluluğu.
- `Services/YetkiCacheService.cs`: Yetki performans yönetimi.

---

## 🚀 8. Kurulum ve Çalıştırma (Setup & Deployment)

### 8.1 Geliştirme Kurulumu
1. **SDK:** .NET 10.0+ ve PostgreSQL 15+ kurulu olmalıdır.
2. **Bağlantı:** `appsettings.json` içerisindeki `DefaultConnection` stringini kendi local şifrenize göre güncelleyin.
3. **Database:** Terminalden `dotnet ef database update` komutunu çalıştırın.
4. **Seed:** İlk açılışta `admin` / `admin123` bilgileriyle giriş yapabilirsiniz.

### 8.2 Production Notları
- Şifreler `dotnet user-secrets` veya Environment Variables üzerinden verilmelidir.
- `Hosting.Lifetime` logları üzerinden sistem durumu periyodik izlenmelidir.
- Veritabanı yedekleme (Back-up) stratejisi belirlenmelidir.

---

## 📦 9. Harici Bağımlılıklar (Libraries & Packages)

| Kütüphane | Kullanım Amacı |
|-----------|----------------|
| **Npgsql.EntityFrameworkCore.PostgreSQL** | Veritabanı sağlayıcısı. |
| **BCrypt.Net-Next** | Güvenli şifre hashleme. |
| **Microsoft.AspNetCore.Authentication.Cookies** | Kimlik doğrulama. |
| **SweetAlert2** | Modern uyarı ve onay pencereleri. |
| **EntityFrameworkCore.Proxies** | Lazy Loading desteği. |

---

## 📈 10. Yol Haritası (Roadmap)
Aşağıdaki özellikler projenin gelecek versiyonlarında eklenmesi planlanan teknik borçlardır:
- [ ] **Excel Export:** Tüm raporların Excel olarak dışa aktarılması.
- [ ] **Thermal Print:** Satış anında termal fiş yazıcı desteği.
- [ ] **Dashboard Charts:** Satış istatistiklerinin grafiklerle (Chart.js) görselleştirilmesi.
- [ ] **Toplu Stok Güncelleme:** Excel üzerinden toplu ürün/fiyat güncelleme.

---

## 🏁 11. Sonuç
CariErinc, sadece bir muhasebe programı değil; sürdürülebilir yazılım mimarisinin bir örneğidir. Bu rehberdeki kurallara uymak, projenin büyümesi ve bakımı için hayati önem taşır.

---
*Dökümantasyon Versiyonu: 3.5.0 (Ultimate Edition)*  
*Son Güncelleme: 9 Nisan 2026*  
*Hazırlayan: CariErinc Geliştirme Ekibi*
