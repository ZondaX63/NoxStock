# P0 Açılış Bakiyesi ve Bakiye Güvenliği — Evrimsel Analiz

Denetim tarihi: 2026-08-06  
Karar sınırı: Bu belge yeni bir muhasebe platformu veya genel defter önermez. Amaç, beta verisini koruyarak mevcut stok, cari ve kasa/banka akışlarının yanlış bakiye üretmesini engellemektir.

## 1. Mevcut Cari Hareket Modelleri ve Tabloları

| Yapı | Durum | Gerçek rolü | Kaynak |
|---|---|---|---|
| `BusinessPartner` | Var | Müşteri/tedarikçi tipini ve **materyalize net** `Balance` değerini tutar; ayrı cari hareket satırı değildir | `src/NoxStock.Domain/BusinessPartners/BusinessPartner.cs` |
| `Document` | Var | Satış, alış, tahsilat (`Collection`) ve ödeme (`Payment`) kaynak belgesi olabilir; `Status`, `PaidAmount`, tarih ve audit alanlarını taşır | `src/NoxStock.Domain/Documents/Document.cs` |
| `BusinessPartnerTransfer` | Var, riskli eski davranış | Müşteri alacağını başka bir tedarikçi borcuna aktarır; standart cari mahsup değildir | `src/NoxStock.Domain/BusinessPartnerTransfers/BusinessPartnerTransfer.cs` |
| `BusinessPartnerStatementAppService` | Kısmen var | Belgeye bağlı hesap hareketleri ve transferlerden geçmiş üretir; bağımsız cari hareketlerin tam defteri değildir | `src/NoxStock.Application/BusinessPartners/BusinessPartnerStatementAppService.cs` |

**Sonuç:** Cari için `AccountTransaction` eşdeğeri, doğrudan bir `BusinessPartnerTransaction` tablosu yoktur. `BusinessPartner.Balance` mevcut beta verisi olarak korunmalıdır; geçmişten yeniden hesaplanmayacak veya otomatik açılış hareketine dönüştürülmeyecektir.

## 2. Mevcut Kasa/Banka Hareket Modelleri ve Tabloları

| Yapı | Durum | Gerçek rolü | Kaynak |
|---|---|---|---|
| `Account` | Var | Kasa/banka/kart hesabı, currency, aktiflik ve materyalize `Balance` | `src/NoxStock.Domain/Accounts/Account.cs` |
| `AccountTransaction` | Var | Hesap, opsiyonel `DocumentId`, `Type`, `Amount`, currency, tarih, açıklama ve `OperationKey` | `src/NoxStock.Domain/Accounts/AccountTransaction.cs` |
| `AccountTransactionType.Opening` | P0.1B’de kullanıma alındı | Açılış türü, özel açılış akışında materyalize hesabı artıran izlenebilir harekettir; genel gelir/gider API’si bu türü kabul etmez | `src/NoxStock.Domain.Shared/Accounts/AccountTransactionType.cs`, `Application/Accounts/AccountAppService.cs` |

EF modeli `AccountTransaction` için tenant+document+operation-key benzersizliğini zaten içerir. Belgesiz manuel hareketler ve virmanlar için aynı kuvvette idempotency indeksi yoktur. Kaynak: `src/NoxStock.EntityFrameworkCore/EntityFrameworkCore/NoxStockDbContext.cs`.

## 3. Mevcut Manuel Gelir/Gider ve Para Hareketleri

- `AccountAppService.AddTransactionAsync(AddTransactionDto)` mevcut manuel giriş noktasıdır. `Income` hesabı artırır; `Expense`, `Payment` ve `TransferOut` azaltır. P0.1B’den itibaren `Opening` bu genel API’de reddedilir; yalnız özel açılış akışı kullanılabilir.
- Kasa/banka ekranındaki manuel hareketler `Pages/Accounts/*` üzerinden bu API’ye gider.
- `DocumentType.Collection` ve `DocumentType.Payment` mevcut, onaylanabilir kaynak belgelerdir. `Payments/CreateModal` bunları oluşturup hemen onaylar; cari ve hesap etkisini birlikte üretir.
- `Shared/PaymentModal` ise `BusinessPartnerAppService.ProcessPaymentAsync` yolunu kullanır; cari ve `AccountTransaction` etkisi üretir ama `Document`/status/iptal kaynağı oluşturmaz.

**Çift sayım riski:** `Income`/`Expense` isimleri kullanıcıda satış geliri/alış gideri algısı yaratabilir. Tahsilat/ödeme ayrı akıştır; raporlarda tahsilat ikinci satış geliri veya ödeme ikinci alış gideri olarak toplanmamalıdır. P0’da isimlendirme “Diğer para girişi”, “Diğer para çıkışı” ve “Masraf” biçimine evriltilecek; fatura tahakkuku ile hesap hareketi ayrı rapor metrikleri olarak korunacaktır.

