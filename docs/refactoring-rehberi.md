# Servis Katmanı Refactoring ve Merkeziyetleştirme Rehberi

> [!IMPORTANT]
> Bu doküman, tek geliştirici olarak kod tekrarını (DRY) engellemek ve **"Veri Tutarsızlığı" (Data Inconsistency)** sorunlarının önüne geçmek için tasarlandı. Amacımız, sistemin dört bir yanına (Alış, Satış, Veresiye) dağılmış kasa ve stok ekleme/çıkarma kodlarını tek bir merkeze çekmektir. 

## 1. Veri Tutarlılığını "Merkeziyetleştirme" İle Sağlamak (Ana Hedef)

Şu anda kasa, stok ve cari hareketleri `AlisService`, `SatisService` ve `VeresiyeService` içinde ayrı ayrı tekrarlanıyor. Bu dağınıklık büyük bir risktir. Örneğin; Kasa mantığında ufacık bir kural değişikliği yapmanız gerektiğinde bunu 4-5 farklı dosyada unutmadan yapmanız gerekir. Unutulan tek bir dosya **"Kasada tutarsızlığa"** yol açar.

**Sistemsel Koruma (Ne Yapılmalı?):**
Eğer Kasaya veya Stoğa bir müdahale olacaksa, bu işlemi hiçbir servis kendi başına (örneğin doğrudan `_db.KasaHareketler.Add(...)` yazarak) yapmamalıdır. Herkes yetkili olduğu işi tek bir "Ortak Metot / Servis" üzerinden halletmelidir. (Single Point of Truth / Tek Doğru Kaynağı).

## 2. Merkezi Servislerin ve Kuralların Belirlenmesi

Servis klasörü tarandığında en sık tekrarlanan 3 ana merkez tespit edilmiştir. Bu operasyonları yönetecek merkezi metotlar şöyledir:

### A. Kasa İşlemleri Merkezi (KasaService.cs Genişletilmesi)
Şu an `VeresiyeService`, `SatisService` ve `AlisService` kendi içlerinde doğrudan `_db.KasaHareketler.Add(...)` çağırıyor. Bunu engellemek için mevcut `KasaService` içinde standart metotlar olmalıdır:

```csharp
// KasaService.cs içerisine eklenecek merkezi metotlar:
public void KasaGelirEkle(decimal tutar, string kategori, string islemAciklamasi) 
{
    _db.KasaHareketler.Add(new KasaHareket {
        HareketTipi = KasaHareketTipi.Gelir,
        Tutar = tutar,
        Kategori = kategori,
        Aciklama = islemAciklamasi,
        Tarih = DateTime.UtcNow
    });
}

public void KasaGiderCik(decimal tutar, string kategori, string islemAciklamasi) { ... }
```
**Kural:** Bundan sonra hiçbir servis kendi içinde `new KasaHareket()` yazamaz. Herkes paramatreleri verip `KasaService.KasaGelirEkle()` metodunu çağıracaktır.

### B. Stok İşlemleri Merkezi (StokService.cs)
`SatisService` ürünü satarken `urun.StokAdedi -= miktar` diyor, `AlisService` ürünü iade ederken benzer şeyleri yapıyor. Herkes kendi bildiği gibi `_db.StokHareketler.Add` nesnesi atıyor.

```csharp
// StokService.cs içerisine eklenecek:
public void StokCikisYap(Urun urun, int miktar, string islemAciklamasi, HareketTipi tip)
{
    if (urun.StokAdedi < miktar) throw new Exception($"'{urun.Ad}' Yetersiz Stok!");
    urun.StokAdedi -= miktar;
    urun.GuncellenmeTarihi = DateTime.UtcNow;
    
    _db.StokHareketler.Add(new StokHareket { 
        UrunId = urun.Id, Miktar = miktar, Aciklama = islemAciklamasi, HareketTipi = tip, Tarih = DateTime.UtcNow
    });
}
// Satış servisi sadece bu fonksiyonu çağırıp geçecek.
```

### C. Cari (Borç/Alacak) İşlemler Merkezi
Satışta `BorcHelper.Guncelle(musteri)` kullanılmış (Bu çok doğru!). Ancak `AlisService` içinde tedarikçi ucu açık bırakılarak doğrudan `tedarikci.ToplamBorc += tutar;` şeklinde yönetilmiş.
**Kural:** Tedarikçi ve Müşteri borç-alacak hesaplamaları dağınık olamaz. Mevcut `BorcHelper` gibi bir dosya içinde standartlaştırılıp (Örn: `TedarikciHelper.BorcEkle`) tek bir merkezden yürütülecek.

## 3. Servislerin Spagettiden Kurtarılması (Refactoring Aşaması)

Altyapı merkezileştiğinde servislerde devrim niteliğinde bir düşüş olacak.

**Mevcut Dağınık Durum:**
```csharp
// SatisService.cs İçi 
urun.StokAdedi -= miktar;
_db.StokHareketler.Add(new StokHareket { ... });

if (pesin) {
    _db.KasaHareketler.Add(new KasaHareket { Tutar = x, Tip = KasaHareketTipi.Gelir ... });
}
// Bu kod blokları satışta, taslak onamada ve iptalde "kopyala-yapıştır" serpilmiş durumda.
```

**Merkezi (Hedeflenen) Durum:**
```csharp
// SatisService.cs İçi - Sadece ilgili servisi çağır!
_stokService.StokCikisYap(urun, miktar, $"Satış #{satis.Id}", HareketTipi.Cikis);

if (pesin) {
    _kasaService.KasaGelirEkle(tutar, "Satış", $"Peşin Satış #{satis.Id}");
} else {
    BorcHelper.MusteriBorclandir(musteri, tutar);
}
```

## 4. Adım Adım İlerleme Planı (Yol Haritası)

> [!CAUTION]
> Tüm projeyi tek gecede değiştirmeye kalkışmamalısınız! "Çalışan koda dokunulmaz" ilkesi ile kademeli, güvenli bir geçiş (refactoring) yapılmalıdır.

*   **1. Aşama (Merkezlerin Hazırlanması):** Önce `KasaService`, `StokService` ve Cari/Helper dosyaları içine bu ortak `Add`/`CikisYap` metotlarını yazın. Sistemin geri kalanına (Satış, Alış) henüz dokunmayın.
*   **2. Aşama (Kolay Hedef - Alış Servisi):** Hata riski satıştan nispeten daha az olan `AlisService.cs` içindeki tekrarlanan `_db.StokHareketler.Add` ve `_db.KasaHareketler.Add` kod bloklarını silin. Yerine oluşturduğunuz ortak metotları çağırın ve alış modülünü local'de test edin.
*   **3. Aşama (Orta Hedef - Veresiye Servisi):** `VeresiyeService.cs` içindeki tahsilat süreçlerinde yer alan Kasa gelir/gider ekleme kodlarını bu merkezden çağıracak şekilde tıraşlayın.
*   **4. Aşama (Final - Satış Servisi):** En karmaşık kod olan (iptal, iade, güncelleme vb.) `SatisService.cs` dosyasını yeni yapıya adapte edin. Dağınık `if-else` kasa kayıtlarının tek satıra indiğini ve iade metodunun yarı yarıya kısaldığını göreceksiniz.

Bu modeli uyguladığınızda, sistemdeki "Veri Tutarlılığı" artık ağır kilit mekanizmalarıyla değil, **"Bütün yollar aynı kapıya çıkar (Tek Merkezden Yönetim)"** felsefesiyle doğal yoldan kusursuz hale gelmiş olacaktır.
