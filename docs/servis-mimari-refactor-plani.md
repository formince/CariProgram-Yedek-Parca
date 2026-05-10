# Servis Mimari Refactor Planı

Bu doküman, cari ve stok uygulamasındaki servis katmanını sadeleştirmek için hazırlanmıştır.

Ana hedef:
- Bir use case tek yerde yönetilsin.
- Ortak işlemler tek merkezden çalışsın.
- Kasa, stok, borç, fiyat ve audit davranışları tutarlı olsun.
- Aynı iş kuralı farklı servislerde tekrar yazılmasın.

## 1. Temel Problem

Mevcut yapıda bazı servisler birden fazla sorumluluk taşıyor.

Örnek:
- `AlisService` içinde hem alış oluşturma var, hem stok güncelleme var, hem kasa etkisi var, hem ürün maliyet güncelleme var, hem de audit log var.
- `SatisService` içinde satış, stok çıkışı, kasa hareketi, veresiye, iade, taslak yönetimi aynı sınıfta toplanmış durumda.
- `StokService` hem hareket kaydı tutuyor hem de ürünün stok adedini hareketlerden yeniden hesaplıyor.
- `UrunFiyatService` ürün fiyat geçmişini yazıyor ama çağrıldığı yerlerde tek transaction disiplinine bağlı değil.

Bu yapı kısa vadede hızlı görünür, ama uzun vadede şu sorunları üretir:
- Bir bug’ın kaynağı kolay bulunmaz.
- Aynı iş kuralı birden fazla yerde farklı uygulanır.
- Transaction sınırları belirsizleşir.
- Test yazmak zorlaşır.
- Bir küçük değişiklik başka alanları bozabilir.

## 2. Hedef Mimari

Servis katmanı şu prensiple sadeleşmeli:

> Bir servis bir use case’i yönetir, hesaplama ve yan etki adımlarını ise küçük, net yardımcı metodlara veya ayrı küçük servislere böler.

Bu proje için en doğru yaklaşım:
- Repository katmanını zorla büyütmemek.
- SQL erişimini EF Core ile doğrudan korumak.
- Ama servisleri “çok iş yapan class” olmaktan çıkarmak.

## 3. Ortak İşlem Merkezi

Kasa, stok, borç, fiyat ve audit gibi çapraz etkiler için tek bir uygulama çekirdeği tanımlanmalı.

Önerilen yaklaşım:
- `DomainOperationContext` veya benzeri bir işlem bağlamı oluştur.
- Alış, satış, iade, ödeme gibi işlemler bu bağlamı kullanarak ilerlesin.
- Her use case, aynı ortak yardımcıları çağırsın.

Bu ortak merkezde yer alması gereken işler:
- stok giriş/çıkış
- kasa gelir/gider kaydı
- borç güncelleme
- ürün maliyet/fiyat güncelleme
- audit log
- taslak temizleme

Yani mantık şu olmalı:
- “İşlem” tek yerde başlar.
- “Yan etkiler” aynı çatı altında yürür.
- Aynı iş başka servislerde yeniden yazılmaz.

## 4. Servisleri Nasıl Bölmeliyiz

### 4.1 `AlisService`

Sadece alış use case’ini yönetmeli.

İçerik:
- alış validasyonu
- alış başlatma
- satırları hazırlama
- toplam hesaplama
- ortak operasyonları çağırma
- commit/rollback

İçinde olmaması gerekenler:
- maliyet hesaplama detayları
- stok algoritması
- kasa formatlama
- barkod güncelleme mantığı
- fiyat tarihçesi yazım detayları

### 4.2 `SatisService`

Sadece satış use case’ini yönetmeli.

İçerik:
- satış oluşturma
- satış güncelleme
- iade akışı
- taslak kaydetme / yükleme
- stok ve kasa etkilerini tetikleme

İçinde olmaması gerekenler:
- satır hesap formülleri
- indirim dağıtım algoritması
- veresiye borç mantığının düşük seviye detayları

### 4.3 `StokService`

Bu servis stok hareketlerinin tek sahibi olmalı.

İki farklı iş aynı sınıfta karışmamalı:
- hareket ekleme
- hareketlerden stok yeniden hesaplama

Öneri:
- hareket yazma metotları net olsun
- yeniden hesaplama ayrı bir internal helper olsun
- dış dünyaya sadece anlamlı use case metotları açılsın

### 4.4 `KasaService`

