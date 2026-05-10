# CariErinc — Servis & Repository Katmanı Analiz Raporu

> **Tarih:** 2026-04-09  
> **Kapsam:** `Services/`, `Repositories/`, `Services/Interfaces/`, `Repositories/Interfaces/`  
> **Kod değişikliği içermez — yalnızca tespit ve önerilerden oluşur.**

---

## Yönetici Özeti

Proje, mimari açıdan **doğru yönde** kurulmuştur: katmanlı yapı (Controller → Service → Repository → DbContext) vardır, interface'ler yazılmıştır, CQRS benzeri iş bölümü denenmiştir. Ancak süreç içinde birbiriyle çelişen kararlar alınmış, bazı katmanlar fiilen işlevsiz hâle gelmiş, bazı servislerde ise tek bir dosyada çözülmesi gereken sorunlar sistematik kaosa dönmüştür.

Tespit edilen sorunlar **6 ana başlık** altında toplanmaktadır.

---

## 1. En Kritik Sorun: Service + AppDbContext İkili Bağımlılığı

### Ne Oldu?

Repository pattern'in temel amacı servislerin `AppDbContext`'i **hiç görmemesi**dir. Servis yalnızca repository interface'ini kullanmalı; tüm SQL erişimi repository'de olmalıdır.

Ancak mevcut durumda **tüm iş servisleri hem repository'e hem de doğrudan `AppDbContext`'e bağımlıdır:**

| Servis | `_repo` var mı? | `_db` var mı? |
|---|---|---|
| `SatisService` | ✅ | ✅ |
| `AlisService` | ✅ | ✅ |
| `VeresiyeService` | ✅ | ✅ |
| `MusteriService` | ✅ | ✅ |
| `TedarikciService` | ✅ | ✅ |
| `StokService` | ✅ | ✅ |
| `UrunService` | ✅ | ✅ |
| `UrunFiyatService` | ✅ | ✅ |
| `RaporService` | ✅ (3 repo) | ✅ |
| `LookupService` | ❌ | ✅ |
| `KasaService` | ✅ | ❌ |
| `DashboardService` | ✅ | ❌ |
| `AyarService` | ❌ | ✅ (scope ile) |

**Sonuç:** Repository katmanı, servisteki `_db` kullanımının yanında dekoratif kalmaktadır. Servisler doğrudan `_db.Satislar`, `_db.Urunler`, `_db.StokHareketler` vb. ile sorgu yazmaktadır.

### Neden Problem?

- Repository pattern'in hiçbir faydası elde edilemiyor: test edilebilirlik yok, bağımlılık enjeksiyonu ile mock yapılamıyor.
- Veri erişim mantığı iki farklı yerde (repository sınıfı + servis içi `_db` çağrıları) dağılmış durumda.
- Yeni geliştirici kodu okurken hangi sorgunun nerede olduğunu bilemez.

### Somut Örnekler

**`SatisService.cs` içinde doğrudan `_db` kullanımı:**
```csharp
// Satır 71: Repository yerine doğrudan DbContext
var urun = await _db.Urunler.FindAsync(satir.UrunId);

// Satır 136: Repo.Add* yerine doğrudan DbSet
_db.Satislar.Add(satis);

// Satır 147: Başka bir entity, bambaşka bir DbSet
_db.StokHareketler.Add(new StokHareket { ... });

// Satır 160: Ve bir başkası
_db.KasaHareketler.Add(new KasaHareket { ... });
```

`SatisRepository.cs`'deki `AddAsync` metodu ise:
```csharp
public async Task AddAsync(Satis satis)
{
    _db.Satislar.Add(satis);
    await _db.SaveChangesAsync();  // Ayrı bir SaveChanges!
}
```

Bu gereksiz çünkü `SatisService` gene de `_db.SaveChangesAsync()` çağırıyor. Repository'nin `SaveChanges`'i ile servisin `SaveChanges`'i **çakışmaktadır.**

---

