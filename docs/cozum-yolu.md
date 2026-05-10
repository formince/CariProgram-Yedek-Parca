# CariErinc — Mimari Temizlik Planı

> **Bu dosya hem karar belgesi hem ilerleme takibidir.**  
> Her AI oturumu önce buraya bakmalı, tamamlananları görmeli, sıradaki adımdan devam etmelidir.  
> **Kafaya göre ek değişiklik yapılmaz. Sadece bu plandaki adımlar uygulanır.**

---

## Durum Göstergesi

```
[ ] = Yapılmadı
[/] = Devam ediyor
[x] = Tamamlandı
```

---

## Kesinleşen Kararlar (Değişmez)

| # | Karar |
|---|---|
| 1 | Repository katmanı tamamen kaldırılır (22 dosya) |
| 2 | Ortak işler → `BorcHelper`, `AyarService.GetVarsayilanKdvOraniAsync()` static/servis metodu |
| 3 | UTC → `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` + `DateTime.UtcNow`, `DateTimeUtcFilter` silinir |
| 4 | Interface sadeleşir — iç metodlar private, `GetUrunlerByIdsAsync` IUrunService'e taşınır |
| 5 | `SaveChangesAsync` her zaman serviste, repository içinde asla |

---

## Adımlar

---

### ADIM 1 — Program.cs: Legacy Timestamp Flag [x]

**Dosya:** `CariErinc/Program.cs`

**Ne yapılır:**  
Dosyanın en üstüne, `var builder = WebApplication.CreateBuilder(args);` satırından **önce** eklenir:

```csharp
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
```

**Kontrol:** Uygulama derlenmeli ve çalışmalı.

---

### ADIM 2 — DateTimeUtcFilter Silinir [x]

**Dosya:** `CariErinc/Helpers/DateTimeUtcFilter.cs`

**Ne yapılır:**  
Bu dosya tamamen silinir.

**Sonra:** `DateTimeUtcFilter` kullanılan tüm yerlerdeki referanslar temizlenir:

| Dosya | Satır | Ne yapılır |
|---|---|---|
| `Repositories/SatisRepository.cs` | `ToUtcInclusiveStart`, `ToUtcExclusiveEndAfterLastDay` | Repository silineceği için bu adımı atla |
| `Repositories/AlisRepository.cs` | aynı | atla |
| `Repositories/VeresiyeRepository.cs` | aynı | atla |
| `Repositories/StokRepository.cs` | aynı | atla |
| `Repositories/KasaRepository.cs` | aynı | atla |
| `Repositories/DashboardRepository.cs` | aynı | atla |
| `Services/KasaService.cs` | UTC dönüşüm kodu | Direkt DateTime kullan, flag halleder |
| `Services/RaporService.cs` | UTC dönüşüm kodu | Direkt DateTime kullan |
| `Services/StokService.cs` | `private static NormalizeUtc()` metodu | Bu private metodu sil, `DateTime.UtcNow` kullan |

**Not:** Repository'deki `DateTimeUtcFilter` kullanımları repository silinince otomatik gider (Adım 9). Şimdilik sadece servis dosyalarına bak.

**Kontrol:** Derleme hatası olmamalı.

---

### ADIM 3 — BorcHelper Oluşturulur [x]

**Yeni Dosya:** `CariErinc/Helpers/BorcHelper.cs`

**İçeriği:**
```csharp
using CariErinc.Models;

namespace CariErinc.Helpers;

/// <summary>
/// Müşteri borcu güncellemeleri için merkezi nokta.
/// Tüm borç değişimleri bu metot üzerinden yapılır.
/// delta pozitif → borç artar (satış, yeni veresiye)
/// delta negatif → borç azalır (ödeme, iptal, iade)
/// </summary>
public static class BorcHelper
{
    public static void Guncelle(Musteri musteri, decimal delta)
    {
        musteri.ToplamBorc = Math.Max(0, musteri.ToplamBorc + delta);
    }
}
```

**Sonra BorcHelper'ın kullanılacağı yerler:**

