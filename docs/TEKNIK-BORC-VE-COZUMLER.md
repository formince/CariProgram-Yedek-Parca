# CariErinc — Teknik Borç & Çözüm Rehberi

> **Tarih:** 2026-04-11  
> **Durum:** Uygulama başarıyla derleniyor ve çalışıyor (`dotnet run` başarılı).  
> **Bu Belgenin Amacı:** Tespit edilen sorunları ve bunların birebir ne yapılıp nasıl düzeltileceğini açıkça belgelemek. Bir sonraki oturumda ne yapılacağını tartışmadan doğrudan bu plandaki sırayla çalışılmalıdır.

---

## ⚙️ Proje Genel Durumu (2026-04-11 İtibarıyla)

| Katman | Durum |
|---|---|
| Repository katmanı | ✅ Kaldırıldı (boş klasör yapısı kaldı) |
| Service + DbContext | ✅ Servisler artık doğrudan `AppDbContext` kullanıyor |
| `BorcHelper` merkezi borç yönetimi | ✅ Tüm borç güncellemeleri `BorcHelper.Guncelle()` üzerinden |
| `KasaGelirEkle` / `KasaGiderCik` | ✅ Tüm kasa işlemleri `IKasaService` üzerinden |
| `StokCikisYap` / `StokGirisYap` | ✅ Tüm stok işlemleri `IStokService` üzerinden |
| `GetVarsayilanKdvOraniAsync` | ✅ KDV tekrarı kaldırıldı |
| Interface sadeleşmesi | ✅ `IStokService` internal metodları temizlendi |
| `UrunFiyatUpdateResult` | ✅ `ViewModels/` klasöründe tanımlı |
| Repository boş klasörleri | ❌ `Repositories/` ve `Repositories/Interfaces/` silinmedi |

### Kesinleşen Mimari Kararlar (Değiştirilemez)
1. **Repository katmanı yok.** Servisler doğrudan `AppDbContext` kullanır.
2. **`SaveChangesAsync` yalnızca servislerde çağrılır** — asla helper veya alt metodlarda değil.
3. **`BorcHelper.Guncelle`** her türlü borç değişimi için tek kapı.
4. **`KasaGelirEkle` / `KasaGiderCik`** her türlü kasa hareketi için tek kapı.
5. **`StokCikisYap` / `StokGirisYap`** her türlü stok hareketi için tek kapı.

---

## 🔴 KRİTİK — Veri Tutarsızlığı Riskleri

### SORUN 1: `AlisYapAsync` Transaction'sız

**Dosya:** `CariErinc/Services/AlisService.cs`  
**Metod:** `AlisYapAsync()`

**Problem:**
```csharp
// MEVCUT (YANLIŞ) — satır ~169-173
_db.Alislar.Add(alis);
await _db.SaveChangesAsync();   // ← 1. commit: alış kaydedildi
// ... stok, kasa, fiyat işlemleri ...
// Herhangi birinde exception → alış kaydı var ama stok/kasa güncellenmedi
```

**Çözüm — AlisYapAsync içine transaction eklenmeli:**

```csharp
public async Task<ServiceResult> AlisYapAsync(AlisVM vm)
{
    // ... validation kodları aynı kalır ...

    await using var tx = await _db.Database.BeginTransactionAsync(); // ← EKLE
    try
    {
        _db.Alislar.Add(alis);
        await _db.SaveChangesAsync(); // artık transaction içinde

        foreach (var satir in gecerliSatirlar)
        {
            // ... stok, kasa, fiyat işlemleri ...
        }

        // Vadeli alışta tedarikçi borcunu güncelle
        if (vm.OdemeTipi == AlisOdemeTipi.Vadeli)
            BorcHelper.Guncelle(tedarikci, toplamTutar);

        // NOT: _fiyatService.UpdateAlisFiyatiAsync çağrısından önce
        // bu metodun kendi SaveChanges yapmasını engelleyelim (Sorun 2 ile birlikte çöz)

        await _db.SaveChangesAsync();
        await _auditLog.LogEkleAsync(...);
        await tx.CommitAsync(); // ← EKLE
        return ServiceResult.Success("Alış başarıyla kaydedildi.");
    }
    catch
    {
        await tx.RollbackAsync(); // ← EKLE
        throw;
    }
}
```

