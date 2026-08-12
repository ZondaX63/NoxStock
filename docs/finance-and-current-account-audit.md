# Noxstock Finans ve Cari Sistem Denetimi

Denetim tarihi: 2026-08-06  
Kapsam: Domain, Application, Contracts, EF Core, Razor Pages, izinler, migrationlar ve mevcut testler. Bu belge yalnızca repository'de bulunan yapıları raporlar; planlanan kabiliyetler mevcut kabul edilmemiştir.

> **Kapsam kararı (2026-08-06):** NoxStock bir stok ve ön muhasebe uygulamasıdır. Genel muhasebe defteri, hesap planı, yevmiye, mizan, e-defter, beyanname, bordro, konsolidasyon, otomatik kur farkı muhasebesi, banka entegrasyonlu profesyonel mutabakat ve çok aşamalı finans onay zincirleri bu planın dışındadır. Buradaki hareket modeli, yalnız ticari işlemlerin stok, cari ve kasa/banka etkisini güvenilir şekilde korumak içindir; kullanıcıya muhasebe fişi veya defter satırı oluşturma ekranı vermez.

## Kapsam ve Domain Kuralları Kararı

- **Cari mahsup:** Mevcut `BusinessPartnerTransfer` müşteri ile ayrı bir tedarikçi arasında bakiye aktarabilmektedir. Bu standart ticari akış olarak doğru değildir. Yeni standart akışın adı **Cari Mahsup** olacak ve yalnız aynı gerçek/tüzel kişinin müşteri ve tedarikçi pozisyonları arasında çalışacaktır. Mevcut kayıtlar silinmeyecek; yeni kayıt kuralı eklenmeden önce kullanım yerleri ve veri uyumluluğu ayrıca doğrulanacaktır.
- **Gelir/gider ≠ tahsilat/ödeme:** Satış/alış belgesi tahakkuku, tahsilat/ödeme ise nakit hareketidir. Tahsilat ikinci bir gelir; tedarikçi ödemesi ikinci bir gider üretmemelidir. Hesap ekranındaki `Income`/`Expense` işlemleri yalnız faturadan bağımsız diğer para girişi/çıkışı veya masraf amacıyla açıkça adlandırılmalıdır.
- **Kaynak işlem ilkesi:** Satış/alış faturası, POS, tahsilat, ödeme, hesap virmanı, masraf, açılış ve iade ana kayıttır. Stok/cari/kasa-banka hareketleri bu kaynaklardan sistemce üretilen sonuçlardır ve kullanıcı tarafından doğrudan düzenlenemez.
- **Finansal iptal:** Taslaklar silinebilir; bakiye etkisi oluşturmuş işlemler soft-delete ile yok edilmez. İptal durumu, iptal zamanı/kullanıcısı/nedeni ve kaynak–ters hareket bağlantısı tutulmalı; etki ters kayıtla sıfırlanmalıdır.
- **Avans güvenliği:** `PaymentAllocation` ve avans modeli kurulana kadar açık alacağı/borcu aşan tahsilat ve ödeme engellenmeye devam edecektir.

### Mevcut Cari Virman Kullanımı ve Uyumluluk Riski

- Servis: `src/NoxStock.Application/BusinessPartners/BusinessPartnerAppService.cs`, `TransferCustomerReceivableAsync`.
- Arayüz girişleri: `src/NoxStock.Web/Pages/Customers/Index.js` ve `src/NoxStock.Web/Pages/CustomerDetail/Index.js`.
- Modal: `src/NoxStock.Web/Pages/BusinessPartnerTransfers/CreateModal.*`; bugün müşteriyi kaynak alıp aynı currency ve açık borcu olan **herhangi bir** tedarikçiyi seçtirir.
- Geçmiş görünümü: `src/NoxStock.Application/BusinessPartners/BusinessPartnerStatementAppService.cs` ve `CustomerDetail/Index.js` işlemi “Cari virman” olarak gösterir.

`BusinessPartner` bir `Both` tipi taşısa da yalnız **tek net `Balance`** alanı vardır; aynı tüzel kişinin müşteri alacağı ve tedarikçi borcu ayrı açık pozisyonlar olarak tutulmaz. Bu nedenle doğru cari mahsup, mevcut net bakiye alanına basit bir `CustomerId == SupplierId` doğrulaması eklenerek çözülemez. P2 belge/eşleştirme modelinde kaynak satış/alış belgeleri üzerinden aynı ticari taraf doğrulaması ve mahsup tahsisi eklenmelidir. O zamana kadar mevcut virman kayıtları/verisi korunacak, ancak standart kullanıcı akışının bu işlem etrafında genişletilmesi yapılmayacaktır.