Kasa sadece kasa hareketi üretmeli.

Öneri:
- `KasaGelirEkle`
- `KasaGiderCik`
- `KasaHareketOlustur`

Bu servis içinde iş kuralı karmaşası olmamalı.
Kasa, alış/satış/veresiye domain’ini bilmemeli.

### 4.5 `UrunFiyatService`

Bu servis fiyat geçmişinin tek yazım noktası olmalı.

Öneri:
- ürün maliyet değişimi tek methoddan geçsin
- audit yazımı burada ya da tek bir ortak audit helper’da olsun
- alış/satış servisleri fiyat tablosuna doğrudan elle dokunmasın

### 4.6 `AuditLogService`

Audit log, tüm kritik işlemlerde standart şekilde çağrılmalı.

Öneri:
- create/update/delete için tek format
- eski ve yeni değer opsiyonel ama standart
- audit yazımı başarısız olursa ana transaction politikasına göre davranış net olsun

## 5. Tek Tip İş Akışı

Her kritik işlem için standart akış şu olmalı:

1. Request al.
2. Validate et.
3. Hesaplama yap.
4. Transaction başlat.
5. Ana kaydı oluştur veya güncelle.
6. Ortak yan etkileri sırayla uygula.
7. SaveChanges.
8. Audit yaz.
9. Commit.
10. Hata varsa rollback.

Bu akış bozulursa sistem bir noktada tutarsızlık üretmeye başlar.

## 6. Barkod ve KDV Kuralları

Bu iki konu özellikle bölünmemeli.

### Barkod

Barkod güncellemesi her serviste ayrı ayrı yazılmamalı.

Öneri:
- barkod değişikliği için tek bir yardımcı metod olsun
- gerekli validation tek yerde yapılsın
- hangi işlem barkod değiştirebiliyor açıkça tanımlansın

### KDV

KDV hesaplaması tek merkezden yönetilmeli.

Öneri:
- `KdvHesaplayici`
- ürün bazlı KDV
- default KDV
- satır bazlı KDV

Hepsi aynı hesap mantığını kullanmalı.

## 7. Alış Servisi İçin Önerilen Parçalar

`AlisService` içindeki `AlisYapAsync` şu parçalara ayrılmalı:

- `ValidateAlisRequest`
- `LoadTedarikci`
- `PrepareAlisHeader`
- `BuildAlisDetails`
- `ApplyProductUpdates`
- `ApplyStockMovement`
- `ApplyFinancialEffect`
- `ApplyPriceHistory`
- `SaveAndAudit`

Bu parçalar aynı class içinde private method olarak başlayabilir.
Sonra ihtiyaç olursa bazıları ayrı servise taşınır.

## 8. Stok ve Kasa Tutarlılığı

En önemli kural:

> Stok ve kasa bir işin sonucuysa, o işin dışında ayrı ayrı ve bağımsız commit edilmemeli.

Yani:
- alış kaydı oluştu ama stok oluşmadı
- kasa yazıldı ama audit yazılmadı
- fiyat güncellendi ama ana kayıt başarısız oldu

gibi yarım durumlar engellenmeli.

Bu yüzden:
- tek transaction
- tek commit noktası
- servisler arası senkron yan etkiler

zorunlu olmalı.

## 9. Refactor Sırası

Öncelik sırası şöyle olmalı:

1. `AlisService` içindeki büyük metotları böl.
2. `SatisService` içindeki hesaplama ve yan etki bloklarını ayır.
3. `StokService` içindeki yeniden hesaplama mantığını sadeleştir.
4. `UrunFiyatService` ile ürün güncelleme akışını tek sahipli hale getir.
5. Kasa ve audit çağrılarını ortak helper ile standartlaştır.
6. Sonra gerekirse daha küçük servisler çıkar.

## 10. Önerilen Kural Seti

- Bir servis bir ana use case yönetsin.
- Hesaplama ile persistence aynı metodda boğulmasın.
- Barkod, KDV, stok, kasa gibi kurallar tek noktadan yönetsin.
- Transaction sınırı net olsun.
- Exception ve `ServiceResult` kullanımı tek stile indirilsin.
- Controller, mümkün olduğunca sadece request/response yapsın.

## 11. Not

Bu refactor, sistemi “daha çok class” yapmak için değil, daha az sürprizli yapmak için yapılmalı.
Amaç mimari gösteriş değil, tutarlılık ve bakım kolaylığıdır.