## 4. Mevcut Fatura ve POS İptal Mekanizması

| Kaynak işlem | Mevcut iptal yaklaşımı | Güçlü taraf | Eksik |
|---|---|---|---|
| Satış/alış faturası | `DocumentWorkflowAppService.CancelAsync` | `DocumentStatus.Cancelled`; ters stok, cari ve hesap etkisi aynı UoW içinde | Ayrı iptal nedeni/kullanıcısı/özgün-ters hareket bağlantısı yok; `PaidAmount` allocation değildir |
| POS | `PosSaleAppService.CancelAsync` | Ters stok hareketi, ters `AccountTransaction` ve veresiye cari etkisi üretilir | İptal nedeni ve standardize source/reversal bağlantısı yok |
| Tahsilat/ödeme belgesi | `DocumentWorkflowAppService.CancelAsync` | `Collection`/`Payment` belge türleri mevcut cancel akışından yararlanır | Liste ekranındaki `Shared/PaymentModal` bu yolu kullanmaz |
| Bağımsız tahsilat/ödeme | `BusinessPartnerAppService.ProcessPaymentAsync` | P0 doğrulamaları ve UoW vardır | Kaynak belge/status/iptal yolu ve operation key yok |
| Hesap virmanı/manüel hareket | `AccountAppService.TransferAsync` / `AddTransactionAsync` | Aynı currency transfer doğrulaması vardır | İptal/ters kayıt ve belge dışı idempotency yok |

## 5. Mevcut Bakiye Güncelleme Servisleri

- Fatura onay, ödeme kaydı ve iptali: `src/NoxStock.Application/Documents/DocumentWorkflowAppService.cs`.
- POS satış ve iptali: `src/NoxStock.Application/Sales/PosSaleAppService.cs`.
- Cari tahsilat/ödeme ve eski cari aktarımı: `src/NoxStock.Application/BusinessPartners/BusinessPartnerAppService.cs`.
- Hesap oluşturma, aktarım ve manuel hareket: `src/NoxStock.Application/Accounts/AccountAppService.cs`.
- Stok sayımı: `src/NoxStock.Application/InventoryCounts/InventoryCountAppService.cs`.
- Ürün importu ve ürün düzenleme: `src/NoxStock.Application/Products/ProductAppService.cs`.

## 6. `Balance` ve Stok Bakiyesi Yazan Kod Noktaları

### `Account.Balance`

- `AccountAppService.CreateAsync`: DTO’daki legacy `Balance` korunur ancak uygulanmaz; hesap sıfır bakiye ile oluşturulur.
- `AccountAppService.CreateWithOpeningAsync` / `CreateOpeningBalanceAsync`: mevcut `AccountTransactionType.Opening` ile açılışı tek UoW içinde yazar ve `Account.Balance` değerini artırır.
- `AccountAppService.TransferAsync`: kaynak/hedef hesap bakiyeleri değiştirilir.
- `AccountAppService.AddTransactionAsync`: diğer giriş/çıkış için bakiye değiştirilir.
- `BusinessPartnerAppService.ProcessPaymentAsync`: tahsilat/ödemede hesap bakiyesi değiştirilir.
- `DocumentWorkflowAppService`: peşin fatura, tahsilat/ödeme belgesi, ödeme kaydı ve iptal etkisi.
- `PosSaleAppService`: nakit/banka POS tahsilatı ve iptal etkisi.

### `BusinessPartner.Balance`

- `BusinessPartnerAppService.CreateAsync`: yeni caride DTO `Balance` doğrudan yazılır.
- `BusinessPartnerAppService.UpdateAsync`: P0’da düzeltildi; gelen `Balance` korunur ve uygulanmaz.
- `BusinessPartnerAppService.ProcessPaymentAsync`: bağımsız tahsilat/ödeme.
- `BusinessPartnerAppService.TransferCustomerReceivableAsync`: eski cari aktarımı.
- `DocumentWorkflowAppService`: fatura, belge ödemesi ve iptali.
- `PosSaleAppService`: veresiye POS ve iptali.

### `Product.Quantity`

- `DocumentWorkflowAppService`: onaylı satış/alış faturası ve iptal.
- `PosSaleAppService`: POS satış/iptal.
- `InventoryCountAppService`: tamamlanan sayım.
- `ProductAppService`: ürün importu ve doğrudan ürün miktarı güncellemesi.

## 7. Açılış Bakiyesini En Az Değişiklikle Ekleme Seçenekleri