## Mevcut Mimari

- **Backend:** .NET 10, ASP.NET Core ve ABP Framework 10.4.1 katmanlı monolit. Katmanlar `src/NoxStock.Domain`, `Application.Contracts`, `Application`, `EntityFrameworkCore`, `HttpApi`, `Web` ve `DbMigrator` altındadır. `memory-bank/systemPatterns.md`.
- **Frontend:** ABP Razor Pages, jQuery, ABP ModalManager ve DataTables. Ortak layout/sidebar/topbar `src/NoxStock.Web/Pages/Shared`, `Components/AppSidebar` ve `Components/AppTopbar` altındadır. Sayfa durumunun kalıcı merkezi bir client-store'u yoktur; DataTable state'i tarayıcı tarafında saklanır.
- **Veri erişimi:** EF Core PostgreSQL sağlayıcısı ve ABP repository/UoW. Model yapılandırması `src/NoxStock.EntityFrameworkCore/EntityFrameworkCore/NoxStockDbContext.cs`; migrationlar `src/NoxStock.EntityFrameworkCore/Migrations` altındadır.
- **API:** `src/NoxStock.Web/NoxStockWebModule.cs` içindeki ABP conventional controller yapılandırması, `NoxStock.Application` app service'lerini HTTP API olarak açar. Repository'de finans için elle yazılmış ayrı controller yoktur; web sayfaları generated JavaScript proxy'lerini (`noxStock.accounts.account`, `noxStock.businessPartners.businessPartner`, vb.) çağırır.
- **Doğrulama:** DTO DataAnnotations ve app-service business exception'ları birlikte kullanılır. Finansal doğrulama kısmen servislerde bulunur; tüm giriş yollarında ortak bir finans kural motoru yoktur.
- **Yetkilendirme:** ABP permission policy'leri `src/NoxStock.Application.Contracts/Permissions/NoxStockPermissions.cs` ve `NoxStockPermissionDefinitionProvider.cs` altında. Ticari veriler tenant-scoped `IMultiTenant` entity'ler ile korunur; app-service'ler çoğunlukla `CurrentTenant.RequireTenantId()` çağırır.
- **Audit:** ABP Audit Logging EF entegrasyonu ve `AbpAuditLogs` altyapısı vardır. Host'a özel görüntüleme servisi `src/NoxStock.Application/AuditLogs/AuditLogAppService.cs`, kullanıcı odaklı ekranı `src/NoxStock.Web/Pages/AuditLogs/Index.cshtml`dir.
- **Test:** xUnit/ABP test katmanları `test/` altında; EF entegrasyon testlerinde SQLite kullanılır. Browser smoke testleri Playwright ile `test/NoxStock.BrowserTests` altındadır.

## Mevcut Veritabanı Modelleri