| Dosya | Mevcut Kod | Yeni Kod |
|---|---|---|
| `SatisService.SatisYapAsync()` | `musteri.ToplamBorc += satis.ToplamTutar` | `BorcHelper.Guncelle(musteri, +satis.ToplamTutar)` |
| `SatisService.UpdateSatisAsync()` | `musteri.ToplamBorc -= eskiSatis.Veresiye.Tutar` | `BorcHelper.Guncelle(musteri, -eskiSatis.Veresiye.Tutar)` |
| `SatisService.UpdateSatisAsync()` | `musteri.ToplamBorc += eskiSatis.ToplamTutar` | `BorcHelper.Guncelle(musteri, +eskiSatis.ToplamTutar)` |
| `SatisService.TamIptalAsync()` | `musteri.ToplamBorc -= kalanBorc` | `BorcHelper.Guncelle(musteri, -kalanBorc)` |
| `SatisService.KismiIadeAsync()` | `musteri.ToplamBorc = Math.Max(0, ...)` | `BorcHelper.Guncelle(musteri, -toplamIadeTutari)` |
| `VeresiyeService.OdemeAlAsync()` | `veresiye.Musteri.ToplamBorc -= tutar` | `BorcHelper.Guncelle(veresiye.Musteri, -tutar)` |
| `VeresiyeService.KompleKapatAsync()` | `v.Musteri.ToplamBorc -= buVeresiyeOdenen` | `BorcHelper.Guncelle(v.Musteri, -buVeresiyeOdenen)` |
| `VeresiyeService.SaveAsync()` | `musteri.ToplamBorc += vm.Tutar` | `BorcHelper.Guncelle(musteri, +vm.Tutar)` |
| `VeresiyeService.SaveAsync()` | `veresiye.Musteri.ToplamBorc += fark` | `BorcHelper.Guncelle(veresiye.Musteri, fark)` |
| `VeresiyeService.SilAsync()` | `musteri.ToplamBorc -= veresiye.Tutar` | `BorcHelper.Guncelle(musteri, -veresiye.Tutar)` |

**Kontrol:** Derleme hatası olmamalı.

---

### ADIM 4 — AyarService: KDV Metodu Eklenir [x]

**Dosya:** `CariErinc/Services/Interfaces/IAyarService.cs`

**Eklenir:**
```csharp
Task<int> GetVarsayilanKdvOraniAsync();
```

**Dosya:** `CariErinc/Services/AyarService.cs`

**Eklenir:**
```csharp
public async Task<int> GetVarsayilanKdvOraniAsync()
{
    var str = await GetAsync("VarsayilanKdv");
    return int.TryParse(str, out var v) && v >= 0 ? v : 20;
}
```

**Sonra bu tekrarlayan kodlar kaldırılır:**

| Dosya | Kaldırılacak Tekrar |
|---|---|
| `SatisService.SatisYapAsync()` satır ~89 | `var kdvStr = await _ayarService.GetAsync("VarsayilanKdv"); var varsayilanKdv = ...` |
| `SatisService.UpdateSatisAsync()` satır ~290 | aynısı |
| `AlisService.AlisYapAsync()` satır ~88 | aynısı |
| `AlisService.AlisGuncelleAsync()` satır ~322 | aynısı |

Hepsi → `var varsayilanKdv = await _ayarService.GetVarsayilanKdvOraniAsync();`

**Kontrol:** Derleme hatası olmamalı.

---

### ADIM 5 — Interface Sadeleştirmesi [x]

#### IStokService.cs
**Dosya:** `CariErinc/Services/Interfaces/IStokService.cs`

**Kaldırılır:**
```csharp
Task<(bool basarili, string mesaj)> HareketEkleAsync(StokHareketVM vm);
Task<(bool basarili, string mesaj)> HareketGuncelleAsync(StokHareketVM vm);
Task<(bool basarili, string mesaj)> HareketSilAsync(int id);
```

**StokService.cs'de bu 3 metot `public` → `private` yapılır.**

#### ISatisService.cs
**Dosya:** `CariErinc/Services/Interfaces/ISatisService.cs`

**Kaldırılır:**
```csharp
Task<(bool basarili, string mesaj)> TamIptalAsync(int satisId, string? neden);
Task<List<Urun>> GetUrunlerByIdsAsync(List<int> urunIds);
```

**SatisService.cs'de `TamIptalAsync` `private` yapılır.**

#### IUrunService.cs
**Dosya:** `CariErinc/Services/Interfaces/IUrunService.cs`

**Eklenir:**
```csharp
Task<List<Urun>> GetByIdsAsync(List<int> urunIds);
```

**UrunService.cs'de** `SatisService`'den gelen `GetUrunlerByIdsAsync` implement edilir, metod adı `GetByIdsAsync` olur.

**SatisController.cs'de** varsa `GetUrunlerByIdsAsync` çağrısı → `_urunService.GetByIdsAsync(...)` olarak güncellenir.

#### UrunFiyatUpdateResult Taşıması
**Dosya:** `CariErinc/Services/Interfaces/IUrunFiyatService.cs`

`UrunFiyatUpdateResult` sınıfı bu dosyadan alınır.

