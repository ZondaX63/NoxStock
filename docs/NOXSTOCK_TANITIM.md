# NoxStock

## Stok, satış ve finans tek operasyon ekranında

NoxStock; stok, satın alma, satış, perakende, müşteri-cari hesap ve kasa/banka hareketlerini aynı iş akışında birleştiren web tabanlı ticari işletme yönetim uygulamasıdır.

Özellikle dövizle maliyetlenen ürünleri TL, USD veya EUR ile satan işletmeler için tasarlanmıştır. Satış fiyatı, eski bir TL maliyetine sabit kalmaz; onaylanan alış belgesindeki maliyet, referans döviz ve güncel kur üzerinden izlenebilir şekilde önerilir. Böylece kur değişimlerinin karlılığı sessizce eritmesi engellenir.

## Kimin için?

- Yedek parça, teknik ürün, elektronik ve ithal ürün satan işletmeler
- Stok, satış, fatura ve cari takibini tek ekrandan yönetmek isteyen KOBI'ler
- Hem faturalı satış hem hızlı perakende/POS satışı yapan firmalar
- Birden fazla şirketi tek platformda, birbirinden izole şekilde yönetmek isteyen yöneticiler

## NoxStock ile neler yönetilir?

### Ürün ve stok

- Ürün kartları, stok kodu, barkod, marka, kategori, raf/konum ve kritik stok seviyesi
- Birden fazla OEM kodu ile arama: Aynı parçanın farklı marka ve stok kodlu alternatifleri tek aramada bulunur
- Stok giriş/çıkışlarının belge ve kullanıcı bağlantısıyla izlenmesi
- Satın alma, satış, POS ve iptal işlemlerinin stok hareketlerine yansıması
- Kritik ve sıfır stokların dashboard ve ürün listesinde görünmesi
- Kolon seçimi, sıralama, filtreleme ve kaydedilen kullanıcı tercihleriyle yoğun ürün kataloglarında hızlı çalışma

### Dövizden korumalı fiyatlama

NoxStock'un temel farkı, satın alma maliyeti ile satış fiyatını açıklanabilir bir kurgu içinde bağlamasıdır.

1. Alış faturası onaylanır.
2. Net TL maliyeti, belge anındaki kurla referans para birimine çevrilir ve maliyet snapshot'ı olarak kilitlenir.
3. Ürünün maliyet üzeri kar ekleme oranı uygulanır.
4. POS ve satış faturası güncel kurla satış önerisini TRY, USD veya EUR olarak getirir.
5. Satış onaylandığında kullanılan fiyat, kur ve maliyet bilgisi belge satırında sabitlenir.

Bu sayede kullanıcı, “Bu ürün neden bu fiyatta?” sorusunun cevabını ürün, fatura ve POS ekranlarında görebilir.

- Son onaylı alış faturası maliyetin kaynağıdır
- Maliyet tarihi, tedarikçi, belge, işlem kuru ve TRY karşılığı saklanır
- Dinamik fiyat veya manuel fiyat politikası ürün bazında seçilir
- Kullanıcı fiyatı değiştirirse manuel fiyat etiketi ve maliyet altı satış uyarısı görünür
- Kur yoksa sistem sıfır fiyat üretmez; fiyatın hesaplanamadığını açıkça bildirir

### Satın alma ve satış faturaları

- Alış ve satış faturası taslağı oluşturma, düzenleme ve onaylama
- Belge para birimi, işlem kuru, KDV, iskonto, vade ve satır bazlı fiyat yönetimi
- Alış faturası onayında ürünün son alış maliyeti snapshot'ının güncellenmesi
- Satış faturasında dinamik fiyat hesabının ve fiyat override bilgisinin gösterilmesi
- Taslak belgelerin düzenlenmesi, onaylı belgelerde fiyat/kur geçmişinin korunması
- İptal akışlarında stok ve ticari hareketlerin ters kayıtla izlenmesi

### POS ve perakende satış

- Barkod, SKU, ürün adı veya OEM numarasıyla hızlı ürün bulma
- Sepet, miktar değiştirme, müşteri seçimi ve ödeme yöntemi
- Dinamik fiyatlı ürünlerde sade satış fiyatı; gerektiğinde ayrıntılı fiyat hesabı
- Ürün sepete eklendiğinde fiyatın kilitlenmesi
- Bilinçli “fiyatları güncelle” aksiyonu ile güncel kurun kasiyer kontrolünde uygulanması
- POS satışı ve iptalinde stok, cari ve hesap hareketlerinin birlikte işlenmesi