| Model | Durum | Gerçekte bulunan alanlar / ilişki | Kaynak |
|---|---|---|---|
| Kasa/banka hesabı | Var | `Account`: ad, tür, para birimi, doğrudan tutulan bakiye, aktiflik; full audit ve tenant | `Domain/Accounts/Account.cs` |
| Finans hareketi | Kısmen var | `AccountTransaction`: hesap, opsiyonel belge, tür, imzalı tutar, para birimi, tarih, açıklama, `OperationKey`; full audit ve tenant | `Domain/Accounts/AccountTransaction.cs` |
| Cari hesap | Var | `BusinessPartner`: müşteri/tedarikçi/her ikisi, iletişim/vergi alanları, doğrudan tutulan bakiye, kredi limiti, para birimi, 4 varsayılan satış iskontosu | `Domain/BusinessPartners/BusinessPartner.cs` |
| Cari virman | Var, riskli eski davranış | `BusinessPartnerTransfer`: müşteri, tedarikçi, referans, tutar, para birimi, tarih, açıklama. Kasa/banka hareketi değildir; bağımsız cari arasında aktarımı desteklediği için yeni standartta **Cari Mahsup** kuralına dönüştürülmelidir | `Domain/BusinessPartnerTransfers/BusinessPartnerTransfer.cs` |
| Ticari belge | Var | `Document`: cari, tür, durum, tutarlar, ödeme toplamı, para birimi/kur, belge ve vade tarihi, kaynak belge, operation key | `Domain/Documents/Document.cs` |
| Belge satırı | Var | miktar, birim fiyat, KDV, tek/4'lü alış ve satış iskontosu, maliyet/fiyat/kur snapshot alanları | `Domain/Documents/DocumentItem.cs` |
| Belge kur snapshot'ı | Var | belge-para birimi bazında `RateToTry`, `RateDate` | `Domain/Documents/DocumentExchangeRateSnapshot.cs` |
| Güncel kur | Var | Host-owned `ExchangeRate`, para birimi başına tek güncel oran | `Domain/ExchangeRates/ExchangeRate.cs` |
| Avans | Yok | Cari/belgeye bağlanmayan avans aggregate'i, allocation satırı veya açık kalem modeli bulunmuyor | repository taraması |
| Cari/belge eşleştirme | Yok | `Document.PaidAmount` toplamı vardır; ödeme ile belge arasında tahsis/eşleştirme satırı bulunmaz | `Domain/Documents/Document.cs`, `AccountTransaction.cs` |
| Banka mutabakatı | Yok | Banka ekstresi, statement line, eşleştirme veya reconciliation aggregate'i bulunmuyor | repository taraması |

EF Core hassasiyetleri: bakiye/tutarlar çoğunlukla `decimal(18,4)`, kur ve bazı fiyat snapshot'ları `decimal(18,6)`, oran/iskontolar `decimal(9,4)` olarak konfigüre edilmiştir. Bkz. `NoxStockDbContext.cs` satır 201-333.

## Mevcut API Uçları

ABP conventional controller üretimi nedeniyle bu liste, statik route tahmini değil, uygulama sözleşmesi/method düzeyindeki gerçek yüzeydir:

- Hesap CRUD, lookup, transfer, manuel hareket ve hareket listesi: `Application.Contracts/Accounts/IAccountAppService.cs`; uygulama: `Application/Accounts/AccountAppService.cs`.
- Salt-okunur finans hareket listesi: `IAccountTransactionAppService.cs` / `AccountTransactionAppService.cs`.
- Cari CRUD, tahsilat/ödeme ve müşteri-tedarikçi virmanı: `BusinessPartners/IBusinessPartnerAppService.cs` / `BusinessPartnerAppService.cs`.
- Cari ödeme geçmişi: `IBusinessPartnerStatementAppService.cs` / `BusinessPartnerStatementAppService.cs`.
- Belge CRUD/numara önerisi: `Documents/IDocumentAppService.cs` / `DocumentAppService.cs`.
- Belge onay, ödeme kaydı ve iptal: `IDocumentWorkflowAppService.cs` / `DocumentWorkflowAppService.cs`.
- POS ürün/müşteri/checkout lookup, satış ve iptal: `Sales/IPosSaleAppService.cs` / `Sales/PosSaleAppService.cs`.
- Finans/operasyon özeti: `Reports/IReportingAppService.cs` / `Reports/ReportingAppService.cs`.
- Denetim özeti: `AuditLogs/AuditLogAppService.cs`; host-onlydir.

## Mevcut Ekranlar

| Ekran | Durum | Kaynak |
|---|---|---|
| Kasa ve bankalar listesi, hesap oluşturma/düzenleme, gelir-gider, hesaplar arası transfer, hareket modalı | Var | `Web/Pages/Accounts/*` |
| Müşteriler listesi ve oluştur/düzenle modalı | Var | `Web/Pages/Customers/*` |
| Tedarikçiler listesi ve oluştur/düzenle modalı | Var | `Web/Pages/Suppliers/*` |
| Müşteri detay/cari hareket geçmişi | Kısmen var | `Web/Pages/CustomerDetail/*`; ödeme geçmişi ve virman gösterir, tam cari ekstre değildir |
| Tahsilat/ödeme modalı | Var, basit | `Web/Pages/Shared/PaymentModal.*`, `Pages/Payments/CreateModal.*` |
| Cari virman modalı | Var | `Web/Pages/BusinessPartnerTransfers/CreateModal.*` |
| Fatura listesi, giriş, yazdırma, belge ödeme modalı | Var | `Web/Pages/Invoices/*` |
| Perakende/POS | Var | `Web/Pages/Sales/*` |
| Finans/operasyon raporu | Var, özet | `Web/Pages/Reports/*` |
| Denetim kayıtları | Var, host-only | `Web/Pages/AuditLogs/*` |
| Cari mutabakat, vade takvim/aksiyon merkezi, avans yönetimi, banka ekstre mutabakatı | Yok | repository taraması |