**Yeni Dosya:** `CariErinc/ViewModels/UrunFiyatUpdateResult.cs` olarak oluşturulur.

**Kontrol:** Derleme hatası olmamalı.

---

### ADIM 6 — Repository Silinir [ ]

> [!WARNING]  
> En riskli adım. Dikkatli ve sırayla yapılır.

**Sıra:**

1. `Repositories/Interfaces/` klasörü tamamen silinir
2. Her repository sınıfındaki sorgu mantığı ilgili servise taşınır (aşağıda detay)
3. Servis constructor'larından `_repo` field'ı ve parametresi kaldırılır
4. `Program.cs`'den repository DI kayıtları silinir
5. `Repositories/` klasörü silinir

**Servis bazında taşıma tablosu:**

| Repository | İlgili Servis | Taşınacak Metodlar |
|---|---|---|
| `AlisRepository` | `AlisService` | `GetPagedAsync`, `GetCountAsync`, `GetAllAsync`, `GetByIdAsync`, `GetVadeliAcikAlislarAsync` |
| `MusteriRepository` | `MusteriService` | `GetPagedAsync`, `GetCountAsync`, `GetAllAsync`, `GetByIdAsync`, `SilinebilirMiAsync` |
| `SatisRepository` | `SatisService` | `GetAllAsync`, `GetByIdAsync`, `GetTaslaklarAsync`, `DeleteAsync` |
| `StokRepository` | `StokService` | `GetPagedAsync`, `GetCountAsync`, `GetAllAsync`, `GetByIdAsync`, `GetUrunHareketleriAsync` |
| `TedarikciRepository` | `TedarikciService` | `GetAllAsync`, `GetByIdAsync`, `SilinebilirMiAsync` |
| `UrunRepository` | `UrunService` | `GetPagedAsync`, `GetCountAsync`, `GetAllAsync`, `GetByIdAsync`, `GetByBarkodAsync`, `BarkodVarMiAsync`, `GetKritikStoklaAsync`, `StokHareketiVarMiAsync` |
| `VeresiyeRepository` | `VeresiyeService` | `GetPagedAsync`, `GetCountAsync`, `GetAllAsync`, `GetByIdAsync` |
| `KasaRepository` | `KasaService` | `GetPagedAsync`, `GetCountAsync`, `GetAllAsync`, `GetByIdAsync`, `GetToplamGelirAsync`, `GetToplamGiderAsync`, `GetBakiyeAsync` |
| `GiderKategoriRepository` | `KasaService` | `GetAllAsync`, `GetByIdAsync` |
| `DashboardRepository` | `DashboardService` | `GetBugunkuSatisToplamAsync`, `GetKasaBakiyesiAsync`, `GetAcikVeresiyeToplamAsync`, `GetAcikVeresiyeMusteriSayisiAsync`, `GetKritikStokSayisiAsync`, `GetKritikStokUrunleriAsync` |
| `UrunFiyatAuditRepository` | `UrunFiyatService` | `AddAsync`, `GetByUrunIdAsync`, `GetLastByUrunIdAsync` |

**Not:** Sorgu mantığı birebir taşınır. `DateTimeUtcFilter` referansları bu sırada zaten temizlenmiş olacak (Adım 2).

**Kontrol:** Derleme + temel manuel test (Dashboard, Satış, Alış).

---

### ADIM 7 — Son Kontrol [ ]

Tüm adımlar sonrası manuel test:

- [ ] Uygulama derlenir (sıfır hata)
- [ ] Login → Dashboard açılır
- [ ] Satış yapma → stok düşer, kasa artar
- [ ] Veresiye satış → müşteri borcu artar
- [ ] Veresiye ödeme alma → borç azalır
- [ ] Alış yapma → stok artar
- [ ] Satış iptali → stok geri döner
- [ ] Rapor sayfaları açılır

---

## Dokunulmayacaklar (Kesinlikle Elleme)

| Dosya | Neden |
|---|---|
| `Services/SatisTutarHesaplayici.cs` | Mükemmel yazılmış, risk almaya değmez |
| `Services/AuditLogService.cs` | Bağımsız ve temiz |
| `Services/KdvOranlariAyarlari.cs` | Konfigürasyon, sorun yok |
| `Services/YetkiCacheService.cs` | Bağımsız |
| `Services/LookupService.cs` | Şu an direkt `_db` kullanıyor, repository zaten yok, dokunma |
| `Helpers/TurkceHelper.cs` | ILike pattern helper, sorunsuz |

---

## İlerleme Geçmişi

- **2026-04-09** — Kararlar kesinleşti, plan oluşturuldu. Git commit alındı.
