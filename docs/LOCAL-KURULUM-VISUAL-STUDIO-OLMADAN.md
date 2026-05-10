# CariErinc — Visual Studio Olmadan Başka Bilgisayarda Çalıştırma

Bu rehber, projeyi **Visual Studio kurulu olmayan** başka bir Windows bilgisayarda çalıştırmak için adımları açıklar.

---

## Gereksinimler (Hedef Bilgisayarda)

1. **PostgreSQL** — Veritabanı sunucusu
2. **.NET 10 Runtime** (veya **self-contained** publish ile hiç kurulum gerekmez)

---

## Yöntem 1 — En Kolay: Self-Contained Publish (Önerilen)

Bu yöntemde hedef bilgisayarda .NET kurulumu gerekmez. Tüm bağımlılıklar publish klasörüne dahil edilir.

### Adım 1 — Projeyi Yayınla (Geliştirme Bilgisayarında)

```powershell
cd c:\Users\Yunus\source\repos\CariErinc\CariErinc
dotnet publish -c Release -r win-x64 --self-contained true -o ..\publish
```

Bu komut `CariErinc\publish` klasörünü oluşturur.

### Adım 2 — Başlatma Dosyasını Ekle (Opsiyonel)

Proje kökündeki `CariErinc-Baslat.bat` dosyasını `publish` klasörüne kopyalayın. Kullanıcılar bu dosyaya çift tıklayarak uygulamayı başlatabilir.

### Adım 3 — Klasörü Kopyala

`publish` klasörünü USB, ağ paylaşımı veya başka yöntemle hedef bilgisayara kopyalayın.

### Adım 4 — Hedef Bilgisayarda PostgreSQL Kur

1. https://www.postgresql.org/download/windows/ adresinden PostgreSQL indirin
2. Kurulumda **şifre belirleyin** (örn: `sifre123`)
3. Varsayılan port: **5432**

### Adım 5 — Veritabanı Oluştur

PostgreSQL kurulduktan sonra **pgAdmin** veya **psql** ile:

```sql
CREATE DATABASE kirtasiye;
```

Veya pgAdmin'de sağ tık → Create → Database → `kirtasiye` yazın.

### Adım 6 — appsettings.json Düzenle

`publish` klasöründeki `appsettings.json` dosyasını açın. Connection string'i hedef bilgisayardaki PostgreSQL'e göre güncelleyin:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=kirtasiye;Username=postgres;Password=HEDEF_BILGISAYARDAKI_SIFRE"
  },
  "AppSettings": {
    "DukkanAdi": "Kırtasiye Dükkanı"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Adım 7 — Uygulamayı Başlat

`publish` klasöründe **CariErinc.exe** dosyasına çift tıklayın.

Veya PowerShell/CMD ile:

```powershell
cd C:\publish
.\CariErinc.exe
```

Tarayıcıda **http://localhost:5000** adresine gidin. (Port farklı olabilir, konsolda yazacaktır.)

**Varsayılan giriş:** `admin` / `admin123`

---

## Yöntem 2 — Framework-Dependent (Daha Küçük Boyut)

Hedef bilgisayarda .NET 10 Runtime kurulu olacaksa bu yöntem daha az yer kaplar.

### Adım 1 — Publish

```powershell
cd c:\Users\Yunus\source\repos\CariErinc\CariErinc
dotnet publish -c Release -o ..\publish
```

### Adım 2 — Hedef Bilgisayarda .NET Runtime Kur

1. https://dotnet.microsoft.com/download/dotnet/10.0 adresine gidin
2. **ASP.NET Core Runtime 10.0** (Windows x64) indirin ve kurun

### Adım 3 — Çalıştırma

```powershell
cd C:\publish
dotnet CariErinc.dll
```

---

## Yöntem 3 — Tek Tıkla Başlatma (Batch Dosyası)

Proje kökünde `CariErinc-Baslat.bat` dosyası bulunur. Bu dosyayı `publish` klasörüne kopyalayın. Hedef bilgisayarda kullanıcılar bu dosyaya çift tıklayarak uygulamayı başlatabilir.

---

## Ağ Üzerinden Erişim (Aynı Ağdaki Diğer Bilgisayarlar)

Uygulama varsayılan olarak sadece `localhost` üzerinden dinler. Aynı ağdaki diğer bilgisayarların erişebilmesi için:

### appsettings.json'a ekleyin veya ortam değişkeni kullanın:

**Seçenek A — Ortam değişkeni (batch dosyasında):**

```batch
set ASPNETCORE_URLS=http://0.0.0.0:5000
CariErinc.exe
```

**Seçenek B — launchSettings.json yerine komut satırı:**

```powershell
CariErinc.exe --urls "http://0.0.0.0:5000"
```

Bu durumda diğer bilgisayarlar `http://BILGISAYAR_IP:5000` ile erişebilir (örn: `http://192.168.1.100:5000`).

---

## Özet Kontrol Listesi

| Adım | Açıklama |
|------|----------|
| 1 | `dotnet publish` ile publish klasörü oluştur |
| 2 | Publish klasörünü hedef bilgisayara kopyala |
| 3 | Hedef bilgisayarda PostgreSQL kur ve `kirtasiye` veritabanı oluştur |
| 4 | `appsettings.json` içindeki connection string'i güncelle |
| 5 | `CariErinc.exe` veya `CariErinc-Baslat.bat` ile başlat |
| 6 | Tarayıcıda `http://localhost:5000` aç, `admin` / `admin123` ile giriş yap |

---

## Sorun Giderme

| Sorun | Çözüm |
|-------|-------|
| "Port zaten kullanılıyor" | `appsettings.json`'a `"Urls": "http://localhost:5001"` ekleyin veya farklı port deneyin |
| Veritabanı bağlantı hatası | PostgreSQL servisinin çalıştığından, kullanıcı adı/şifrenin doğru olduğundan emin olun |
| Sayfa açılmıyor | Windows Güvenlik Duvarı'nda 5000 portunu izin verin |
| .exe bulunamadı | Self-contained publish yaptığınızdan emin olun; `win-x64` için `CariErinc.exe` oluşur |