## Çalışan Fonksiyonlar

- [Var] Satış/alış faturası onayında stok, cari bakiye ve opsiyonel hesap hareketi tek workflow içinde işlenir; iptal ters stok/hesap/cari hareketleri üretir. `Application/Documents/DocumentWorkflowAppService.cs`.
- [Var] Kısmi belge tahsilat/ödemesi `Document.PaidAmount` ve `AccountTransaction` ile işlenir; `OperationKey` ile belge ödeme retry koruması vardır. Aynı dosya.
- [Var] POS nakit/banka tahsilatı veya müşteri veresiye satışını stok, cari ve hesap hareketleriyle yürütür; POS iptali ters kayıt üretir. `Application/Sales/PosSaleAppService.cs`.
- [Var] Kasa/banka için diğer para girişi/çıkışı, masraf niteliğinde çıkış ve aynı nominal para birimindeki transfer hareketleri; hesap hareket geçmişi. `Application/Accounts/AccountAppService.cs`. Bu hareketlerin satış geliri veya alış gideriyle raporda çift sayılmaması ayrıca doğrulanmalıdır.
- [Var] Cari kredi limiti satış faturası, alış faturası ve POS veresiye akışlarında kısmen kontrol edilir. `DocumentWorkflowAppService.EnsureBusinessPartnerCreditLimitAsync`, `PosSaleAppService.CreateAsync`.
- [Var, eski davranış] Müşteri alacağını aynı para birimindeki tedarikçi borcuna kapatan, kasa/bankasız aktarım. `BusinessPartnerAppService.TransferCustomerReceivableAsync`. Bu, yalnız aynı tüzel kişi için **Cari Mahsup** olacak şekilde daraltılmalıdır; bağımsız cariler arası yeni aktarım standart akışta sunulmamalıdır.
- [Var] Belge tarihine bağlı kur snapshot'ı ve satış/maliyet snapshot'ları. `DocumentAppService.RefreshExchangeRateSnapshotsAsync`, `DocumentWorkflowAppService.LockSalesPricingSnapshotsAsync`.
- [Var] ABP full auditing ile `CreationTime`, `CreatorId`, `LastModificationTime`, `LastModifierId`, silme audit alanları finans/cari aggregate'lerinde mevcuttur.

## Eksik Fonksiyonlar

- [Yok] Cariyi oluşturan her olayı tek, append-only, belge/ödeme/virman/avans tahsislerini kapsayan cari hareket defteri.
- [Yok] Avans alma/verme, avansı belgeye tahsis etme, açık avans bakiyesi ve ters kayıtları.
- [Yok] Çoklu belgeyi tek tahsilat/ödemeye, tek belgeyi birden fazla tahsilat/ödemeye bağlayan `PaymentAllocation` eşleştirmesi.
- [Yok] Fatura/cari vade planı, taksit, gecikme faizi, tahsilat vaatleri ve kullanıcı aksiyon listesi.
- [Yok] Cari mutabakat talebi, dönem bakiyesi, karşı onay/itiraz, PDF/e-posta kaydı.
- [Yok] Banka ekstresi içe aktarma, otomatik eşleştirme ve banka mutabakatı.
- [Yok] Dövizli hesaplar arası transfer için kaynak/hedef tutarı, işlem kuru, masrafı ve kur farkını saklayan model.
- [Yok] Cari virmanın ters kaydı/iptali; transfer immutable görünse de geri alma komutu yoktur.
- [Yok] Finans hareketleri için bağımsız, kullanıcı dostu ters kayıt komutu ve kaynak hareket bağlantısı.
- [Yok] Ayrı cari risk seviyesi, vade aşım riski, limit override yetkisi/açıklaması ve onay akışı.
- [Yok] Hesap bazında açılış bakiyesini hareket olarak oluşturan zorunlu akış; açılış doğrudan `Account.Balance` ile yazılabiliyor.

