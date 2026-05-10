# Guvenli Cari Gecisi Bagimlilik Analizi

Bu not, guvenli gecis planindaki "audit-current" adimi icin mevcut bagimlilik haritasini dokumante eder.

## Veri modeli bagimliliklari

- `Musteri.ToplamBorc` musterinin isletmeye borcunu temsil eder.
- `Tedarikci.ToplamBorc` isletmenin tedarikciye borcunu temsil eder.
- `Satis` kayitlari `MusteriId` ile calisir.
- `Veresiye` kayitlari `MusteriId` ile calisir.
- `Alis` kayitlari `TedarikciId` ile calisir.
- `Urun` icinde varsayilan tedarikci iliskisi `TedarikciId` ile tutulur.

## Servis bagimliliklari

- `SatisService`: veresiye satislarda `Musteri` borcunu artirir, pesin satislarda kasaya gelir yazar.
- `VeresiyeService`: veresiye odeme ve kapatma islemlerinde hem borc hem kasa hareketi uretir.
- `AlisService`: vadeli alis ve odeme islemlerinde `Tedarikci` borcunu ve kasa hareketlerini yonetir.
- `MusteriService` ve `TedarikciService`: detay ekran ozetlerini ayri uretir.
- `DashboardService` ve `RaporService`: acik veresiye ve alim-satim toplamlarini ayri raporlar.

## Kasa etkisi kurallari (korunacak)

- Pesin satis -> kasa gelir.
- Veresiye satis -> kasa etkisi yok.
- Veresiye tahsilat -> kasa gelir.
- Nakit alis -> kasa gider.
- Vadeli alis -> kasa etkisi yok.
- Vadeli alis odemesi -> kasa gider.
- Mahsup -> kasa etkisi olmamali.

## Gecis riski notlari

- `BorcHelper` sifir alti bakiyeyi engelledigi icin net bakiye tek alanda tutulmuyor.
- Kayit silme ve duzeltme akislari satis/alis/veresiye tarafinda zincirli etkiye sahip.
- Bu nedenle ilk adimda sadece ozet/okuma katmani eklenip yazma akislari korunmali.

## Shim Controller Kaldirilabilirlik Checklisti

Bu bolum, `MusteriController` ve `TedarikciController` shim controller'larini kaldirmadan once kontrol edilmesi gereken yetki bagimliliklarini ozetler.

### Mevcut durum

- `MusteriController` butun actionlarda `Cari` ekranlarina redirect eder.
- `TedarikciController` butun actionlarda `Cari` ekranlarina redirect eder.
- Yetki kontrolu `YetkiMiddleware` icinde `(ControllerName, ActionName)` bazli calisir.
- Bu nedenle kullanicinin `Musteri/Index` yetkisi varsa ama `Cari/Index` yetkisi yoksa, redirect olsa bile once `Cari` aksiyonuna yetki denetimi uygulanir.

### Risk seviyesi

- `MusteriController` silinmesi: **Orta-Yuksek risk** (rol/yetki tablolari guncel degilse 403 riski).
- `TedarikciController` silinmesi: **Orta-Yuksek risk** (rol/yetki tablolari guncel degilse 403 riski).
- Sadece shimleri bir sure daha tutmak: **Dusuk risk**.

### Kaldirma oncesi zorunlu kontroller

1. Rol-yetki kayitlarinda `Cari` controller aksiyonlarinin tum ilgili rollere tanimli oldugunu dogrula.
2. `Musteri` ve `Tedarikci` icin menu/route referanslarinin kalmadigini dogrula.
3. Non-admin bir test kullanicisi ile su akislari smoke test et:
   - `Cari/Index`
   - `Cari/Form`
   - `Cari/Detail`
4. Uretim loglarinda `Auth/Yetkisiz` artisi olup olmadigini yayin sonrasi izle.

### Kaldirma karari icin onerilen sira

1. Once `Cari` yetkilerini tum ilgili rollere ac.
2. 1 yayin sureci boyunca shimleri tutup izleme yap.
3. Sorun yoksa sonraki yayinda shim controller'lari kaldir.

## Tek Admin Operasyon Notu (Shimler Kaldirildi)

- `MusteriController` ve `TedarikciController` shim dosyalari kaldirildi.
- Bu nedenle eski bookmark URL'ler (`/Musteri/*`, `/Tedarikci/*`) artik 404 donecektir.
- Guncel erisim noktasi `Cari` akisidir:
  - `/Cari/Index`
  - `/Cari/Form`
  - `/Cari/Detail/{id}`
- Tek admin kullaniminda rol-yetki gecis riski pratikte dusuktur; yine de yayin sonrasi ilk giriste menuden `Cari` ekranlarinin acildigi kontrol edilmelidir.