## 2. SaveChanges Kaos Noktaları

`SaveChangesAsync()` hem repository sınıflarında hem de servislerde çağrılıyor. Bu **çift kayıt, eksik kayıt ve transaction çakışmaları** üretir.

### Senaryo A: Repository'de Yok, Servis Çağırıyor

`MusteriService.SaveAsync()` akışı:
```
_repo.AddAsync(musteri)        → MusteriRepository: _db.Musteriler.Add(musteri) [SaveChanges YOK]
await _db.SaveChangesAsync()   → Servis kendi çağırıyor
```

```
_repo.UpdateAsync(musteri)     → MusteriRepository: _db.Musteriler.Update(musteri) [SaveChanges YOK]
await _db.SaveChangesAsync()   → Servis kendi çağırıyor
```

Yani `MusteriRepository`'nin `AddAsync` / `UpdateAsync` metotları **içlerinde kaydetmiyor**, servis kaydediyor. Bu tutarsız.

### Senaryo B: Repository SaveChanges İçinde, Servis de Çağırıyor

`TedarikciService.SaveAsync()` akışı:
```
await _repo.AddAsync(tedarikci)   → TedarikciRepository: _db.Add + SaveChanges ✅ KAYDEDER
await _db.SaveChangesAsync()      → Servis tekrar kaydeder — boş ama yanıltıcı
```

`TedarikciRepository.cs`:
```csharp
public async Task AddAsync(Tedarikci tedarikci)
{
    _db.Tedarikciler.Add(tedarikci);
    await _db.SaveChangesAsync(); // GERÇEKten kaydeder
}
```

Servis sonra gene `await _db.SaveChangesAsync()` çağırıyor. Birisi kaldırdığında sistem bozulur, birisi tutarsa gereksiz veritabanı roundtrip olur.

### Senaryo C: Transaction + Repository SaveChanges = Meşru Tehlike

`AlisService.AlisSilAsync()` içinde transaction açılıyor, ama `_db.SaveChangesAsync()` transaction kapanmadan önce doğrudan çağrılıyor. `AlisRepository`'deki `AddAsync` de kendi içinde `SaveChangesAsync` yapıyor. Eğer dışarıdaki transaction rollback olursa repository'nin zaten commit ettiği veri kaybolmaz — **transaction'ın etkisi yarım kalır.**

---

## 3. Interface Şişkinliği — Özlü Olmayan Sözleşmeler

### Sorun

Interface'ler hem internal implementation detayı olan metodları `public` olarak açığa çıkarıyor:

**`IStokService.cs` — Interface'de gereksiz metodlar:**
```csharp
Task<(bool basarili, string mesaj)> SaveAsync(StokHareketVM vm);           // ✅ gerekli
Task<(bool basarili, string mesaj)> SilAsync(int id);                      // ✅ gerekli
Task<(bool basarili, string mesaj)> HareketEkleAsync(StokHareketVM vm);    // ❌ SaveAsync'in iç adımı
Task<(bool basarili, string mesaj)> HareketGuncelleAsync(StokHareketVM vm);// ❌ iç adım
Task<(bool basarili, string mesaj)> HareketSilAsync(int id);               // ❌ SilAsync'in iç adımı
```

`HareketEkle/Guncelle/Sil` zaten `Save` ve `Sil` tarafından çağrılıyor. Bu metodların `public` interface'de olmasının tek etkisi, controller'ın doğrudan `HareketEkleAsync`'i çağırabilmesidir; bu da kimin neyi çağıracağını belirsizleştirir.

**`ISatisService.cs` — Benzer sorun:**
```csharp
Task<(bool basarili, string mesaj)> SaveAsync(SatisVM vm);          // ✅ gerekli
Task<(bool basarili, string mesaj)> SilAsync(int id, ...);          // ✅ gerekli
Task<(bool basarili, string mesaj)> TamIptalAsync(int satisId, ...);// ❓ SilAsync zaten bunu çağırıyor
Task<List<Urun>> GetUrunlerByIdsAsync(List<int> urunIds);           // ❌ IUrunService'e ait!
```