## Hatalı veya Riskli İş Mantıkları

1. **Giderildi (P0) — çapraz döviz transferi:** `AccountAppService.TransferAsync` artık farklı para birimlerini, pasif hesabı, aynı hesabı, sıfır/negatif tutarı ve yetersiz bakiyeyi reddeder. Dövizli transfer, ayrı kaynak/hedef tutar ve kur snapshot modeli olmadan desteklenmeyecektir. Kaynak: `Application/Accounts/AccountAppService.cs`.
2. **Kritik — açılış bakiyeleri hareketsiz oluşturulabiliyor.** Cari güncellemesinde gelen `Balance` artık korunur/uygulanmaz; ancak yeni `BusinessPartner` ve `Account` yaratılırken DTO bakiyesi doğrudan entity'ye yazılabiliyor. Açılış işlemi/hareketi ve ters kayıt bağlantısı henüz yoktur. Kaynak: `BusinessPartnerAppService.cs`, `AccountAppService.cs`, ilgili DTO'lar.
3. **Kısmen giderildi (P0) — doğrudan tahsilat/ödeme:** `ProcessPaymentAsync` artık partner türü, aktif hesap, hesap-cari para birimi ve mevcut açık alacak/borç kontrolleri yapar; atomik UoW içindedir. İşlem anahtarı/idempotency, belge tahsisi ve avans bağlantısı henüz yoktur. Kaynak: `Application/BusinessPartners/BusinessPartnerAppService.cs`.
4. **Yüksek — tam cari ekstre yerine ödeme geçmişi sunuluyor.** `BusinessPartnerStatementAppService.GetPaymentHistoryAsync` yalnız belge bağlı hesap hareketleri ile virmanı birleştiriyor; fatura tahakkukları, bakiye başlangıcı, bağımsız tahsilat/ödeme ve açık kalem eşleştirmesi tam olarak görünmüyor. Kaynak: `Application/BusinessPartners/BusinessPartnerStatementAppService.cs`.
5. **Yüksek — transfer/manuel finans hareketleri için idempotency ve ters kayıt standardı yok.** Belge ödemesinde operation key vardır; `TransferDto` ve `AddTransactionDto`nda yoktur. Hesap transferi geri alınamaz ve bağımsız gelir/gider ters kaydı API'si yoktur. Kaynak: `Accounts/*.cs`.
6. **Orta — rapor para birimlerini tek ekonomik ölçüye güvenli biçimde dönüştürmüyor.** Rapor belgeleri `Document.ExchangeRate` ile TRY'ye çevirirken hesap/cari bakiye ve akışları kendi currency alanıyla sunar; TRY dışı hesap/cari toplamları karışabilir. Kur farkı ve işlem snapshot'ı rapora taşınmaz. Kaynak: `Application/Reports/ReportingAppService.cs`.
7. **Orta — finans kayıtları immutable değildir.** `AccountTransaction` full-audited/soft-deletable entity'dir; kaynak kayıtları ve ters kayıt bağını zorunlu kılan domain kuralı yoktur. Kullanıcı arayüzü update sunmuyor olsa da model bu garantiye sahip değildir.
8. **Orta — kredi limiti yalnız seçili tahakkuk anlarında uygulanır.** Vade aşımı, açık avans, manuel cari hareket, limit override ve risk sınıflaması yoktur. Kaynak: `DocumentWorkflowAppService.cs`, `PosSaleAppService.cs`.
9. **Orta — cari virman referansı yarış koşuluna açıktır.** `CountAsync + 1` referans üretiyor; unique index hata ile korusa da eşzamanlı iki talepte kullanıcı dostu retry yoktur. Kaynak: `BusinessPartnerAppService.CreateTransferReferenceAsync`.

## Veri Tutarlılığı Riskleri

- `Account.Balance` ve `BusinessPartner.Balance` current projection ile hareket kayıtlarının aynı işlemde tekrar hesaplanıp uzlaştırılmasını sağlayan servis/mutabakatsız tutuluyor.
- Kur bilgisi belge snapshot'ında vardır; fakat hesap transferi, doğrudan tahsilat/ödeme ve cari virman için işlem kuru/snapshot alanları bulunmaz.
- Ödeme `Document.PaidAmount` toplamına dayanır; satır bazında hangi tahsilatın hangi faturayı kapattığı saklanmaz.
- Soft-delete edilmiş finans hareketleri veya doğrudan bakiye editleri, rapor ile hesap bakiyesinin ayrışmasına yol açabilir.
- `BusinessPartner` tek `Currency` alanına sahiptir; aynı cari için döviz bazlı ayrı bakiye yaşlandırması yoktur.

