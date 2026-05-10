# Low-risk Faz 3 Teknik Notu

Bu not, Faz 3 kapsaminda yapilan dusuk riskli sadeleştirme adimlarini ve beklenen etkileri ozetler.

## Yapilanlar

- `SatisService` icinde stok/kasa/veresiye yan etkileri helper metotlara ayrildi.
- `VeresiyeService` icinde odeme validasyonu ve odeme kaydi olusturma adimlari ortak helper metotlara alindi.
- `AlisService` icinde odeme validasyonu/odeme kaydi olusturma ayristrildi.
- `Alis` borc durum modelinde tek hesap kurali icin yeni yardimci eklendi:
  - `Helpers/AlisBorcHesaplayici.cs`
  - `CalculateKalanBorc(alis)`
  - `IsTamOdendi(alis)`
  - `RecalculateDurum(alis)`
  - `SetInitialDurum(alis)`
- `OdemeYapAsync` akisi, artimsal `KalanBorc -= tutar` yerine helper tabanli yeniden hesaplamaya cekildi.
- `GetVadeliAcikAlislarAsync` filtresi `ToplamTutar > OdenenTutar` kuralina hizalandi.
- Okuma tarafinda `CariService` ve `TedarikciService` kalan borc hesaplari ortak helper mantigina baglandi.
- `AlisController` odeme ekrani giris kontrolu `IsTamOdendi` kuralina hizalandi.

## Etki

- Kalan borc hesap mantigi tek noktada toplandi.
- Ekranlar arasi "kalan borc" drift riski azaltildi.
- Servis metotlari daha okunur hale geldigi icin hata ayiklama ve degisiklik yapma maliyeti dustu.

## Dogrulama

- `dotnet build` basarili (0 hata, 0 uyari).
- Faz 3 kapsamindaki degisiklikler derleme seviyesinde regresyon vermedi.
