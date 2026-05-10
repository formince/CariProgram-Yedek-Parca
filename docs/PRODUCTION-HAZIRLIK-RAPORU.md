# CariErinc — Gerçek Hayat Hazırlık Raporu

> Tarih: Mart 2026  
> Durum: Geliştirme aşaması — production'a taşınmadan önce aşağıdaki maddeler ele alınmalıdır.

---

## 🔴 P0 — Kritik (Deploy Öncesi Zorunlu)

### 1. Veritabanı Şifresi `appsettings.json`'da Düz Metin
**Sorun:** `appsettings.json` içinde gerçek PostgreSQL şifresi açık metin olarak duruyor ve git geçmişine işlendi.

```json
"DefaultConnection": "Host=localhost;Database=kirtasiye;Username=postgres;Password=571632Yunus%"
```

**Çözüm:**
- Geliştirme ortamı için `dotnet user-secrets` kullanın
- Production için ortam değişkeni veya bir secrets manager (HashiCorp Vault, Azure Key Vault) kullanın
- `.gitignore`'a `appsettings.Production.json` ekleyin
- **Mevcut şifreyi hemen değiştirin** — git geçmişine işlendi

```bash
# Geliştirme için kullanım
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Password=GERCEK_SIFRE"
```

---

### 2. Varsayılan Admin Hesabı (`admin` / `admin123`)
**Sorun:** `Program.cs` seed kodunda hardcoded şifre. Veritabanı sıfırlandığında veya yeni kurulumda bu hesap otomatik oluşuyor.

**Çözüm:** 
- İlk girişte şifre değiştirmeyi zorla (veya kurulum sırasında şifreyi parametre olarak al)
- Seed kullanıcısını production build'larında devre dışı bırakın

---

## 🟠 P1 — Yüksek Öncelik (İlk Sprint'te Düzeltilmeli)

### 3. Logout GET ile Yapılıyor (CSRF Açığı)
**Sorun:** `GET /Auth/Logout` herhangi bir sayfa ya da `<img src="/Auth/Logout">` etiketiyle tetiklenebilir.

**Çözüm:** Logout'u POST yap ve CSRF token doğrula.

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Logout() { ... }
```

Sidebar'daki logout linkini forma çevir:
```html
<form asp-controller="Auth" asp-action="Logout" method="post" class="d-inline">
    @Html.AntiForgeryToken()
    <button type="submit" class="sidebar-link">⬅️ Çıkış</button>
</form>
```

---

### 4. Login Brute-Force Koruması Yok
**Sorun:** `/Auth/Login` POST'una sınırsız şifre denemesi yapılabilir.

**Çözüm:** .NET'in yerleşik `IDistributedCache` veya basit bir `IMemoryCache` ile IP bazlı deneme sayacı:
```csharp
// Örnek: 5 başarısız deneme → 5 dakika bekleme
var key = $"login_fail_{RemoteIp}";
var count = _cache.Get<int>(key);
if (count >= 5) return View("LoginKilitlendi");
```
Ya da `ASP.NET Core Rate Limiting` middleware'i (.NET 7+):
```csharp
builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("login", opts => {
    opts.PermitLimit = 5;
    opts.Window = TimeSpan.FromMinutes(5);
}));
```

---

### 5. AJAX Endpoint'lerinde CSRF Koruması Eksik
**Sorun:** `RolController.YetkiToggle` ve `SidebarAyarGuncelle` JSON POST endpoint'lerinde `[ValidateAntiForgeryToken]` yok.

**Çözüm — seçenek A:** Tüm controller'lara `[AutoValidateAntiforgeryToken]` ekle ve AJAX'ta header gönder:
```javascript
// Her fetch isteğinde antiforgery token gönder
headers: {
    'Content-Type': 'application/json',
    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
}
```

**Çözüm — seçenek B:** Bu endpoint'leri `[IgnoreAntiforgeryToken]` ile işaretle ve API auth (bearer token) kullan.

---

### 6. Hata Mesajları Kullanıcıya Sızıyor
**Sorun:** `return (false, $"Hata: {ex.Message}")` — SQL hataları, şema bilgileri, iç detaylar kullanıcıya gösteriliyor.

```csharp
// Tehlikeli — kaldırılmalı:
catch (Exception ex)
{
    return (false, $"Hata: {ex.Message}"); // SQL detayı açığa çıkar
}
```

**Çözüm:**
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Satış işlemi başarısız");
    return (false, "İşlem sırasında bir hata oluştu. Lütfen tekrar deneyin.");
}
```

---

