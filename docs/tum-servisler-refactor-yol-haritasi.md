# Tüm Servisler İçin Refactor Yol Haritası

Bu doküman, servis katmanını sade, tutarlı, DRY ve kolay yönetilebilir hale getirmek için hazırlanmıştır.

Ana amaç:
- Tek iş = tek sorumluluk
- Ortak kurallar = tek merkez
- Hesaplama = saf ve test edilebilir
- Yan etkiler = kontrollü ve tutarlı
- Servisler = birbirini kopyalamayan, birbirine bağımlılığı düşük bileşenler

## 1. Genel Kural Seti

Tüm servisler için geçerli temel kurallar:

- Bir servis aynı anda hem hesaplama hem iş akışı hem de veri senkronizasyonu yapmamalı.
- Kasa, stok, borç, fiyat, audit gibi çapraz etkiler tek standarttan yönetilmeli.
- Barkod, KDV, indirim, toplam ve tarih hesapları tek helper / calculator üzerinden geçmeli.
- `SaveChangesAsync` çağrısı tek yerde ve mümkün olduğunca tek commit noktasında olmalı.
- Exception ile `ServiceResult` karışık kullanılmamalı.
- Controller'lar mümkün olduğunca sadece request/response yapmalı.
- Servis içinde duplicate query ve duplicate business rule olmamalı.

## 2. Ortak Mimari Çekirdek

Tüm servislerin bağlanacağı ortak çekirdek mantığı şu olmalı:

- `KdvHesaplayici`
- `BorcHesaplayici`
- `StokHareketYoneticisi`
- `KasaHareketYoneticisi`
- `AuditYoneticisi`
- `FiyatDegisimYoneticisi`

Bu bileşenlerin amacı yeni servis yaratmak değil, ortak iş kurallarını tekleştirmektir.

Önemli prensip:
- `AlisService`, `SatisService`, `VeresiyeService`, `StokService`, `KasaService` ve `UrunFiyatService` aynı kuralları ayrı ayrı yazmamalı.

## 3. Servis Bazlı Refactor Planı

### 3.1 `AlisService`

Mevcut durum:
- alış oluşturma
- alış güncelleme
- alış silme
- ödeme alma
- stok etkisi
- kasa etkisi
- borç etkisi
- fiyat güncelleme
- audit log

Yapılacaklar:
- `AlisYapAsync`, `AlisGuncelleAsync`, `AlisSilAsync` akışları tek bir use case yapısına indirilmeli.
- Barkod güncelleme alış akışının içine gömülmemeli, tek bir ortak ürün güncelleme yardımcı fonksiyonuna taşınmalı.
- KDV hesaplama tek helper üzerinden yapılmalı.
- `NetAlisBirimMaliyetKdvsiz` gibi hesaplar ayrı calculator katmanına taşınmalı.
- Stok, kasa, borç ve fiyat değişimleri tek transaction içinde ve tek akıştan yönetilmeli.
- `OdemeYapAsync` ile alış oluşturma arasında ortak borç ve kasa mantığı tekrar yazılmamalı.

Risk:
- Alış sırasında bir işlem başarılı, diğeri başarısız kalırsa veri tutarsızlığı oluşur.

### 3.2 `SatisService`

Mevcut durum:
- satış yapma
- satış güncelleme
- satış iptal
- kısmi iade
- taslak kaydetme
- taslak yükleme
- taslak silme
- stok çıkışı
- kasa hareketi
- veresiye oluşturma
- audit log

Yapılacaklar:
- `SatisYapAsync`, `UpdateSatisAsync`, `TamIptalAsync`, `KismiIadeAsync` ayrı use case başlıkları olarak korunmalı ama iç hesaplar bölünmeli.
- Satır hesaplama sadece `SatisTutarHesaplayici` üzerinden geçmeli.
- Kasa ve stok yan etkileri tek ortak akışla uygulanmalı.
- Taslak yönetimi ayrı bir iç modül gibi ele alınmalı.
- Veresiye oluşturma, satışın içine gömülü dağınık bir blok olmaktan çıkmalı.
- `SaveAsync` metodunun seçme mantığı sade kalmalı.

