# CariErinc Kullanıcı Arayüzü (UI) ve Görünüm (View) Mimarisi Raporu

Bu doküman, CariErinc projesindeki mevcut ön yüz (frontend) yapısını, kullanılan UI/UX kalıplarını, sayfaların amacını ve mevcut View (.cshtml) organizasyonunu açıklamaktadır. Bu raporun amacı; sisteme yepyeni, modern, etkileşimli ve "premium" hissi veren bir görünüm tasarımı tasarlamak adına (Google Stitch vd. yapay zeka tasarım toolları için) kapsamlı bir *brief (şablon)* sağlamaktır.

---

## 1. Temel Altyapı ve Teknolojiler
Projenin View katmanı ASP.NET Core MVC Razor (.cshtml) teknolojisi ile geliştirilmiştir. İstemci tarafı (Client-side) teknolojileri şunlardır:

*   **CSS Framework:** Bootstrap 5.3.2 (Ancak özelleştirilmiş değişkenler ve CSS gridleri ön plandadır).
*   **İkonlar:** Login sayfasında 'Lucide Icons', panel içerisinde ise sıklıkla standart emojiler (📦, ⚙️, 🗑️ vb.) kullanılmıştır. Yeni mizanpajda kurumsal bir icon-set (FontAwesome, Phosphor veya Lucide) tercih edilebilir.
*   **Fontlar:** Google Fonts 'Inter' ailesi. (Font ağırlıkları 300, 400, 500, 600, 700).
*   **Etkileşim:** jQuery 3.7.1, Vanilla JS (site.js) ve uyarılar için **SweetAlert2**.

---

## 2. Genel Yerleşim Planı (Layout Stratejisi)

Kullanıcı giriş yaptıktan sonra sistem `_Layout.cshtml` şablonunu kullanır.

*   `_Sidebar.cshtml`: Solda yer alan dik veya daraltılabilir (collapsible) menü. 
*   `_Navbar.cshtml`: Üstte yer alan, dükkan adını ve giriş yapan kullanıcıyı gösteren, mobil menü butonunu (hamburger ikon) barındıran üst bar.
*   `main-content`: Sidebar'ın sağında kalan arayüz bölgesidir. Veriler ve tablolar bu bölgede gösterilir. Form ve panellerin maksimum genişliği içerik durumuna göre bazen kısıtlanmıştır (örn. formlarda max-width: 600px).
*   *Mobil Uyum:* Ekran küçüldüğünde Sidebar kaybolur veya daralır, Navbar tam genişliğe geçer. Bir `.sidebar-overlay` yardımıyla mobilde sağdan / soldan menü açılır.

**Beklenti:** Yapılacak tasarımın; mevcut parçalı yapıyı (Sidebar + Topbar + Content) modern "glassmorphism", yuvarlatılmış köşeler, hafif gölgeler ve koyu mod / açık mod geçişleri ile renklendirmesi ("premium" görünüm) hedeflenmektedir.

---

## 3. Modüller, Sayfalar ve Amaçları

Sistemdeki View klasörleri temel ticari modüllere karşılık gelir. Menüde yer alan gruplar ve sayfaların amacı şöyledir:

### 3.1. Dashboard (`/Dashboard`)
*   Sistemin ana özet ekranıdır. Genellikle 4'lü kart ("stat-card") barındırır:
    1.  Bugünkü Satış (₺)
    2.  Kasa Bakiyesi (₺)
    3.  Açık Veresiye (Toplam ₺ ve müşteri sayısı)
    4.  Kritik Stok Uyarısı
*   Altında ise stok miktarı azalan ürünlerin (Kritik Stok) listelendiği basit bir *Dry Table* (Açıklama aşağıdadır) bulunur.