### 7. `AccessDeniedPath` Yanlış Yapılandırılmış
**Sorun:** `options.AccessDeniedPath = "/auth/login"` — giriş yapmış ama yetkisiz kullanıcıyı login sayfasına yönlendiriyor. Kullanıcı döngüye giriyor.

**Çözüm:**
```csharp
options.AccessDeniedPath = "/Auth/Yetkisiz";
```

---

## 🟡 P2 — Orta Öncelik (İkinci Sprint)

### 8. `DateTime.Now` Model Varsayılanları (PostgreSQL UTC Sorunu)
**Sorun:** Birçok modelde `DateTime.Now` kullanılıyor — bu `DateTimeKind.Local` oluşturur. Npgsql 6+ `timestamp with time zone` sütunlarına `Local` veya `Unspecified` yazamaz.

Etkilenen modeller: `Urun.OlusturulmaTarihi`, `Musteri.OlusturulmaTarihi`, `KasaHareket.Tarih`, `StokHareket.Tarih`, `Veresiye.Tarih`, `Alis.Tarih` vb.

**Çözüm:** Tüm modellerde `DateTime.UtcNow` kullan:
```csharp
// Tüm modellerde değiştir:
public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow; // ✅
// yerine:
public DateTime OlusturulmaTarihi { get; set; } = DateTime.Now;   // ❌
```

Ya da `Program.cs`'te Npgsql'e `Legacy timestamp` modu açılabilir (geçici geçici çözüm):
```csharp
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
```

---

### 9. `KullaniciAdi` Veritabanı Seviyesinde Unique Değil
**Sorun:** Benzersizlik kontrolü sadece uygulama kodunda. İki eş zamanlı istek aynı kullanıcı adını oluşturabilir.

**Çözüm:** Migration ile DB'ye unique index ekle:
```csharp
// AppDbContext.OnModelCreating içine:
modelBuilder.Entity<Kullanici>()
    .HasIndex(k => k.KullaniciAdi)
    .IsUnique();
```

---

### 10. `GiderKategoriController` Direkt Entity Binding (Mass Assignment)
**Sorun:** Controller action'ı `GiderKategori` modelini direkt parametre olarak alıyor. `SilinebilirMi`, `AktifMi` gibi alanlar form üzerinden manipüle edilebilir.

```csharp
[HttpPost]
public async Task<IActionResult> Create(GiderKategori kategori) // ❌ Tehlikeli
```

**Çözüm:** Dedicated ViewModel kullan:
```csharp
public class GiderKategoriCreateVM
{
    [Required] public string Ad { get; set; }
    public KasaHareketTipi Tip { get; set; }
}
```

---

### 11. Admin Kontrolü Dört Controller'da Tekrar Ediyor
**Sorun:** `AdminKontrol()` metodu `RolController`, `KullaniciYonetimiController`, `AyarlarController`'da kopyalanmış.

**Çözüm:** Authorization policy veya base controller:
```csharp
// Option A — Policy:
builder.Services.AddAuthorization(o =>
    o.AddPolicy("AdminOnly", p => p.RequireClaim("is_admin", "true")));

[Authorize(Policy = "AdminOnly")]
public class RolController : Controller { ... }
```

---

### 12. `Musteri.ToplamBorc` Denormalizasyon Riski
**Sorun:** `ToplamBorc` birden fazla yerde elle güncelleniyor (satış, iptal, kısmi iade). Transaction rollback'i bu değeri tutarsız bırakabilir.

**Çözüm:** Ya hesaplanan bir alan yapın (view veya computed column):
```sql
-- Veritabanı view'ı ile:
SELECT MusteriId, SUM(Tutar - OdenenTutar) as ToplamBorc FROM Veresiyeler GROUP BY MusteriId;
```
Ya da düzenli reconciliation servisi çalıştırın.

---

### 13. `YetkiCacheService.InvalidateRol` Her Şeyi Temizliyor
**Sorun:** Bir rol güncellendiğinde tüm `IMemoryCache` temizleniyor (işletme ayarları dahil).

**Çözüm:** Rol bazlı cache key pattern kullanın ve sadece ilgili keyleri temizleyin. Bunun için `IMemoryCache` yerine `ICacheTagHelper` veya `IHybridCache` (.NET 9+) kullanabilirsiniz. En basit düzeltme:
```csharp
public void InvalidateRol(int rolId)
{
    // Sadece bu role ait cache key'leri sil
    _cache.Remove(AllKeyPrefix + rolId);
    _cache.Remove(AllKeyPrefix + "sidebar_" + rolId);
    // İşletme ayarları cache'ini dokunma
}
```

---

## 🔵 P3 — Düşük Öncelik / Teknik Borç