**Dikkat:** `AlisGuncelleAsync` ve `AlisSilAsync` zaten transaction kullanıyor — sadece `AlisYapAsync` eksik.

---

### SORUN 2: `UrunFiyatService.UpdateAlisFiyatiAsync` Bağımsız SaveChanges

**Dosya:** `CariErinc/Services/UrunFiyatService.cs`  
**Metod:** `UpdateAlisFiyatiAsync()`

**Problem:**
```csharp
// MEVCUT (YANLIŞ) — satır ~53-61
_db.UrunFiyatAuditlari.Add(audit);
_db.Urunler.Update(urun);
await _db.SaveChangesAsync(); // ← Bağımsız commit! AlisService transaction'ı rollback etse bile bu geri alınamaz.
```

**Neden tehlikeli?**  
`AlisService` içinde `_fiyatService.UpdateAlisFiyatiAsync(...)` çağrısı yapılıyor.  
`AlisService` transaction açık → hata → rollback → **ama fiyat zaten commit edildi** → veri tutarsız.

**Çözüm — `SaveChangesAsync` bu metoddan kaldırılmalı:**

```csharp
public async Task<ServiceResult<UrunFiyatUpdateResult>> UpdateAlisFiyatiAsync(
    int urunId, decimal yeniFiyat, string neden, string kullanici, int? alisId = null)
{
    var urun = await _db.Urunler.FindAsync(urunId);
    if (urun == null)
        return ServiceResult<UrunFiyatUpdateResult>.Failure($"Ürün ID {urunId} bulunamadı.");

    if (yeniFiyat < 0)
        return ServiceResult<UrunFiyatUpdateResult>.Failure("Alış fiyatı 0'dan küçük olamaz.");

    if (Math.Abs(urun.AlisFiyati - yeniFiyat) < 0.01m)
        return ServiceResult<UrunFiyatUpdateResult>.Success(new UrunFiyatUpdateResult { IsChanged = false }, "Fiyat değişimi yok.");

    var audit = new UrunFiyatAudit
    {
        UrunId = urunId,
        EskiFiyat = urun.AlisFiyati,
        YeniFiyat = yeniFiyat,
        Neden = neden,
        KullaniciAdi = kullanici,
        AlisId = alisId,
        Tarih = DateTime.UtcNow
    };

    _db.UrunFiyatAuditlari.Add(audit);
    urun.AlisFiyati = yeniFiyat;
    urun.SonAlisTarihi = DateTime.UtcNow;
    urun.GuncellenmeTarihi = DateTime.UtcNow;
    _db.Urunler.Update(urun);

    // ← SaveChangesAsync KALDIRILDI. Çağıran servis yönetecek.

    var resultData = new UrunFiyatUpdateResult
    {
        IsChanged = true,
        OldPrice = audit.EskiFiyat,
        NewPrice = yeniFiyat,
        ChangedAt = audit.Tarih
    };

    return ServiceResult<UrunFiyatUpdateResult>.Success(resultData, "Ürün fiyatı başarıyla güncellendi.");
}
```

**Etki analizi:** `UpdateAlisFiyatiAsync` çağrılan tek yer `AlisService.AlisYapAsync` ve `AlisGuncelleAsync`. Her ikisi de kendi `SaveChangesAsync` çağırıyor. Bu değişiklikten sonra çağıran servis tüm commit'i yönetir.

---

### SORUN 3: `Satis.IptalEdildi` ↔ `SatisDurum.Iptal` Çakışması

**Dosya:** `CariErinc/Models/Satis.cs` ve `CariErinc/Services/SatisService.cs`

**Problem:**  
`Satis` modelinde iki ayrı "iptal" mekanizması var:
```csharp
public bool IptalEdildi { get; set; } = false;           // boolean flag
public SatisDurum Durum { get; set; } = SatisDurum.Tamamlandi; // enum: Taslak, Tamamlandi, Iptal
```

