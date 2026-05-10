# YedekParça Projesi — Uygulama Planı

## Bağlam

CariErinc kirtasiye yönetim sistemi, yedek parça dükkanı yönetim sistemine dönüştürülüyor.
Aynı ASP.NET Core MVC + EF Core + PostgreSQL altyapısı korunuyor, iki büyük ekleme yapılacak:

1. **Yedek parça Urun modeli genişletmesi** — araç uyumluluk bilgileri + çoklu parça kodları (OEM, Üretici, Tedarikçi, Barkod, Eski, Muadil)
2. **Tamamen izole multi-tenant mimari** — subdomain → ayrı PostgreSQL DB, merkezi tenant registry

Her tenant tamamen izole (kendi DB'si, kendi stoku, kendi kullanıcıları). Merkezi paylaşılan yapı yok.

---

## Faz 1 — Yedek Parça Model Değişiklikleri

### 1.1 Yeni Dosya: `Models/ParcaKodu.cs`

```csharp
public enum ParcaKoduTipi { OEM = 0, Uretici = 1, Tedarikci = 2, Barkod = 3, EskiKod = 4, Muadil = 5 }

public class ParcaKodu
{
    public int Id { get; set; }
    public int UrunId { get; set; }
    public Urun Urun { get; set; } = null!;
    public ParcaKoduTipi KodTipi { get; set; }
    [Required][MaxLength(100)] public string Kod { get; set; } = string.Empty;
    [MaxLength(200)] public string? Aciklama { get; set; }
    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;
}
```

### 1.2 Güncelleme: `Models/Urun.cs`

Eklenecek alanlar (mevcut alanlar, Barkod dahil, korunur):

```csharp
public enum ParcaTipi { Orjinal = 0, YanSanayi = 1, Revizyon = 2 }

// Urun sınıfına eklenecekler:
[MaxLength(100)] public string? AracMarkasi { get; set; }
[MaxLength(100)] public string? AracModeli { get; set; }
public int? ModelYiliBaslangic { get; set; }
public int? ModelYiliBitis { get; set; }
[MaxLength(50)] public string? MotorTipi { get; set; }
public ParcaTipi? ParcaTipi { get; set; }
public ICollection<ParcaKodu> ParcaKodlari { get; set; } = new List<ParcaKodu>();
```

> **Not:** `Urun.Barkod` alanı olduğu gibi kalır. ParcaKodlari tablosuna da `KodTipi=Barkod` eklenir, ikisi birlikte kullanılır.

### 1.3 Güncelleme: `Data/AppDbContext.cs`

```csharp
// DbSet ekle:
public DbSet<ParcaKodu> ParcaKodlari { get; set; }

// OnModelCreating'e ekle:
modelBuilder.Entity<ParcaKodu>()
    .HasOne(pk => pk.Urun)
    .WithMany(u => u.ParcaKodlari)
    .HasForeignKey(pk => pk.UrunId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<ParcaKodu>()
    .HasIndex(pk => pk.Kod);  // Arama performansı için

modelBuilder.Entity<ParcaKodu>()
    .HasIndex(pk => new { pk.KodTipi, pk.Kod });

// IsletmeAyar seed'indeki "Kırtasiye" değerlerini "YedekParca" ile güncelle
```

### 1.4 Migration

```bash
dotnet ef migrations add AddYedekParcaAlanlari
dotnet ef database update
```

Migration şunları yapacak:
- `Urunler` tablosuna 6 yeni sütun ekler (nullable, backward compat sorun yok)
- `ParcaKodlari` tablosunu oluşturur + FK + indexler

---

## Faz 2 — Service & ViewModel Değişiklikleri

### 2.1 Yeni Dosya: `ViewModels/ParcaKoduVM.cs`

```csharp
public class ParcaKoduVM
{
    public int Id { get; set; }
    public int UrunId { get; set; }
    [Required] public ParcaKoduTipi KodTipi { get; set; }
    [Required][MaxLength(100)] public string Kod { get; set; } = string.Empty;
    [MaxLength(200)] public string? Aciklama { get; set; }
}
```

### 2.2 Güncelleme: `ViewModels/UrunVM.cs`

Eklenecekler:
```csharp
public string? AracMarkasi { get; set; }
public string? AracModeli { get; set; }
public int? ModelYiliBaslangic { get; set; }
public int? ModelYiliBitis { get; set; }
public string? MotorTipi { get; set; }
public ParcaTipi? ParcaTipi { get; set; }
public List<ParcaKoduVM> ParcaKodlari { get; set; } = new();
```

### 2.3 Güncelleme: `ViewModels/UrunIndexVM.cs`

Eklenecekler:
```csharp
public string? AracMarkasi { get; set; }
public string? AracModeli { get; set; }
public ParcaTipi? ParcaTipi { get; set; }
public string? ParcaKoduArama { get; set; }
```

### 2.4 Güncelleme: `Services/Interfaces/IUrunService.cs`

Mevcut imzaya default parametreler eklenir (mevcut çağrı yerleri değişmez):

```csharp
Task<UrunIndexVM> GetPagedListAsync(
    int page = 1,
    string? arama = null,
    string? kategori = null,
    int? tedarikciId = null,
    string? stokDurumu = null,
    string? aracMarkasi = null,      // YENİ
    string? aracModeli = null,       // YENİ
    ParcaTipi? parcaTipi = null,     // YENİ
    string? parcaKoduArama = null);  // YENİ

// Yeni imzalar:
Task<Urun?> GetByParcaKoduAsync(string kod);
Task<List<ParcaKodu>> GetParcaKodlariAsync(int urunId);
Task<ServiceResult> ParcaKoduEkleAsync(int urunId, ParcaKoduVM vm);
Task<ServiceResult> ParcaKoduGuncelleAsync(int kodId, ParcaKoduVM vm);
Task<ServiceResult> ParcaKoduSilAsync(int kodId);
```

### 2.5 Güncelleme: `Services/UrunService.cs`

- `GetPagedListAsync`: Yeni filtreler için ILike sorguları + ParcaKodlari JOIN

```csharp
if (!string.IsNullOrWhiteSpace(parcaKoduArama))
    query = query.Where(u => u.ParcaKodlari.Any(pk =>
        EF.Functions.ILike(pk.Kod, $"%{parcaKoduArama}%")));

if (!string.IsNullOrWhiteSpace(aracMarkasi))
    query = query.Where(u => EF.Functions.ILike(u.AracMarkasi!, $"%{aracMarkasi}%"));

if (parcaTipi.HasValue)
    query = query.Where(u => u.ParcaTipi == parcaTipi.Value);
```

- `SearchAsync` (POS arama): `u.ParcaKodlari.Any(pk => pk.Kod.ToLower().Contains(term))` ekle
- `GetByIdAsync`: `.Include(u => u.ParcaKodlari)` ekle
- `SaveAsync`: Yeni araç alanlarını ve ParcaKodlari'nı kaydet
- `GetByParcaKoduAsync`: Yeni metot — herhangi bir kodla ürün bul

---

## Faz 3 — Controller & View Değişiklikleri

### 3.1 Güncelleme: `Controllers/UrunController.cs`

- `Index` action: `aracMarkasi`, `aracModeli`, `parcaTipi`, `parcaKoduArama` parametreleri
- `Form/Save`: Yeni VM alanlarını işle
- Yeni AJAX endpoint'ler (ParcaKodu CRUD):
  - `GET  /Urun/ParcaKodlari/{urunId}` → JSON
  - `POST /Urun/ParcaKoduEkle`
  - `POST /Urun/ParcaKoduGuncelle`
  - `POST /Urun/ParcaKoduSil`

### 3.2 Güncelleme: `Views/Urun/Index.cshtml`

- Filtre formuna: ParcaKodu arama, Araç Markası, Araç Modeli, Parça Tipi dropdown
- Tabloya: "Araç Uyumluluğu" ve "Tip" sütunları

### 3.3 Güncelleme: `Views/Urun/Form.cshtml`

- "Araç Uyumluluk Bilgileri" section: AracMarkasi, AracModeli, ModelYili aralığı, MotorTipi, ParcaTipi dropdown
- "Parça Kodları" section: Dinamik satır ekle/sil (JavaScript), KodTipi dropdown + Kod input + Açıklama

### 3.4 Güncelleme: `Views/Urun/Detail.cshtml`

- Araç uyumluluk bilgileri
- Parça kodları tablosu (tip badge + kod değeri + açıklama)

---

## Faz 4 — Multi-Tenant Altyapısı

### 4.1 Yeni Dosya: `Models/TenantKayit.cs`

```csharp
public class TenantKayit
{
    public int Id { get; set; }
    [Required][MaxLength(100)] public string Subdomain { get; set; } = string.Empty;
    [Required][MaxLength(200)] public string DukkanAdi { get; set; } = string.Empty;
    [Required] public string ConnectionString { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;
    public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;
}
```

### 4.2 Yeni Dosya: `Data/AdminDbContext.cs`

```csharp
public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }
    public DbSet<TenantKayit> TenantKayitlar { get; set; }
    protected override void OnModelCreating(ModelBuilder m) =>
        m.Entity<TenantKayit>().HasIndex(t => t.Subdomain).IsUnique();
}
```

### 4.3 Yeni Dosya: `Data/TenantDbContextFactory.cs`

- `IHttpContextAccessor`'dan `context.Items["TenantInfo"]` okur
- `TenantInfo` varsa tenant'ın connection string'ini kullanır
- Yoksa (MultiTenant:Enabled=false) `DefaultConnection`'a fallback

```csharp
public AppDbContext CreateDbContext()
{
    var tenantInfo = _httpContextAccessor.HttpContext?.Items["TenantInfo"] as TenantInfo;
    var connStr = tenantInfo?.ConnectionString
        ?? _config.GetConnectionString("DefaultConnection")!;
    
    var opts = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connStr).Build();
    return new AppDbContext(opts);
}
```

### 4.4 Yeni Dosya: `Services/TenantInfo.cs` + `Services/Interfaces/ITenantService.cs`

```csharp
public class TenantInfo
{
    public int Id { get; set; }
    public string Subdomain { get; set; } = string.Empty;
    public string DukkanAdi { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}
```

### 4.5 Yeni Dosya: `Middleware/SubdomainMiddleware.cs`

Akış:
1. `MultiTenant:Enabled=false` → bypass et, `_next` çağır
2. `Request.Host.Host` → "istanbul.app.com" → subdomain = "istanbul"
3. AdminDbContext'ten (IMemoryCache TTL: 5 dk) TenantKayit sorgula
4. Bulunamazsa 404 dön
5. `context.Items["TenantInfo"] = tenantInfo` set et
6. `_next(context)` çağır

> **Middleware sırası (Program.cs):** `UseMiddleware<SubdomainMiddleware>` → `UseRouting` → `UseAuthentication` → `UseAuthorization` → `UseMiddleware<YetkiMiddleware>`

### 4.6 Güncelleme: `appsettings.json`

```json
"ConnectionStrings": {
    "DefaultConnection": "...(mevcut)...",
    "AdminConnection": "Host=localhost;Database=cariErinc_admin;Username=postgres;Password=..."
},
"MultiTenant": {
    "Enabled": false,
    "BaseDomain": "app.com"
}
```

> `appsettings.Development.json`'da `"Enabled": false` — local geliştirme subdomain gerektirmez.
> Production `appsettings.json`'da `"Enabled": true` yapılır.

### 4.7 Güncelleme: `Program.cs`

```csharp
// 1. AdminDbContext kaydı
builder.Services.AddDbContext<AdminDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("AdminConnection")));

// 2. AppDbContext artık factory'den geliyor
builder.Services.AddScoped<TenantDbContextFactory>();
builder.Services.AddScoped<AppDbContext>(sp =>
    sp.GetRequiredService<TenantDbContextFactory>().CreateDbContext());

// 3. Startup migration bloğu güncellenir:
//    - AdminDb migrate
//    - Aktif tenant DB'lerini migrate (her tenant için DbContext oluştur → MigrateAsync)
```

### 4.8 Güncelleme: `Services/YetkiCacheService.cs`

Cache key'lerine tenant subdomain prefix eklenir (farklı tenant'ların yetki cache'i karışmasın):

```csharp
var tenant = _httpContextAccessor.HttpContext?.Items["TenantInfo"] is TenantInfo t ? t.Subdomain : "default";
var cacheKey = $"{tenant}_yetki_{string.Join("_", ids.OrderBy(x => x))}";
```

Aynı fix `Services/AyarService.cs`'e de uygulanır.

---

## Kritik Dosya Listesi

| Dosya | İşlem |
|-------|-------|
| `Models/ParcaKodu.cs` | YENİ |
| `Models/TenantKayit.cs` | YENİ |
| `Data/AdminDbContext.cs` | YENİ |
| `Data/TenantDbContextFactory.cs` | YENİ |
| `Middleware/SubdomainMiddleware.cs` | YENİ |
| `Services/TenantInfo.cs` | YENİ |
| `Services/Interfaces/ITenantService.cs` | YENİ |
| `ViewModels/ParcaKoduVM.cs` | YENİ |
| `Models/Urun.cs` | GÜNCELLE |
| `Data/AppDbContext.cs` | GÜNCELLE |
| `Services/Interfaces/IUrunService.cs` | GÜNCELLE |
| `Services/UrunService.cs` | GÜNCELLE |
| `Services/YetkiCacheService.cs` | GÜNCELLE |
| `Services/AyarService.cs` | GÜNCELLE |
| `Controllers/UrunController.cs` | GÜNCELLE |
| `ViewModels/UrunVM.cs` | GÜNCELLE |
| `ViewModels/UrunIndexVM.cs` | GÜNCELLE |
| `Views/Urun/Index.cshtml` | GÜNCELLE |
| `Views/Urun/Form.cshtml` | GÜNCELLE |
| `Views/Urun/Detail.cshtml` | GÜNCELLE |
| `Program.cs` | GÜNCELLE |
| `appsettings.json` | GÜNCELLE |

---

## Uygulama Sırası

```
Faz 1: Model + DB
  1. ParcaKodu.cs oluştur
  2. Urun.cs güncelle
  3. AppDbContext.cs güncelle (DbSet + OnModelCreating)
  4. Migration oluştur ve uygula

Faz 2: Service + ViewModel
  5. ParcaKoduVM.cs oluştur
  6. UrunVM.cs + UrunIndexVM.cs güncelle
  7. IUrunService.cs güncelle
  8. UrunService.cs güncelle

Faz 3: Controller + View
  9. UrunController.cs güncelle
  10. Views/Urun/*.cshtml güncelle

Faz 4: Multi-Tenant
  11. TenantKayit.cs + AdminDbContext.cs oluştur
  12. TenantDbContextFactory.cs oluştur
  13. SubdomainMiddleware.cs oluştur
  14. ITenantService.cs + TenantInfo.cs oluştur
  15. appsettings.json güncelle
  16. Program.cs güncelle
  17. YetkiCacheService + AyarService cache key fix
  18. AdminDbContext migration oluştur
```

---

## Doğrulama

1. **Parça kodu arama:** Yeni parça oluştur, OEM kodu ekle, hem ad hem OEM koduyla ara — her ikisi de aynı parçayı bulsun
2. **Araç filtresi:** Ford Focus parçası ekle, "Ford" + "Focus" filtresiyle listele — sadece o parça görünsün
3. **Muadil arama:** POS ekranında muadil koduyla ara — ilgili parça gelsin
4. **Multi-tenant:** appsettings.Development.json'da `Enabled:false` → localhost'ta normal çalışsın; `Enabled:true` + hosts dosyası subdomain → doğru DB'ye bağlansın
5. **Tenant izolasyonu:** İki farklı tenant DB'si oluştur, birinde eklenen parça diğerinde görünmesin