### 3.2. Satış ve POS (`/Satis`)
Bu modül uygulamanın kalbidir.
*   **Hızlı Satış (`/Satis/HizliSatis`):** Özel bir sayfadır. Standart CRUD sayfası *değildir*. 
    *   Sayfa iki sütuna ayrılmıştır. (%65 Sol, %35 Sağ vb.)
    *   **Sol Taraf:** Bir sepet / POS ekranı gibi çalışır. Barkod okutma inputu, miktar/indirim ayarları ve alt kısımda "Ara Toplam, Genel İndirim, Genel Toplam" baremleri bulunur.
    *   **Sağ Taraf:** Ödeme panelidir. 'Peşin' / 'Veresiye' radyo butonları, eğer veresiye ise müşteri arama kutusu ve "Satışı Tamamla", "Beklet" butonları bulunur. Özel Javascriptler ile donatılmıştır.
*   **Satışlar Listesi:** Geçmiş satış faturalarının listelendiği sayfadır.

### 3.3. Stok ve Ürünler (`/Urun`, `/Stok`)
*   **Ürünler Listesi (`/Urun/Index`):** Mağazadaki tüm ürünlerin listelendiği, barkod, kategori, alış/satış fiyatı, kdv ve stok miktarının bulunduğu ana CRUD tablosudur.
*   **Ürün Ekle/Düzenle (`/Urun/Form`):** Yeni ürün eklendiği veya düzenlendiği standart form yapısı.

### 3.4. Cari Yönetim (`/Musteri`, `/Tedarikci`, `/Veresiye`)
*   **Müşteriler Lisesi (`/Musteri/Index`):** Müşterilerin adı, telefonu ve **en önemlisi** Toplam Borcu (Veresiyesi) listelenir. Borcu olanların tutarı genellikle tehlike rengiyle (`text-danger`) gösterilir.
*   **Veresiye (`/Veresiye/Index`):** Kimin ne kadar bakiyesi olduğu, müşterilerden alınan ödemeler (Tahsilat işlemleri) bu liste ve formlardan yönetilir.
*   **Tedarikci:** Mal alınan toptancıların listelendiği kayıtlar.

### 3.5. Alış Modülü (`/Alis`)
*   Alış faturaları (toptancıdan mal alma). Form sayfası "Hızlı Satış" sayfasına benzer bir dinamikte tasarlanabilir; çoklu ürün kalemleri eklenebilecek bir "Sepet/Fiş" arayüzü gerektirir. Toptancıya yapılan borçlanmalar takip edilir.

### 3.6. Maliye (`/Kasa`, `/GiderKategori`)
*   **Kasa:** Kasaya giren çıkan tüm nakit veya sanal hareketlerin günlüğü. Bakiye takibi yapılır.
*   **Gider Kategorileri:** Dükkan masraflarının (Maaş, Elektrik, Kira vb.) kategorize edildiği CRUD sayfası.

### 3.7. Raporlar (`/Rapor`)
*   Bol bol "Data Table" içeriği barındıran; Günlük, Aylık, Kar/Zarar ve Stok bazlı istatistik sayfalarıdır. Yeni tasarımlarda buralara modern "Chart.js" veya "ApexCharts" türevi görselleştirmeler dahil edilebilecek estetik boşluklar bırakılmalıdır.

---

## 4. Ortak UI Bileşenleri (Component'ler)

Proje içinde standartlaşmış belirli HTML/CSS bileşenleri bulunmaktadır. Tasarım oluşturulurken bu "Dry (Don't Repeat Yourself)" bileşenlerin modernize edilmesi projeyi doğrudan ayağa kaldıracaktır.

### 4.1. Liste / Dry Table (Tablo Ekranları)
Genellikle her modülün bir `Index.cshtml`'si vardır. Standart yapı:
1.  **Filtre/Arama Kartı:** Üstte tabloyu filtrelemek için bir form kartı arayüzü. (Gölgesi olan beyaz bir kart `.card.border-0.shadow-sm`)
2.  **Tablo Başlığı ve Yeni Ekle Butonu:** Tablonun sol üstünde Başlık ve Toplam Kayıt Sayısı rozeti (`<span class="badge">`), sağında yeşil / primary renkli bir "➕ Yeni Ekle" butonu.
3.  **Table Wrapper:** Tablo, etrafında ince bir çerçeve ve hafif bir gölge bulunan `.table-wrapper` DIV'i içinde bulunur.
4.  **Tablo (`.table.table-hover`):** Modern görünümlü, satır üzeri gelindiğinde hafif renk değiştiren tablo.
5.  **Aksiyon Menüsü:** Tablo içi sağ hücrede butonlar (`<div class="btn-group btn-group-sm">`). Düzenle (✏️), Detay (🔍), Sil (🗑️) butonları yer alır. Sil butonları `btn-delete` class'ına sahiptir ve AJAX + SweetAlert tetikler.