`TamIptalAsync` içinde sadece `IptalEdildi = true` set ediliyor, `Durum = SatisDurum.Iptal` hiç set edilmiyor.  
→ `SatisDurum.Iptal` enum değeri hiçbir zaman kullanılmıyor (dead code).

**Tercih edilecek çözüm — `IptalEdildi` boolean'ı kaldırıp `SatisDurum.Iptal` kullanmak:**

> [!CAUTION]
> Bu değişiklik migration gerektirir ve `IptalEdildi`'ye bağlı tüm view/sorgu referansları taranmalıdır. Riski düşük tutmak için alternatif seçenek B önerilir.

**Seçenek A (Temiz, ama migration gerekli):**
1. `Satis.cs`'den `IptalEdildi`, `IptalTarihi`, `IptalNedeni` kaldırılır.
2. `SatisDurum.Iptal` kullanılır.
3. `TamIptalAsync` içinde: `satis.Durum = SatisDurum.Iptal;`
4. Tüm `s.IptalEdildi` referansları → `s.Durum == SatisDurum.Iptal` olarak güncellenir.
5. Migration oluşturulur.

**Seçenek B (Güvenli, kısa vadeli fix):**  
`SatisDurum.Iptal` enum değerini kaldır, sadece `IptalEdildi` boolean'ı kullan.  
`TamIptalAsync` içinde `Durum` alanına dokunma. Tutarsızlık ortadan kalkar.

```csharp
// Satis.cs — Seçenek B
public enum SatisDurum { Taslak, Tamamlandi } // Iptal kaldırıldı
```

**Mevcut sorgularda ne değişir?** `ApplyFilters` içinde:
```csharp
query = query.Where(s => s.Durum == SatisDurum.Tamamlandi); // hâlâ doğru
query = query.Where(s => !s.IptalEdildi);                   // hâlâ doğru
```

---

## 🟠 YÜKSEK — Kod Kalitesi

### SORUN 4: `SatisYapAsync` İçinde Çift FindAsync Döngüsü

**Dosya:** `CariErinc/Services/SatisService.cs`  
**Metod:** `SatisYapAsync()`

**Problem:**
```csharp
// 1. döngü (satır ~102-115) — toplamTutar için:
foreach (var satir in gecerliSatirlar)
{
    var urun = await _db.Urunler.FindAsync(satir.UrunId); // ← FindAsync
    toplamTutar += satir.Miktar * satir.BirimFiyat;
}

// 2. döngü (satır ~128-152) — detay ekleme için:
foreach (var satir in gecerliSatirlar)
{
    var urunForKdv = await _db.Urunler.FindAsync(satir.UrunId); // ← YİNE FindAsync
    // ...
}
```

**Çözüm — 1. döngüyü kaldır, hesaplamayı 2. döngüye taşı:**

```csharp
public async Task<ServiceResult> SatisYapAsync(SatisVM vm)
{
    // ... validation ...

    var satis = new Satis { /* ... */ };
    decimal araToplam = 0;
    var varsayilanKdv = await _ayarService.GetVarsayilanKdvOraniAsync();

    // TEK döngü yeterli:
    foreach (var satir in gecerliSatirlar)
    {
        if (satir.BirimFiyat < 0 || satir.BirimFiyat > MaxNumeric18_2)
            return ServiceResult.Failure("Birim fiyat veri tabanı sınırını aşıyor.");

        if (satir.IndirimOrani < 0 || satir.IndirimOrani > 100)
            return ServiceResult.Failure("Satır indirim oranı 0-100 arası olmalıdır.");

        var urun = await _db.Urunler.FindAsync(satir.UrunId);
        if (urun == null)
            return ServiceResult.Failure("Ürün bulunamadı.");

        var kdvOrani = satir.KdvOrani > 0 ? satir.KdvOrani : (urun.KdvOrani > 0 ? urun.KdvOrani : varsayilanKdv);
        var (brutTutar, indirimTutari, netTutar, kdvTutari, indirimOraniKayit) =
            SatisTutarHesaplayici.SatirHesapla(satir.Miktar, satir.BirimFiyat, satir.IndirimOrani, kdvOrani, satir.SatirNetTutarHedef);

        if (brutTutar > MaxNumeric18_2 || netTutar > MaxNumeric18_2)
            return ServiceResult.Failure("Tutar hesaplaması veri tabanı sınırını aşıyor.");

        araToplam += netTutar;

        satis.SatisDetaylari.Add(new SatisDetay
        {
            UrunId = satir.UrunId,
            Miktar = satir.Miktar,
            BirimFiyat = satir.BirimFiyat,
            KdvOrani = kdvOrani,
            KdvTutari = kdvTutari,
            IndirimOrani = indirimOraniKayit,
            IndirimTutari = indirimTutari,
            NetTutar = netTutar,
            AlisBirimFiyati = urun.AlisFiyati
        });
    }

    // ... genel indirim ve transaction bloğu devam eder ...
}
```