| Seçenek | Kod değişikliği | Veri riski | API kırılma riski | Test maliyeti | Geri alma kolaylığı | Mevcut ekranlarla uyum |
|---|---|---|---|---|---|---|
| **A. Mevcut hareket enum’una tür ekleme** | Düşük (hesap tarafı) / yetersiz (cari) | Düşük | Düşük | Düşük | Yüksek | Hesap ekranına iyi uyar |
| **B. Mevcut manuel hareket/belge modelini genişletme** | Orta-yüksek | Orta | Orta | Orta | Orta | Tahsilat/ödeme belgesine yakın; hesap açılışına doğal değil |
| **C. Küçük yeni cari açılış kaynak tablosu** | Orta | Düşük | Düşük | Orta | Yüksek | Cari ekranına doğal; yalnız ihtiyaç duyulan alanları taşır |

### A. `AccountTransactionType.Opening` kullanmak

- **Uygun olduğu yer:** Kasa/banka açılışı. Enum zaten vardır; `Account` önce sıfır bakiye ile oluşturulur, aynı UoW içinde `Opening` hareketi ve bu hareketin etkisi yazılır.
- **Yetersiz olduğu yer:** Cari açılışı. Cari hareket tablosu yoktur; `AccountTransaction` bir `AccountId` gerektirir.
- **Beta verisi:** Eski `Account.Balance` aynen kalır; yalnız yeni hesaplarda açılış hareketi oluşturulur. Otomatik backfill yapılmaz.

### B. `DocumentType.Collection/Payment` veya manuel hareketi genişletmek

- **Artısı:** `Document` zaten status/audit ve `CancelAsync` akışına sahiptir; tahsilat/ödeme için halihazırda kullanılır.
- **Eksisi:** Cari açılışının kasa/banka etkisi olmamalıdır; `Collection`/`Payment` onayı zorunlu hesap hareketi üretir. Yeni `DocumentType.Opening` ise belge numarası, liste/rapor filtreleri, onay kuralları ve mevcut istemci enum davranışlarında yaygın özel durum oluşturur. Hesap açılışına da doğal çözüm değildir.
- **Karar:** Açılış için önerilmez. Mevcut tahsilat/ödeme belgesi ve iptal akışı korunur.

### C. Küçük `BusinessPartnerOpeningBalance` kaynak tablosu eklemek

- **Neden gerekli olabilir:** Cari için mevcut tablolar arasında hesap etkisi olmadan, tarih/açıklama/kullanıcı/status/iptal bağlantısı taşıyan bir kaynak işlem yoktur.
- **Minimum alanlar:** `BusinessPartnerId`, imzalı `Amount`, `Currency`, `OpeningDate`, `Description`, `Status`, `CancelledAt`, `CancelledBy`, `CancellationReason`, `ReversalOfId` ve audit/tenant alanları.
- **Davranış:** Yeni cari açılışında ya da ayrı “Açılış bakiyesi gir” aksiyonunda tek UoW ile kaynak satır ve `BusinessPartner.Balance` etkisi yazılır. İptal aynı tabloda ters kaynak satır oluşturur. Kullanıcı bu satırları düzenlemez.
- **Beta verisi:** Hiçbir mevcut `Balance` veya belge yeniden yorumlanmaz. Eski bakiye legacy başlangıç olarak kalır; yeni işlem yalnız ileriye dönük etki yaratır.

## 8. Öneri: Hibrit, En Düşük Riskli Evrimsel Yol

**Önerilen yol A + C'dir; B önerilmez.**

1. Kasa/banka için mevcut `AccountTransaction` ve mevcut `Opening` enum’u genişletilir. Yeni tablo gerekmez.
2. Cari için mevcut model yetersiz olduğundan yalnız `BusinessPartnerOpeningBalance` adlı küçük kaynak tablo eklenir. Bu genel ledger değildir; yalnız açılışın kaynak kaydıdır.
3. Mevcut `Balance` request alanları silinmez. Sunucu yeni güncelleme isteklerinde alanı yok sayar; response DTO’da okunabilir kalır. Sonraki major sürüm için deprecated notu eklenir.
4. Eski beta bakiyeleri, belgeleri, tahsilatları ve `AccountTransaction` satırları dönüştürülmez. Migration yalnız yeni tablo/alanı ekler; rollback migration yeni boş tabloyu kaldırabilir, mevcut veriye dokunmaz.
5. Bağımsız tahsilat/ödeme için iki mevcut yolun (`Payments/CreateModal` belge tabanlı yol ve `Shared/PaymentModal` doğrudan yol) kullanıcı/API kullanımı ölçülmeden birleştirme yapılmaz. P0’da önce her birine idempotency ve iptal güvenliği değerlendirilir.

## 9. Küçük ve Geri Alınabilir Uygulama Sırası