### 4.2. Formlar (Ekle / Düzenle)
Modüllerin `Form.cshtml` dosyalarında geçerlidir.
1.  **Form Container:** Sayfanın soluna hizalanmış, ortalanmamış bir `.card` içerisinde görüntülenir. (Sıklıkla `style="max-width: 600px;"`).
2.  **Input Elementleri:** Modern form pratikleri (Label üstte, input allta). Hatalar inputun hemen altında kırmızı `text-danger` küçük metin ile gösterilir.
3.  **Aksiyon Butonları:** Kartın en altında üstten çizgili (`border-top`) bir bölme. Bir 'Kaydet' (Primary renk) ve bir 'Vazgeç/Geri Dön' (Secondary/Outline) butonu bulunur.

### 4.3. Pagination (Sayfalama)
Sayfaların altında standart bir kısmi görünüm (`_Pagination.cshtml`) ile "Önceki 1 2 3 4 Sonraki" yapısında butonlar bulunur.

### 4.4. Login (Giriş) Ekranı
Ayrı bir tasarımdır (`Login.cshtml`). Şu an sayfa ortasında, "Lucide Icons" ve sadeleşmiş, temiz bir enterprise görünüme sahiptir. Tasarım aracı Login ekranı için izole, çok daha kurumsal ve belki dinamik bir arkaplana / bölünmüş (split screen) yapıya sahip bir template üretebilir.

---

## Tasarım (Google Stitch vb. için) Talimatları

Bu projeye tasarım yapacak UI/UX motoru veya tasarımcıdan beklentiler şöyledir:

1.  **Renk Paleti:** Mevcut "Primary: #2563EB", "Background: #F8FAFC" standarttır. Çok daha şık (Hafif bir mor, lacivert tonajı veya premium bir dark-mode) bir Dashboard renk şeması üretin.
2.  **Typography & Boşluklar (Whitespace):** Tabloların sıkışık görünmemesi için bol whitespace (padding) kullanılmalı. Font hiyerarşisi kalınlaştırılmış başlıklar ve belirginleştirilmiş fiyat etiketleri şeklinde organize edilmeli. Para birimi tutarları (₺) hep odak noktasında kalın (bold) fontla belirtilmeli.
3.  **Modern Kartlar (Cards):** Sadece beyaz kutular yerine, hafif "gradient" (renk geçişli) borderlara sahip, hover durumlarında 1-2 px yukarı kalkan statik istatistik kartları oluşturulmalı.
4.  **Hızlı Satış (POS) Özel Tasarımı:** E-Ticaret / Hızlı Satış ekranı olduğu için barkod giriş alanının (input) çok "büyük ve belirgin" (Focus durumu öne çıkan), sipariş sepeti satırlarının ise birer kağıt fişi/faturası andıran şekilde derli toplu ve şık bir tasarıma ihtiyacı vardır. Sepetteki ürün silme butonları narin ancak kullanışlı olmalıdır.
5.  **Durum Renklendirmeleri (Badges):** Veresiye durumları (Ödendi, Bekleyen) gibi veriler standart metin olarak değil, modern "Pill Badges" (hap şeklinde, background'u pastel renklli, text'i koyu arka plandan okunan) tasarımlarla değiştirilmelidir.

Bu doküman projenin omurgasıdır. Yeni tasarlanacak HTML/CSS dosyaları direkt olarak MVC `_Layout.cshtml`, `Index.cshtml` ve `Form.cshtml` sayfalarındaki sınıflar (classes) gözetilerek parçalanıp projeye dahil edilecektir.