## UI/UX Problemleri

- Kasa/banka ekranı ham hareket türleri ve sayısal bakiyeyi gösterir; kullanıcıya işlemin ticari anlamını, kaynak belgesini ve ters kayıt ilişkisini bütünlüklü sunmaz. `Web/Pages/Accounts/Index.js`.
- Müşteri ve tedarikçi ayrı listelerde olsa da ortak cari görünüm, risk/vade filtresi ve açık kalem drill-down yoktur. `Pages/Customers`, `Pages/Suppliers`.
- Cari detay yalnız müşteri için bulunuyor; tedarikçi eşdeğer genel cari detay/ekstre ekranı yoktur.
- Tahsilat/ödeme modalında belge seçimi, avans seçeneği, kalan açık tutar, limit/vade uyarısı ve işlem önizlemesi yoktur. `Pages/Shared/PaymentModal.*`.
- Cari virman modalı yalnız müşteri listesinden/doğrudan müşteri detayından başlatılır; işlem sonrası kapsamlı iki taraflı ekstre/kapama görünümü yoktur.
- Raporlar TL eşdeğeri ile para birimi bazlı değerleri aynı sayfada ayırt edici bir konsolidasyon politikası olmadan gösterir. `Pages/Reports/Index.cshtml`.

## Güvenlik ve Yetkilendirme Problemleri

- **Var:** Accounts, BusinessPartners, Documents ve Sales izinleri ayrı tanımlanmış; virman için `BusinessPartners.Transfer`, hesap virmanı için `Accounts.Transfer`, belge onay/iptal için ayrı izinler var.
- **Kısmen var:** Kredi limiti aşımı için override yetkisi/açıklaması/audit kaydı bulunmuyor; finans ayrımı (tahsilat, ödeme, virman, iptal, mutabakat) için daha dar yetki setleri gerekli.
- **Kısmen var:** `AuditLogAppService` yalnız host context'te açılıyor. Tenant kullanıcılarının kendi ticari işlem zaman çizelgesini güvenli/kısıtlı görmesi için ayrı projection yok.
- **Yok:** Onay limiti, dört göz kontrolü, ödeme/virman iptal ayrımı, dönem kilidi ve finansal kapanış yetkilendirmesi.

## Migration Gerektiren Değişiklikler

1. Genel muhasebe defteri değil, kaynak ticari işlem bağlantılı finans/cari hareket modeli: kaynak tür/id, yön, tutar, currency, tarih, durum, iptal/ters kayıt bağlantısı, açıklama ve operation key.
2. `PaymentAllocation` ile tahsilat/ödeme/avansın belge açık kalemlerine tahsisi.
3. `Advance` veya financial operation üzerinde açık avans state/projection ve allocation ilişkisi.
4. Dövizli hesap transferi için kaynak/hedef tutar, kur, masraf, TRY eşdeğeri ve kur farkı alanları.
5. Vade/risk alanları ve risk event/override kayıtları.
6. Bank statement/reconciliation header-line-match tabloları.
7. Tenant-first ve tarih/sorgu odaklı indeksler; eşzamanlı referans/operation key benzersizliği.

## P0 Bakiye Yazma Noktaları ve Atomiklik

Mevcut kaynak taramasında doğrudan `Balance` veya `Product.Quantity` yazan uygulama noktaları aşağıdadır. P0 çalışması bu noktaları tek seferde büyük bir defter rewrite'ı yapmadan, küçük transaction-safe kaynak işlem komutlarına taşımayı hedefler.