---

## 🟡 ORTA — Güvenlik & Yapısal Temizlik

### SORUN 5: `AccessDeniedPath` Döngüsel Yönlendirme

**Dosya:** `CariErinc/Program.cs` — satır 40

**Problem:**
```csharp
options.AccessDeniedPath = "/auth/login"; // ← GİRİŞ YAPMIŞ ama yetkisiz kullanıcı login'e gider = döngü
```

**Çözüm:**
```csharp
options.AccessDeniedPath = "/auth/yetkisiz"; // veya "/Home/AccessDenied"
```

Ardından `AuthController`'a bir `Yetkisiz` action ve basit bir view eklenmeli:
```csharp
[AllowAnonymous]
public IActionResult Yetkisiz() => View();
```

---

### SORUN 6: Exception Mesajları Kullanıcıya Sızıyor

**Dosya:** `SatisService.cs`, `AlisService.cs`, `VeresiyeService.cs`, `StokService.cs`

**Problem:**
```csharp
catch (Exception ex)
{
    return ServiceResult.Failure($"Hata: {ex.Message}"); // SQL detayı açığa çıkabilir
}
```

**Çözüm — Tüm catch bloklarında:**
```csharp
catch (Exception ex)
{
    // Loglama eklenene kadar geçici olarak console'a yaz
    Console.Error.WriteLine($"[HATA] {ex}");
    // Kullanıcıya sadece genel mesaj:
    return ServiceResult.Failure("İşlem sırasında bir hata oluştu. Lütfen tekrar deneyin.");
}
```

**Etkilenen metodlar:**
- `SatisService.UpdateSatisAsync` satır 369
- `SatisService.TamIptalAsync` satır 446
- `SatisService.KismiIadeAsync` satır 536
- `SatisService.TaslakKaydetAsync` satır 608

---

### SORUN 7: Boş `Repositories/` Klasörü

**Durum:** `Repositories/Interfaces/` klasörü mevcut ama içi tamamen boş.

**Çözüm:**
```powershell
# Proje kökünden çalıştır:
Remove-Item -Recurse -Force "CariErinc/Repositories"
```

Derleme testi: `dotnet build` — hata olmamalı.

---

### SORUN 8: Model Default'larında `DateTime.Now`

**Etkilenen dosyalar:**

| Dosya | Property | Mevcut | Olması Gereken |
|---|---|---|---|
| `Satis.cs` | `Tarih` | `DateTime.Now` | `DateTime.UtcNow` |
| `Veresiye.cs` | `Tarih` | `DateTime.Now` | `DateTime.UtcNow` |
| `Alis.cs` | `Tarih` | `DateTime.Now` | `DateTime.UtcNow` |
| `Tedarikci.cs` | `OlusturulmaTarihi` | `DateTime.Now` | `DateTime.UtcNow` |
| `Musteri.cs` | `OlusturulmaTarihi` | `DateTime.Now` | `DateTime.UtcNow` |

**Not:** `Program.cs`'de `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` aktif olduğu için şimdilik çalışıyor. Ancak bu switch kaldırılırsa PostgreSQL'e `Local` timezone yazmak hata verir. Temiz kod için `UtcNow` kullanılmalı.