### Müşteri, tedarikçi ve cari yönetimi

- Müşteri ve tedarikçi kartları
- Cari bakiye, hareket geçmişi ve belge ilişkileri
- Müşteri satış geçmişi ile satın alma ilişkilerinin izlenmesi
- Tahsilat ve ödeme kayıtlarının ilgili cari ve hesap hareketlerine işlenmesi
- Tedarikçi özelinde alış ve borç geçmişini görüntüleme

### Finans ve yönetim görünümü

- Kasa, banka ve kredi kartı hesapları
- Tahsilat, ödeme, gelir, gider ve hesaplar arası transfer
- Günlük satış, aylık ciro, müşteri alacağı, tedarikçi borcu ve net durum özeti
- Kritik stok, bekleyen faturalar, son işlemler ve satış trendleri için dashboard
- Türkçe ve İngilizce arayüz, açık/koyu tema desteği

## Piyasadaki klasik yaklaşımlardan farkı

| Klasik stok takibi | NoxStock yaklaşımı |
| --- | --- |
| Alış fiyatı ürün kartına elle yazılır ve güncelliğini kaybeder. | Onaylanan son alış faturası maliyetin izlenebilir kaynağıdır. |
| TL maliyet sabit kalır; kur değişiminde kar fark edilmeden azalır. | Referans döviz maliyeti korunur; satış önerisi güncel kurla üretilir. |
| POS, fatura, stok ve cari farklı ekranlarda kopuk ilerler. | Ticari işlem stok, cari ve hesap etkileriyle birlikte ele alınır. |
| Aynı parçanın farklı markaları ayrı ürünler arasında kaybolur. | OEM numarasıyla marka ve stok kodundan bağımsız alternatif ürün araması yapılır. |
| Geçmiş belgelerde fiyatın neden değiştiği anlaşılmaz. | Onaylanan satışlarda fiyat, kur ve maliyet snapshot'ı belge üzerinde kalır. |
| Çok şirketli kullanımda veri karışma riski oluşur. | Her şirket kendi tenant alanında izole çalışır; platform yöneticisi şirketleri merkezi olarak yönetir. |

## Güven veren ticari kayıt yaklaşımı

NoxStock, ticari işlemleri yalnızca ekranlardaki toplamlar olarak görmez. Kritik onay, iptal, stok ve ödeme hareketleri izlenebilir bir akışta ele alınır. Bu yaklaşım, günlük operasyonu hızlandırırken daha sonra “stok neden azaldı?”, “bakiye nereden geldi?” veya “bu fiyat hangi kura göre oluştu?” sorularına cevap verebilmeyi hedefler.

## Kullanım modeli

- Platform yöneticisi şirket çalışma alanlarını oluşturur ve yönetir.
- Her şirket kendi tenant URL'si üzerinden giriş yapar.
- Şirket verileri diğer şirketlerden izole tutulur.
- Yetki ve kullanıcı yönetimi yönetim alanından yürütülür.

## Private beta kapsamı

NoxStock şu anda kontrollü private beta olarak sunulmaktadır. Pilot kullanım için önerilen kapsam 5-10 şirket ve şirket başına 5.000-10.000 üründür.

Bugün odaklanan kritik iş akışları: ürün/OEM arama, döviz bazlı dinamik fiyatlama, alış-satış faturası, POS satış ve iptal, tahsilat/ödeme, cari ve hesap hareketleri, dashboard ve tenant izolasyonudur.

Depo-şube transferleri, stok rezervasyonu, e-Fatura/e-Arşiv entegrasyonları, gelişmiş genel muhasebe, CRM otomasyonları ve çok yüksek hacimli kurumsal ölçekleme çalışmaları ürün yol haritasındadır; private beta kapsamında vaat edilmez.

## NoxStock'un vaadi

NoxStock, işletmenin günlük ticari operasyonunu daha fazla ekran ekleyerek değil, kayıtların birbirini doğru beslemesini sağlayarak sadeleştirir. Stok, maliyet, fiyat, satış, cari ve finans aynı işletme gerçeğini anlatmalıdır. NoxStock bu bütünlüğü görünür, anlaşılır ve yönetilebilir hale getirmek için tasarlanmıştır.