Risk:
- Satış güncelleme sırasında eski stok/kasa geri alma ile yeni kaydı uygulama aynı mantıkta toparlanmazsa miktar kayması olur.

### 3.3 `StokService`

Mevcut durum:
- stok hareketi ekleme
- stok hareketi silme
- stok hareketi güncelleme
- stok adedini hareketlerden yeniden hesaplama
- ürün üzerinde doğrudan giriş/çıkış

Yapılacaklar:
- Stok hareketleri tek kaynak olmalı.
- `HesaplaStokSonDurumAsync` benzeri hesaplar ayrı bir internal helper olarak korunabilir ama dışarıda tekrarlanmamalı.
- Ürün üzerinde `StokGiris` / `StokCikis` ve ayrıca stok hareketi ekleme akışı aynı işin iki farklı yolu gibi görünmemeli.
- Yeni hedef yapı şu olmalı:
  - ya hareket kaydı yazılır ve stok yeniden hesaplanır
  - ya da tek bir ortak uygulama metodu ikisini birden yönetir
- Güncelleme ve silme akışları transaction disiplini açısından sadeleştirilmeli.

Risk:
- Şu an stok hesabı hem hareket bazlı hem ürün bazlı ilerlediği için çift doğruluk kaynağı oluşabiliyor.

### 3.4 `KasaService`

Mevcut durum:
- kasa listeleme
- kasa kaydı oluşturma
- kasa silme
- gelir ekleme
- gider çıkarma

Yapılacaklar:
- Kasa servisinin rolü netleştirilmeli: “kasa hareket kaynağı”.
- `KasaGelirEkle` ve `KasaGiderCik` gibi metotlar tek bir `KasaHareketOlustur` mantığına indirgenebilir.
- Sistem kaynaklı hareketler ile manuel hareketler kategorik olarak ayrılmalı.
- “Sistem kaydı silinemez” kuralı tek merkezi enum/kural setinden yönetilmeli.

Risk:
- Manuel kasa hareketleri ile sistem hareketleri aynı formatta tutulursa raporlar kirlenir.

### 3.5 `UrunService`

Mevcut durum:
- ürün listeleme
- ürün kaydetme
- ürün silme
- arama
- barkod sorgu
- son alış bilgileri
- son stok hareketleri

Yapılacaklar:
- `SaveAsync` sadece ürün entity yaşam döngüsünden sorumlu olmalı.
- Barkod uniqueness kontrolü tek yerde olmalı.
- Kategori, tedarikçi, stok ve fiyat validasyonları ayrı helper bloklarına bölünmeli.
- Arama ve listeleme metotları ortak query builder kullanmalı.
- `GetSonAlisBilgileriAsync` gibi “rapor amaçlı veri zenginleştirme” metotları mümkünse ayrı query service mantığına taşınmalı.

Risk:
- Ürün servisinde hem CRUD hem arama hem de satış/alış için veri zenginleştirme bir arada duruyor; bu servis büyüdükçe tek başına her şeyi taşıyan bir çöp kutusuna dönüşebilir.

### 3.6 `UrunFiyatService`

Mevcut durum:
- alış fiyatı güncelleme
- fiyat geçmişi
- son fiyat değişimi

Yapılacaklar:
- Fiyat değişimi tek giriş noktasından yapılmalı.
- Audit kaydı ve ürün güncellemesi aynı iş akışında olmalı.
- Bu servis ürün güncelleme servisinin yan kolu gibi değil, fiyat değişiminin sahibi gibi davranmalı.
- Fiyat değişimi ile stok/alış akışları arasındaki sınır netleştirilmeli.

Risk:
- Şu an fiyat geçmişi ayrı, ürün güncelleme ayrı hareket edebiliyor; bu da “geçmiş var ama ana kayıt yok” durumuna neden olabilir.

### 3.7 `VeresiyeService`

Mevcut durum:
- veresiye oluşturma
- veresiye güncelleme
- veresiye silme
- ödeme alma
- toplu kapatma