**Çözüm:** Her dosyada tek satır değişiklik.

```csharp
// Satis.cs
public DateTime Tarih { get; set; } = DateTime.UtcNow; // DateTime.Now → DateTime.UtcNow

// Veresiye.cs
public DateTime Tarih { get; set; } = DateTime.UtcNow;

// Alis.cs
public DateTime Tarih { get; set; } = DateTime.UtcNow;

// Tedarikci.cs
public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;

// Musteri.cs
public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;
```

---

## ✅ Uygulama Sırası (Önerilen)

```
[ ] 1. SORUN 2: UrunFiyatService.SaveChangesAsync'i kaldır
        → Test: dotnet build
[ ] 2. SORUN 1: AlisYapAsync'e transaction ekle
        → Test: dotnet build + manuel alış testi
[ ] 3. SORUN 3: SatisDurum.Iptal çakışması - Seçenek B (enum'dan Iptal değerini kaldır)
        → Test: dotnet build
[ ] 4. SORUN 7: Boş Repositories/ klasörünü sil
        → Test: dotnet build
[ ] 5. SORUN 5: AccessDeniedPath düzelt
        → Test: yetkisiz kullanıcı ile test
[ ] 6. SORUN 8: DateTime.Now → DateTime.UtcNow (tüm modeller)
        → Test: dotnet build
[ ] 7. SORUN 4: SatisYapAsync çift döngü tek döngüye indir
        → Test: manuel satış testi
[ ] 8. SORUN 6: Exception mesajları gizle
        → Test: dotnet build
```

---

## 🚫 Dokunulmayacaklar

| Dosya | Neden |
|---|---|
| `Services/SatisTutarHesaplayici.cs` | Mükemmel yazılmış, test edilmiş |
| `Services/AuditLogService.cs` | Bağımsız, sorunsuz |
| `Services/KdvOranlariAyarlari.cs` | Konfigürasyon, değiştirme |
| `Services/YetkiCacheService.cs` | Bağımsız, sorunsuz |
| `Services/LookupService.cs` | Direkt `_db` kullanıyor, bilerek öyle |
| `Helpers/TurkceHelper.cs` | ILike pattern helper, sorunsuz |
| `Helpers/BorcHelper.cs` | Merkezi borç yönetimi — doğru tasarım |
| `Helpers/ServiceResult.cs` | Merkezi hata yönetimi — doğru tasarım |
| `Program.cs` `AppContext.SetSwitch` | Legacy timestamp flag — kaldırma |

---

## 📌 Mimari Kurallar (Bir Sonraki AI Oturumuna Not)

1. **Her yeni servis metodu** → `SaveChangesAsync` son satırda, metod içinde bir kez.
2. **Birden fazla entity güncelleyen metodlar** → `BeginTransactionAsync` + try/catch + `CommitAsync`/`RollbackAsync`.
3. **`BorcHelper.Guncelle`** → Müşteri veya tedarikçi `ToplamBorc` alanına **hiçbir yerde doğrudan atama yapılmaz.**
4. **`IKasaService.KasaGelirEkle` / `KasaGiderCik`** → Kasa hareketinde **hiçbir yerde `new KasaHareket()` oluşturulmaz.**
5. **`IStokService.StokCikisYap` / `StokGirisYap`** → Stok manipülasyonunda **hiçbir yerde `urun.StokAdedi +=` veya `-=` yazılmaz.**
6. **Repository yok.** `IXxxRepository` veya `XxxRepository` adında yeni dosya oluşturulmaz.
7. **Interface içinde DTO tanımlanmaz.** `UrunFiyatUpdateResult` gibi sınıflar `ViewModels/` klasöründe olur.
8. **Controller'dan servis metoduna giden tüm çağrılar interface üzerinden** — doğrudan implementasyon sınıfına bağımlılık eklenmez.
9. **Exception'lar kullanıcıya gösterilmez** — `ServiceResult.Failure("Genel hata mesajı")` döndürülür, iç detay loglanır.

---

*Bu belge yalnızca analiz ve planlama içerir. Değişiklik yapmadan önce `dotnet build` control edilmelidir.*