`GetUrunlerByIdsAsync` metodu `IUrunService`'in sorumluluğudur, `ISatisService`'e ait değil. Yerleştirme yanlış.

---

## 4. Repository Katmanının Kısmi İşlevsizliği

Bazı servislerde repository hiç kullanılmıyor, bazılarında ise repository ile service'e dağıtılmış aynı tür işlemler var.

### LookupService — Repository Yok, Doğrudan DbContext

```csharp
public class LookupService : ILookupService
{
    private readonly AppDbContext _db; // Repository yok, interface yok

    public async Task<SelectList> GetTedarikcilerAsync(int? currentSelected = null)
    {
        var tedarikciler = await _db.Tedarikciler.OrderBy(t => t.Ad).ToListAsync();
        // ...
    }
}
```

Bu serviste repository kullanılmıyor. Öte yandan `TedarikciService`'de hem `ITedarikciRepository` hem `AppDbContext` var. Tedarikçi listesi iki ayrı yerden iki ayrı yolla çekiliyor.

### StokService — İçsel Metodlar Repository'yi Atlıyor

`HareketSilAsync` içinde doğrudan `_db.StokHareketler.FirstOrDefaultAsync` çağrılıyor. `StokRepository.GetByIdAsync` var ama kullanılmıyor:

```csharp
// StokService.HareketSilAsync() — doğrudan DB
var h = await _db.StokHareketler.FirstOrDefaultAsync(x => x.Id == id);
```

Oysa repository'de hazır bir metot mevcut:
```csharp
// StokRepository — var ama kullanılmıyor
public async Task<StokHareket?> GetByIdAsync(int id)
{
    return await _db.StokHareketler.Include(s => s.Urun).FirstOrDefaultAsync(s => s.Id == id);
}
```

### DashboardRepository — Gereksiz Ayrım

`DashboardRepository` sadece 5 sorgudan ibaret. Bunların tamamı tek bir `DashboardService` içinde doğrudan `AppDbContext` ile yazılabilirdi. 5 metotluk use-case için ayrı repository sınıfı + interface yazmak açık over-engineering.

---

## 5. Tekrar Eden Hesaplama Mantığı (DRY İhlali)

### KDV Oran Okuma — 4 Farklı Yerde Aynı Kod

**`SatisService.cs` (satır 89-90):**
```csharp
var kdvStr = await _ayarService.GetAsync("VarsayilanKdv");
var varsayilanKdv = int.TryParse(kdvStr, out var v) ? v : 20;
```

**`SatisService.cs` (satır 290-291) — aynı metot içinde bile tekrar:**
```csharp
var kdvStr = await _ayarService.GetAsync("VarsayilanKdv");
var varsayilanKdv = int.TryParse(kdvStr, out var v) ? v : 20;
```

**`AlisService.cs` (satır 88-89) ve (satır 322-323)** — dördüncü kez, farklı değişken adıyla:
```csharp
var kdvStr = await _ayarService.GetAsync("VarsayilanKdv");
var varsayilanKdv = int.TryParse(kdvStr, out var vk) ? vk : 20;
```

Aynı 2 satır 4 farklı yerde yazılmış. `LookupService.GetVarsayilanKdvAsync()` ise tam olarak bu işi yapıyor ama hiç kullanılmıyor.

### Müşteri Borcu Güncelleme — 6 Farklı Yerde

`Musteri.ToplamBorc` şu noktalarda güncelleniyor:

1. `SatisService.SatisYapAsync()` → `+= satis.ToplamTutar`
2. `SatisService.UpdateSatisAsync()` → `-= eskiSatis.Veresiye.Tutar` ve `+= eskiSatis.ToplamTutar`
3. `SatisService.TamIptalAsync()` → `-= kalanBorc`
4. `SatisService.KismiIadeAsync()` → `= Math.Max(0, musteri.ToplamBorc - toplamIadeTutari)`
5. `VeresiyeService.OdemeAlAsync()` → `-= tutar`
6. `VeresiyeService.KompleKapatAsync()` → `-= buVeresiyeOdenen`