| Alan | Mevcut yazma noktası | Mevcut durum | P0 hedefi |
|---|---|---|---|
| `Account.Balance` | `Application/Accounts/AccountAppService.cs` (`TransferAsync`, `AddTransactionAsync`) | Transfer UoW içindedir; manuel hareket ve CRUD açılış bakiyesini doğrudan değiştirir | Açılış ve diğer para hareketi ayrı kaynak işlem olsun; kesinleşmiş hareket ters kayıtla iptal edilsin |
| `Account.Balance` | `Application/BusinessPartners/BusinessPartnerAppService.cs` (`ProcessPaymentAsync`) | Tahsilat/ödeme UoW içindedir; temel kontroller eklenmiştir | İşlem anahtarı ve ters kayıt bağını ekle |
| `Account.Balance` | `Application/Documents/DocumentWorkflowAppService.cs`, `Application/Sales/PosSaleAppService.cs` | Belge/POS kaynağından üretilir; onay/iptalde ters etki vardır | Aynı davranışı koru, idempotency ve kaynak bağlantısını standartlaştır |
| `BusinessPartner.Balance` | `BusinessPartnerAppService.cs`, `DocumentWorkflowAppService.cs`, `PosSaleAppService.cs` | Ticari kaynaklardan ve cari aktarımından değişir; CRUD DTO'su da doğrudan yazabilir | Cari kartı düzenlemesinden çıkar; açılış veya yetkili uyarlama işlemi ile yönet |
| `Product.Quantity` | `DocumentWorkflowAppService.cs`, `PosSaleAppService.cs`, `InventoryCountAppService.cs`, `ProductAppService.cs` | Onaylı fatura/POS ve sayım hareket üretir; ürün import/düzenleme doğrudan miktar yazabilir | Açılış stoğu/sayım kaynak işlem olarak ayrıştır; ürün kartında doğrudan stok bakiyesi düzenlemeyi kaldır |

**Atomiklik durumu:** Belge onay/iptal, POS satış/iptal, tahsilat/ödeme ve hesap virmanı UoW ile çalışmaktadır. P0'da manuel para hareketi, açılış ve iptal komutları da aynı UoW ve idempotency kuralına alınacaktır. Hata halinde transaction rollback ile stok, cari, hesap ve kaynak kaydın birlikte geri alınması kabul kriteridir.

## Stok Kesinleşme Kuralları: Mevcut ve Hedef

| Kural | Mevcut davranış | Hedef/P0-P1 kararı |
|---|---|---|
| Teklif | `Offer` türü onaylı stok workflow'una alınmaz; stok etkisi yok | Aynen korunur |
| Sipariş | `SaleOrder`/`PurchaseOrder` stok workflow'una alınmaz; fiziksel stok etkisi yok | Aynen korunur; rezervasyon şu an yoktur ve ayrıca planlanmalıdır |
| Taslak fatura | `DocumentWorkflowAppService.ApproveAsync` çağrılana kadar stok etkisi yok | Aynen korunur |
| Onaylı satış faturası | `Product.Quantity` düşer ve `StockMovement(SalesInvoice)` oluşur | Aynen korunur |
| Onaylı alış faturası | `Product.Quantity` artar ve `StockMovement(PurchaseInvoice)` oluşur | Aynen korunur |
| POS | Satışta stok düşer; iptalde `StockMovement(Reversal)` ve ters miktar etkisi vardır | Aynen korunur |
| Fatura iptali | Mevcut stok hareketleri ters hareketle geri alınır | Aynen korunur; iptal gerekçesi/kaynak bağlantısı P0'da standardize edilir |
| Kesinleşmiş belge satırı değişikliği | Onay/ödenmiş belge güncellemeleri kısmen kısıtlıdır; kural tüm giriş noktalarında tekleştirilmemiştir | Kesinleşmiş belge satırları sessizce değişmez; iptal+yeniden düzenleme/revizyon gerekir |
| İade | Ayrı belge türü ve akış yok | P1'de satış/alış iadesi ayrı kaynak belge olarak eklenecek |
| Depolar arası transfer | Depo ve kaynak/hedef stok modeli yok | P1 sonrası, kaynak/hedef hareketlerini tek transaction'da üretir |

## Tek Depo, Raf ve Çok Depoya Geçiş Stratejisi

