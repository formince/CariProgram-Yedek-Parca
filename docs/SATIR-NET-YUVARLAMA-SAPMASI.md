# Satır toplamı / indirim % yuvarlama sapması (799,99 vs 800,00)

## Sorun neydi?

Hızlı satışta kullanıcı **Toplam** sütununa örneğin **800,00 ₺** yazıyordu (brüt 820 ₺ iken). Ekranda ara toplam doğru görünüyordu; fakat satış kaydedildikten sonra detayda **799,99 ₺** ve satır indirimi **−20,01 ₺** çıkabiliyordu.

**Kök neden:** Arayüz satırı, kullanıcının girdiği net tutardan bir **indirim yüzdesi** türetiyordu; form gönderilirken bu oran **`paraFmt` ile 2 ondalık** (ör. **%2,44**) olarak gidiyordu. Sunucu ise indirimi tekrar şöyle hesaplıyordu:

`indirimTutari = Round(brüt × oran ÷ 100, 2)`

Örnek: `Round(820 × 2,44 ÷ 100, 2) = 20,01` → net **799,99**. Yani **ekrandaki net ile sunucunun “orandan yeniden hesapladığı” net aynı değildi**; sorun genel indirim modalından bağımsız, **satır satırındaki % → POST kısaltması** kaynaklıydı.

## Çözüm

- **`SatirNetTutarHedef`** (`SatisDetaySatirVM`): Kullanıcı toplam sütunundan net tutarı sabitlediğinde istemci bu alanı da gönderir.
- **`SatisTutarHesaplayici.SatirHesapla`**: `SatirNetTutarHedef` doluysa net tutar doğrudan bu değere göre (brüte göre sıkıştırılarak) alınır; indirim tutarı `brüt − net` ile **2 ondalık** üretilir; `IndirimOrani` veritabanına **gösterim/audit** için geriye doğru hesaplanır (ör. 6 hane).

İlgili kod: `ViewModels/SatisVM.cs`, `Services/SatisTutarHesaplayici.cs`, `Services/SatisService.cs`, `wwwroot/js/satis-hizli.js`, taslak yükünde `TaslagiYukleAsync` satır başına `SatirNetTutarHedef = NetTutar`.

## Diğer ekranlarda olmaması gerekir — nelere dikkat?

| Alan | Risk |
|------|------|
| **Hızlı satış** (`satis-hizli.js`) | Toplam sütunu: `SatirNetTutarHedef` gönderilir; düzeltildi. |
| **Bekleyen sepet / taslak** | Yüklemede satırlara kayıtlı `NetTutar` hedef olarak verilir; tamamlamada aynı mantık. |
| **Satış oluşturma (`Create`)** | Satırlar genelde **% veya birim** ile gidiyor; **manuel satır toplamı** yoksa aynı sapma tipi oluşmaz. İleride Create’e “satır toplamından türet” benzeri bir alan eklerseniz, **aynı sözleşmeyi** (`SatirNetTutarHedef` veya sunucuda eşdeğer) kullanın. |
| **Satış düzenleme (`Edit`)** | Şu an satırda doğrudan “toplam” girişi yok; **sadece % / fiyat / net birim**. Oran yine `paraFmt` ile 2 ondalık POST ediliyor; çok uç **brüt × oran** yeniden hesaplarında teorik sapma ihtimali vardır ama Hızlı Satıştaki “800 yazdım 799,99 oldu” senaryosunun aynısı değildir. İhtiyaç olursa Edit’e de `SatirNetTutarHedef` veya daha yüksek hassasiyetli oran gönderimi eklenebilir. |

**Özet kural:** Kullanıcıya **net satır tutarını** veya **nihai genel toplamı** “nihai gerçek” olarak gösteriyorsanız, sunucuya mümkünse **o tutarı** açık alanla gönderin; yalnızca **kısaltılmış %** ile yeniden hesaplatmayın.

## Eski yanlış kayıtlar

Yanlış kaydedilmiş satışlar (ör. net 799,99 yazılı) **otomatik düzelmez**. Yeni satışlar düzeltilmiş akışla doğru kaydolur; eski kayıt için iptal/iade/düzeltme iş kurallarınız geçerli olur.

---
*Son güncelleme: dokümantasyon amaçlı; uygulama davranışı kodla birlikte güncellenir.*