Bu 6 nokta birbirinden bağımsız güncelleme yapıyor. Herhangi birinde bir hata olursa `Musteri.ToplamBorc` gerçek değerden sapıyor.

### Stok Hareketi Yazma — Merkezi Servis Bypass Ediliyor

`StokHareket` nesnesi doğrudan `_db.StokHareketler.Add()` ile şu servislerde ekleniyor:
- `SatisService` (satış, güncelleme, iade, iptal)
- `AlisService` (alış, güncelleme, silme)
- `StokService` (manuel hareket)

Oysa `IStokService.HareketEkleAsync()` tam olarak bu iş için var. Merkezi servis bypass ediliyor.

---

## 6. Tutarsız Mimari Kararlar (Over-Engineering + Under-Engineering Bir Arada)

### Over-Engineering Örnekleri

| Öğe | Neden Fazla? |
|---|---|
| `DashboardRepository` + `IDashboardRepository` | 5 basit sorgu için tam bir repository yapısı gereksiz |
| `UrunFiyatAuditRepository` + interface | Tek metotlu repository — doğrudan servis içinde `_db.UrunFiyatAuditler.Add()` daha sade olurdu |
| `LookupService` + `ILookupService` (16 metod) | SelectList üretmek için ayrı servis + interface. Extension method ya da static helper yeterliydi |
| Her entity için `GetAll` + `GetPaged` + `GetCount` triosu | `GiderKategori`, `Tedarikci` gibi küçük tablolarda sayfalama gereksiz |

### Under-Engineering Örnekleri

| Öğe | Neden Eksik? |
|---|---|
| `VeresiyeRepository.OdemeEkleAsync` var ama kullanılmıyor | Servis doğrudan `_db.VeresiyeOdemeler.Add()` yapıyor |
| `SatisRepository.UpdateAsync`, `IadeEkleAsync`, `GetIadeByIdAsync` var ama servis `_db`'yi tercih ediyor | Repository metodlar boşta bekliyor |
| `MusteriRepository.AddAsync` / `UpdateAsync` içinde `SaveChanges` yok, servis yapıyor | Yarım kalmış pattern |
| `AlisRepository`'de silme metodu yok | `AlisService.AlisSilAsync` doğrudan `_db.Alislar.Remove()` yapıyor |

---

## 7. İkincil Sorunlar

### 7.1 UTC Dönüşüm Kodunun Dağıtık Olması

```csharp
// RaporService.cs — manuel dönüşüm
var baslangicUtc = baslangic.Kind == DateTimeKind.Unspecified
    ? DateTime.SpecifyKind(baslangic, DateTimeKind.Utc)
    : baslangic.ToUniversalTime();
```

```csharp
// KasaService.cs — farklı yöntem
DateTime? startUtc = baslangic.HasValue ? DateTime.SpecifyKind(baslangic.Value.Date, DateTimeKind.Utc) : null;
```

```csharp
// StokService.cs — private helper metot olarak çözülmüş
private static DateTime NormalizeUtc(DateTime t) => t.Kind switch { ... };
```

Her servis bu sorunu kendi yöntemiyle çözüyor. `DateTimeUtcFilter` helper sınıfı repository'de kullanılıyor ama servislerde dağınık yaklaşımlar var.

### 7.2 DTO Sınıfı Interface Dosyasında Tanımlı

`IUrunFiyatService.cs` dosyasında bir DTO sınıfı da barınıyor:

```csharp
// IUrunFiyatService.cs — interface DEĞİL, bir sınıf da içeriyor
public class UrunFiyatUpdateResult
{
    public bool IsChanged { get; set; }
    public decimal OldPrice { get; set; }
    // ...
}
```