- **Mevcut gerçek:** Stok bakiyesi `Product.Quantity` üzerindedir. `StockMovement` yalnız `ProductId`, miktar, tür, belge ve açıklama taşır; `WarehouseId`/`LocationId` yoktur. Ürün kartındaki raf/konum alanı tek depo varsayımında açıklayıcı metindir, depo bakiyesi değildir.
- **Şimdiki karar:** P0 sırasında çok depo rewrite'ı yapılmayacak.
- **Gelecek migration stratejisi:** `Warehouse` (tenant başına bir varsayılan depo), `WarehouseLocation` ve `ProductWarehouseStock(ProductId, WarehouseId, Quantity, ReservedQuantity)` eklenir. Mevcut her ürünün `Product.Quantity` değeri varsayılan depoya bir defalık açılış stoğu hareketiyle aktarılır. Geçmiş `StockMovement` kayıtları uydurulmuş depo tarihiyle yeniden yazılmaz; `LegacyDefaultWarehouse` bağlamında korunur. Paralel toplam doğrulaması tamamlanmadan `Product.Quantity` kaldırılmaz.

## Geriye Dönük Uyumluluk Riskleri

- Mevcut `Balance` alanlarını aniden kaldırmak geçmiş veri/raporları bozar. Önce ledger/projection backfill, paralel tutarlılık kontrolü, sonra yazma yolunu kapatma gerekir.
- Geçmiş `AccountTransaction` kayıtlarında kaynak işlem, TRY snapshot, allocation veya reversal linki olmayabilir; migration bunları uydurmamalı, "legacy/unmatched" olarak işaretlemelidir.
- `Document.PaidAmount` geçmiş belgelerde tek doğru kapama bilgisi olabilir; allocation backfill belgesiz tahsilatları otomatik eşleştirmemelidir.
- Cari tek-currency varsayımını çok-currency open-item modeline geçirmek UI ve rapor toplamlarını etkiler; mixed currency toplamı yasaklanmalı veya rapor tarihi/kur politikası açıkça seçilmelidir.
- Yeni finans hareketi defteri eklenirken mevcut ABP full-audit alanları korunmalı, fakat kullanıcıya teknik audit yerine ticari işlem günlüğü gösterilmelidir.

## Önerilen Hedef Mimari

1. **Kullanıcı niyeti komutları:** "Müşteriden tahsil et", "tedarikçiye ödeme yap", "hesaplar arası aktar", "cari mahsup", "diğer para girişi/çıkışı", "masraf kaydı", "açılış bakiyesi gir", "belgeleri kapat", "işlemi iptal et". UI yalnız işlem özeti, etki önizlemesi ve gerekli ticari bilgiyi gösterir.
2. **Kaynak ticari işlem, sistem üretimli hareket:** Satış/alış faturası, POS, tahsilat, ödeme, hesap aktarımı, masraf, açılış ve iade ana kayıttır. Bunlardan sistemin ürettiği `AccountTransaction`, cari hareketi ve stok hareketi kullanıcı tarafından doğrudan düzenlenmez. Bu, genel muhasebe defteri değildir.
3. **Bakiye projection'ı:** `Account.Balance`, `BusinessPartner.Balance` ve `Product.Quantity` performans amaçlı özetlerdir. Yeni/değişen bakiye yalnız kaynak işlem servisinden, aynı transaction içinde güncellenir; açılış ve uyarlama da kaynak işlem olarak izlenir.
4. **Basit belge-ödeme eşleştirmesi:** Fatura tahakkuku açık tutar üretir. Tahsilat/ödeme/avans, P2'de `PaymentAllocation` ile aynı para birimindeki belgelere bağlanır. `PaidAmount`, `RemainingAmount` ve ödeme durumu kontrol edilen özet alanlarıdır.
5. **Ters kayıt:** Kesinleşmiş finansal hareket soft-delete edilmez. İptal, durum/iptal nedeni/kullanıcısı ve özgün–ters işlem bağlantısıyla etkiyi tersine çevirir.
6. **Cari mahsup:** Standart mahsup yalnız aynı gerçek/tüzel kişi üzerindeki müşteri ve tedarikçi bakiyeleri arasında yapılır; bağımsız cariler arası bakiye aktarımı standart özellik değildir.
7. **Döviz politikası:** Belge para birimi ve kur snapshot'ı korunur. Dövizli hesap transferi ancak kaynak/hedef tutar ve işlem kuru modeli ayrıca kurulduğunda açılır; P0'da engellenir.
8. **UX:** Ortak Cari Detay ekranı; bakiye, son ticari işlemler, belge kapatma durumu, vade/limit uyarısı ve sade tahsilat/ödeme/mahsup aksiyonları sunar.