### 14. Sıfır Test Kapsamı
Proje büyük karmaşıklığa rağmen hiç test içermiyor.

**Önerilen test hedefleri (öncelik sırasıyla):**

| Test | Neden Önemli |
|------|-------------|
| `SatisService.SatisYapAsync` | Stok, kasa, veresiye'yi aynı anda etkiliyor |
| `SatisService.TamIptalAsync` | Karmaşık geri alma mantığı |
| `YetkiMiddleware` | Güvenlik katmanı — hatalı test edilirse tüm sistem açılır |
| `AlisService.OdemeAlAsync` | Vadeli borç hesabı |
| `VeresiyeService.OdemeAlAsync` | Fazla ödeme guard'ı |

```bash
# Test projesi oluşturma:
dotnet new xunit -n CariErinc.Tests
dotnet add CariErinc.Tests reference CariErinc
```

---

### 15. Kısmi İadede Veresiye Bakiyesi Güncellenmıyor
**Sorun:** `KismiIadeAsync` içinde veresiye kaydının `Tutar` alanı düzeltilmiyor, sadece `Aciklama`'ya not ekleniyor. Veresiye raporu yanlış tutar gösterir.

**Çözüm:** İade tutarını hesaplayıp `Veresiye.Tutar`'dan düş.

---

### 16. Müşteri ve Tedarikçi Detay Sayfası Yok
Müşteri ve tedarikçi için liste ve düzenleme var, ama detay sayfası (geçmiş satışlar, veresiyeler, ödemeler) yok.

---

### 17. Rapor Sayfalarında Dışa Aktarma Yok
Raporlar sadece HTML'de görüntüleniyor. Excel/PDF export olmadan ticari kullanım için yetersiz kalabilir.

**Önerilen:** `ClosedXML` veya `EPPlus` ile Excel export butonu.

---

### 18. `SatisService`, Repository Soyutlamasını Kırıyor
**Sorun:** `SatisService` hem `ISatisRepository` hem de direkt `AppDbContext` kullanıyor. Bu katmanlı mimarinin amacını zedeliyor ve test edilebilirliği düşürüyor.

---

### 19. `AllowedHosts: "*"` Production'da Riskli
**Sorun:** `appsettings.json`'da `"AllowedHosts": "*"` var. Host header injection saldırılarına açık.

**Çözüm:**
```json
"AllowedHosts": "kirtasiye.example.com"
```

---

### 20. Uygulama Loglama Altyapısı Eksik
`ILogger` inject edilmiş controller ve servislerde kullanılmıyor. Hatalar sessizce yutulabiliyor.

**Çözüm:** Serilog veya NLog entegrasyonu ile yapılandırılmış loglama (dosyaya veya bir log aggregator'a).

---

## 📋 Eksik Özellikler (Ticari Kullanım İçin)

| Özellik | Açıklama |
|---------|---------|
| **Barkod yazıcı entegrasyonu** | Ürün barkodu basma |
| **Fiş yazıcı (thermal)** | Termal yazıcı desteği (ESC/POS) |
| **Yedekleme / Restore** | DB backup + restore UI |
| **Excel/PDF export** | Rapor ve listeler için dışa aktarma |
| **Müşteri detay sayfası** | Satış geçmişi, veresiye özeti |
| **Tedarikçi detay sayfası** | Alış geçmişi, borç durumu |
| **Stok sayım modülü** | Toplu stok sayım girişi |
| **Çoklu para birimi** | Dövizle alış/satış |
| **E-Fatura / E-Arşiv** | GİB entegrasyonu (zorunlu olabilir) |
| **SMS / E-posta bildirimi** | Veresiye hatırlatma, stok uyarı |
| **Dashboard widget konfigürasyonu** | Hangi KPI'ların görüneceği |
| **İlk giriş şifre değiştirme zorlaması** | Güvenlik politikası |

---

## ✅ İyi Yapılanlar

- Repository + Service + Controller katmanlı mimari tutarlı uygulanmış
- EF Core transactions kritik işlemlerde kullanılmış
- `DateTimeUtcFilter` ile PostgreSQL UTC sorunu merkezi çözülmüş
- Cookie auth + route tabanlı RBAC + dinamik sidebar entegrasyonu temiz
- Audit log altyapısı var
- Satış iade ve iptal senaryoları destekleniyor
- Taslak sepet (draft) özelliği uygulanmış
- `IYetkiCacheService` ve `IAyarService` Singleton + `IServiceScopeFactory` ile doğru DI pattern'i

---

*Bu rapor `dotnet build` başarılı olan Mart 2026 snapshot'ı üzerinden hazırlanmıştır.*