DTO sınıfları `ViewModels/` veya ayrı bir `DTOs/` klasöründe olmalı, interface dosyasında değil.

### 7.3 Yorum Satırları ile Bitmemiş Kararlar

`SatisService.SatisYapAsync()` metodunun başında:
```csharp
// ... (existing SatisYapAsync remains mostly the same, ensuring Kind is Utc if needed)
```

`TaslakKaydetAsync()` içinde:
```csharp
// Eğer mevcut bir taslak güncelleniyorsa (veya yeni kaydediliyorsa farketmez, basitlik için yeni ekliyoruz)
// Ama mantıken aynı ekran üzerinde "Beklet"e basılıyorsa ve bir TaslakId varsa onu silip yenisini kaydedebiliriz
// ya da direkt Insert ederiz. Doküman yeni kayıt demiş.
```

Yorum kararın bile net olmadığını gösteriyor — bu bir teknik borç işareti.

### 7.4 İngilizce / Türkçe Terminoloji Karmaşası

`UrunFiyatUpdateResult` sınıfı İngilizce property kullanıyor:
```csharp
public bool IsChanged { get; set; }
public decimal OldPrice { get; set; }
public decimal NewPrice { get; set; }
public DateTime ChangedAt { get; set; }
```

Geri kalan tüm kod Türkçe: `basarili`, `mesaj`, `MusteriId`, `ToplamBorc` vb. Tek bir sınıf için alınan farklı dil kararı tutarsızlık üretiyor.

---

## Özet Tablo

| # | Sorun | Şiddet | Etkilenen Dosyalar |
|---|---|---|---|
| 1 | Service + AppDbContext çift bağımlılığı | 🔴 Kritik | Tüm servisler |
| 2 | SaveChanges kaos (double-save, missing-save) | 🔴 Kritik | Satis, Alis, Musteri, Tedarikci |
| 3 | Interface şişkinliği (iç metodlar public) | 🟠 Yüksek | IStokService, ISatisService |
| 4 | Repository'nin devre dışı kalması | 🟠 Yüksek | Tüm büyük servisler |
| 5 | Tekrar eden hesaplama mantığı | 🟡 Orta | KDV okuma, ToplamBorc, StokHareket |
| 6 | Over/Under engineering dengesizliği | 🟡 Orta | Dashboard, UrunFiyat, Lookup |
| 7 | UTC dönüşüm dağınıklığı | 🟢 Düşük | Rapor, Kasa, Stok servisleri |
| 8 | DTO interface dosyasında tanımlı | 🟢 Düşük | IUrunFiyatService.cs |
| 9 | Bitmemiş yorum/karar izleri | 🟢 Düşük | SatisService |
| 10 | Dil tutarsızlığı (TR/EN) | 🟢 Düşük | UrunFiyatService |

---

## Önerilen Temizlik Yönü

Kodu tamamen baştan yazmak gerekmez. Öncelik sırası:

1. **Tek kural seç: ya repo kullan ya `_db`, ikisini karıştırma.** En pragmatik çözüm: Repository pattern'i terk et, servisleri düz `AppDbContext` + Unit of Work ile yaz. Büyük transactionlar gerektiren iş mantığında repository sadece engel oluyor.

2. **`SaveChanges`'i tek bir yerde yönet.** Ya hep repository'de olsun ya hep serviste. Karışık olması en tehlikeli durum.

3. **Interface'leri ince tut.** Controller'dan çağrılmayacak metodları public interface'den çıkar.

4. **KDV okuma ve UTC normalize işlemlerini** `LookupService` ya da ortak bir extension üzerinden yönet.

5. **`Musteri.ToplamBorc` gibi denormalize alanları** her zaman tek bir servis metodu üzerinden güncelle; 6 noktaya dağılmış güncellemeyi kır.

---

*Bu rapor yalnızca analiz ve tespit içermektedir. Herhangi bir kaynak kodu değişikliği yapılmamıştır.*