Yapılacaklar:
- Borç güncelleme mantığı tek helper üzerinden geçmeli.
- Kasa gelir yazımı tek merkezden olmalı.
- `OdemeAlAsync` ve `KompleKapatAsync` aynı ödeme kurallarını tekrar etmemeli.
- Borç durumu hesapları ile ödeme dağıtımı ayrı fonksiyonlarda tutulmalı.

Risk:
- Kısmi ödeme, tam ödeme ve toplu kapatma aynı hesap mantığını farklı şekillerde tekrar ettiği için tutarsızlık riski yüksek.

### 3.8 `AyarService`

Mevcut durum:
- ayar okuma/yazma
- cache yönetimi
- KDV oranları listesi
- varsayılan KDV

Yapılacaklar:
- Ayar servisinin cache mantığı sadeleştirilmeli.
- `GetKdvOranlariListeAsync` ve `GetVarsayilanKdvOraniAsync` aynı kaynak mantığını kullanmalı.
- Sabit fallback listeler mümkünse config'e bağlanmalı.

Risk:
- Ayar verisi hem cache hem DB hem fallback listeden beslendiği için hangi değer final geldiği belirsizleşebilir.

### 3.9 `LookupService`

Mevcut durum:
- select list üretimi
- KDV oranları
- tedarikçi listesi
- ürün kategorileri
- müşteri listesi

Yapılacaklar:
- Lookup servisi sadece UI seçenekleri üretmeli.
- İş kuralı taşıyan taraf olmamalı.
- `AyarService` ile tekrar eden KDV mantığı tekilleştirilmeli.

Risk:
- Lookup içinde iş kuralı birikirse servis “UI helper” olmaktan çıkıp ikinci bir domain servisine dönüşür.

### 3.10 `MusteriService`

Mevcut durum:
- müşteri CRUD
- detay view model
- arama

Yapılacaklar:
- borç hesapları müşteri servisinden ayrılmalı.
- müşteri yaşam döngüsü ile finansal bakiye güncellemeleri karıştırılmamalı.
- detay modeli oluşturma ayrı query helper’a taşınabilir.

Risk:
- Müşteri objesi hem kimlik verisi hem finansal özet taşıyorsa servis şişer.

### 3.11 `TedarikciService`

Mevcut durum:
- tedarikçi CRUD
- detay view model

Yapılacaklar:
- müşteri servisindeki temizlik burada da uygulanmalı.
- borç bilgisi varsa tek helper üzerinden yönetilmeli.

Risk:
- Müşteri ve tedarikçi servisleri aynı pattern'i farklı şekilde uygularsa bakım maliyeti artar.

### 3.12 `KasaService`, `StokService`, `VeresiyeService` Ortaklığı

Bu üç servis için ortak ilkeler:

- Hepsi kendi alanının sahibi olmalı.
- Başka alanın iş mantığını tekrar yazmamalı.
- Yan etki üretirken aynı helper sınıfları kullanmalı.
- Bir işin sonucu olarak ortaya çıkan kasa/stok/borç güncellemesi tek akıştan çağrılmalı.

Öneri:
- `IIslemYoneticisi` gibi üst seviye bir orchestrator düşün.
- Bu orchestrator doğrudan controller'dan değil, yalnızca ilgili use case servisi içinden çağrılsın.

### 3.13 `AuditLogService`

Mevcut durum:
- log ekleme
- log listeleme
- sayfalama

Yapılacaklar:
- Log formatı standardize edilmeli.
- Kritik işlemlerde audit yazımı zorunlu davranış olmalı.
- Audit log başarısız olursa ne yapılacağı net tanımlanmalı.

Risk:
- Audit mantığı bir yerde unutulursa sistemin izlenebilirliği bozulur.

### 3.14 `DashboardService`

Mevcut durum:
- dashboard verileri toplama

Yapılacaklar:
- Dashboard sorguları ortak rapor/query yapıları üzerinden beslenmeli.
- Burada business rule olmamalı.

### 3.15 `RaporService`