1. `[FIX]` Cari düzenleme isteğinde `Balance` uygulamasını engelle — **tamamlandı**, eski API alanı silinmedi.
2. `[HARDEN]` Kasa/banka açılışında mevcut `Opening` enum’unu gerçekten işletecek, UoW ve doğrulama içeren küçük servis metodu ekle — **P0.1A/B tamamlandı**.
3. `[MIGRATION] [EXTEND]` Yalnız cari açılışı için küçük kaynak tablo ve modal ekle; beta bakiyelerini migrate etme.
4. `[HARDEN]` Hesap/cari açılış iptalini mevcut fatura/POS’un ters etki yaklaşımını örnek alarak ekle; status, neden ve bağlantı alanlarını yalnız gereken mevcut hareketlere genişlet.
5. `[HARDEN]` Tahsilat/ödeme, manuel hareket ve hesap aktarımı için endpoint bazlı idempotency risk değerlendirmesi; yalnız riskli yere `OperationKey`/unique indeks ekle.
6. `[FIX]` Hesap ekranındaki gelir/gider dilini “diğer para girişi/çıkışı” ve “masraf” olarak ayır; rapor toplamlarını çift sayım açısından test et.
7. `[DEFERRED]` P0 bitmeden ödeme tahsisi, avans, iade, vade/limit genişletmesi ve çok depo geliştirmesi yapılmaz.

## 11. P0.1A/B Uygulama Sonucu — 2026-08-06

### Korunan yapılar

- Yeni tablo ve migration yoktur. `Account`, `AccountTransaction`, `AccountTransactionType.Opening`, mevcut audit alanları ve hesap ekstresi korunmuştur.
- Mevcut beta `Account.Balance` değerleri, geçmiş hesap hareketleri ve belgeler dönüştürülmemiş, yeniden hesaplanmamış veya sıfırlanmamıştır.

### Değişen dosyalar ve davranış

- `src/NoxStock.Application/Accounts/AccountAppService.cs`: normal oluşturma/güncelleme DTO `Balance` değerini yazmaz; özel açılış oluşturma metotları tek UoW ile hesap/hareket/bakiye etkisini yönetir.
- `src/NoxStock.Application.Contracts/Accounts/CreateAccountWithOpeningDto.cs` ve `CreateAccountOpeningBalanceDto.cs`: kullanıcı diliyle açılış tutarı, tarih ve açıklamayı taşır.
- `src/NoxStock.Web/Pages/Accounts/CreateModal.*`: yeni hesap modalı legacy `Balance` yerine açılış DTO’sunu kullanır.
- `src/NoxStock.Application.Contracts/Accounts/CreateUpdateAccountDto.cs`: `Balance` alanı API deserialization uyumluluğu için korunur fakat deprecated/uygulanmayan alan olarak belgelenmiştir.

### Uyumluluk ve risk

- **API kırılması yoktur:** eski istemci `Balance` gönderebilir. Ancak yeni davranışta bu değer uygulanmaz; eski istemci açıkça `CreateWithOpeningAsync` akışına geçirilmelidir.
- **Şimdilik tek açılış:** servis, hesap başına bir `Opening` kaydını engeller. `AccountTransaction` henüz iptal durumu taşımadığından iptal edilmiş açılışa yeniden izin verme P0.1E’den sonra tamamlanacaktır.
- **Eşzamanlı çift istek:** mevcut şemada açılış için özel idempotency anahtarı/filtreli benzersiz indeks yoktur. Normal kullanıcı akışında servis doğrulaması vardır; retry ve eşzamanlılık sertleştirmesi P0.1E/P0.1G kapsamındadır.
- **Özel yetki ve mevcut hesap aksiyonu:** P0.1F’de `FinancialAccounts.OpeningBalance` izni, güçlü legacy bakiye uyarısı ve “Açılış Bakiyesi Gir” modalı eklenecektir.

## 10. Uyum, Migration ve Rollback Özeti

- **Etkilenen endpointler:** `AccountAppService.CreateAsync/UpdateAsync/AddTransactionAsync`, `BusinessPartnerAppService.CreateAsync/UpdateAsync`, doğrudan tahsilat/ödeme ve mevcut ödeme belge akışları.
- **Eski davranış:** Request DTO’lar `Balance` alanını alır; oluşturma akışında doğrudan entity bakiyesi yazılabilir.
- **Yeni davranış:** Güncellemede bakiye yazımı yok sayılır; yeni açılışlar yalnız yeni kaynak işlemleriyle yazılır. Response DTO’daki `Balance` korunur.
- **Migration:** Yeni cari açılış tablosu dışında mevcut beta verisinde update/backfill yoktur. Şema ekleme migration’ı geri alınabilir; veri dönüşümü içermez.
- **Rollback:** Kod geri alınırsa yeni açılış özelliği durur; önceden var olan beta bakiyeleri etkilenmez. Yeni açılış kayıtlarını silmek yerine ters kayıtla iptal etmek kullanıcı verisini korur.