Mevcut durum:
- günlük satış
- aylık rapor
- stok uyarı
- veresiye raporu
- kar/zarar

Yapılacaklar:
- Rapor servisi mümkün olduğunca read-only kalmalı.
- Ortak tarih filtreleme ve para hesaplama mantığı helper'a taşınmalı.
- Kar/zarar hesapları tek bir maliyet kuralı üzerinden ilerlemeli.
- `DateTime.Today` yerine mümkün olan her yerde net zaman standardı kullanılmalı.

Risk:
- Rapor servisi şu an hem kasa, hem satış, hem stok, hem veresiye verisini birleştiriyor; bu yüzden veri modelindeki küçük değişiklikler raporları kolayca bozabilir.

### 3.16 `FaturaAnalizService`

Mevcut durum:
- dosya analizi
- ürün eşleme

Yapılacaklar:
- analiz, eşleme ve tahmin adımları ayrı metodlara bölünmeli.
- dosya okuma ile domain eşleme karıştırılmamalı.

Risk:
- Dosya analiz servisleri genelde bir noktadan sonra her şeyi bilmeye başlar; bu servis de o yola girmemeli.

### 3.17 `YetkiCacheService`

Mevcut durum:
- yetki cache
- sidebar cache
- invalidate

Yapılacaklar:
- cache key stratejisi sadeleşmeli.
- invalidate davranışı daha hedefli olmalı.
- rol bazlı yetki okuma ve sidebar link okuma ortak query temeline alınmalı.

Risk:
- Cache temizleme şu an fazla kaba çalışıyor, bu da performans ve öngörülebilirlik açısından ideal değil.

### 3.18 `GiderKategoriService`

Mevcut durum:
- kategori CRUD

Yapılacaklar:
- sistem kategorileri ile kullanıcı kategorileri ayrılmalı.
- silme kuralı tek yerde tanımlanmalı.

## 4. Öncelik Sırası

Bu refactor tek seferde değil, şu sırayla yapılmalı:

1. `AlisService` sadeleştir.
2. `SatisService` sadeleştir.
3. `StokService` ile `KasaService` ortak akışlarını standartlaştır.
4. `UrunFiyatService` ile `UrunService` sınırını netleştir.
5. `VeresiyeService` borç ve ödeme akışını merkezileştir.
6. `RaporService` ve `DashboardService` için ortak read-only query katmanı oluştur.
7. `AyarService` ve `LookupService` içindeki tekrarları temizle.
8. `AuditLogService` ve `YetkiCacheService` gibi altyapı servislerini standardize et.

## 5. Uygulama Stratejisi

İlk aşamada yeni sınıf yağdırmak yerine mevcut servislerin içi toparlanmalı.

Şu yöntemle ilerlemek en güvenlisi:

- Önce büyük metotları küçük private metodlara böl.
- Sonra tekrar eden hesapları helper sınıfına taşı.
- Ardından yan etki adımlarını ortak orchestrator mantığına bağla.
- En son ihtiyaç varsa yeni servis çıkar.

Bu yaklaşım:
- riski azaltır
- davranışı bozmadan refactor yapmayı kolaylaştırır
- test sürecini sadeleştirir

## 6. Kod Kalitesi Kontrol Listesi

Her servis için şu sorular sorulmalı:

- Bu class tek bir ana iş mi yapıyor?
- Aynı iş kuralı başka serviste tekrar edilmiş mi?
- Bu metodun içinde hesaplama ve persistence karışmış mı?
- Bir transaction içinde yarım kalabilecek yan etkiler var mı?
- Barkod, KDV, stok, kasa, borç ve audit kuralları tek merkezden mi geliyor?
- Bu kodu yarın tek başına okuyunca yeniden anlayabilir miyim?

## 7. Son İlke

Bu projede öncelik gösterişli mimari değil.
Öncelik:
- tutarlılık
- sadeleştirme
- DRY
- test edilebilirlik
- tek geliştirici için sürdürülebilir bakım

Bu doküman, servis katmanını o hedefe taşımak için canlı yol haritasıdır.

