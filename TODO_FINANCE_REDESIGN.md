# NoxStock Ticari Finans ve Cari TODO

Denetim kaynağı: [`docs/finance-and-current-account-audit.md`](docs/finance-and-current-account-audit.md).
P0 açılış ve mevcut hareket analizi: [`docs/p0-opening-balance-evolution-analysis.md`](docs/p0-opening-balance-evolution-analysis.md).

Bu plan **ön muhasebe ve stok yönetimi** sınırındadır. Genel muhasebe defteri, yevmiye, hesap planı, mizan, e-defter, beyanname, konsolidasyon ve profesyonel banka mutabakatı kapsam dışıdır. Kaynak kayıt ticari olaydır; sistem yalnız gerekli stok, cari ve kasa/banka hareketlerini üretir.

İşaretler: `[FIX]` mevcut hata düzeltmesi · `[HARDEN]` mevcut özelliği güvenli hâle getirme · `[EXTEND]` mevcut özelliğe küçük yetenek · `[MIGRATION]` şema/veri geçişi · `[NEW]` yeni özellik · `[DEFERRED]` ertelenen · `[OUT-OF-SCOPE]` kapsam dışı.

## P0 — Bakiye güvenliği ve mevcut hata düzeltmeleri

- [x] [FIX] **Çapraz dövizli kasa/banka aktarımını engelle**
  - Amaç: Kur/snapshot modeli yokken farklı currency hesaplara aynı nominal tutarın yazılmasını önlemek.
  - Etkilenen dosyalar: `Application/Accounts/AccountAppService.cs`, `Web/Pages/Accounts/TransferModal.cshtml`, localization, `AccountTransferAppServiceTests.cs`.
  - Backend/frontend: Aynı currency, aktif hesap, farklı hesap, pozitif tutar ve yeterli bakiye zorunlu; uyumsuz işlem Türkçe hata ile engellenir.
  - Migration: Hayır. Test/kabul: 2 test geçti; geçersiz işlem hiçbir bakiyeyi değiştirmez.
  - Bağımlılık: Yok.

- [x] [HARDEN] **Bağımsız tahsilat/ödeme güvenlik kurallarını tamamla**
  - Amaç: Tahsilatın gelir, ödemenin gider gibi ikinci kez sayılmasını ve yanlış cari/hesap etkisini önlemek.
  - Etkilenen dosyalar: `Application/BusinessPartners/BusinessPartnerAppService.cs`, `ProcessPaymentDto.cs`, `Web/Pages/Shared/PaymentModal.*`, testler.
  - Backend: Cari türü, aktif hesap, currency, açık bakiye ve ödeme hesabı yeterli bakiye kontrolleri; atomik UoW, operation key ve ters kayıt **tamamlandı**.
  - Frontend: Yalnız uyumlu aktif hesaplar, kullanılabilir bakiye, işlem sonrası cari/hesap önizlemesi ve kullanıcı dilindeki hata mesajları **tamamlandı**.
  - Migration: İdempotency/reversal alanları için evet.
  - Test/kabul: Pozitif/negatif, retry ve iptal senaryoları geçti; ikinci finansal etki oluşmuyor.
  - Bağımlılık: P0 iptal/işlem kaynağı modeli.

- [x] [FIX] [MIGRATION] **Cari kartındaki doğrudan bakiye düzenlemeyi kaldır; cari açılış işlemi ekle**
  - Amaç: `BusinessPartner.Balance` değerinin cari düzenleme modalından sessizce değişmesini engellemek.
  - Etkilenen dosyalar: `BusinessPartner`, DTO/mapping/app service, Customers/Suppliers modalı, yeni açılış işlem modeli ve testler.
  - Backend: Oluşturma/güncelleme payload `Balance` değeri uygulanmaz; yön, tarih, açıklama, durum, iptal ve ters kayıt taşıyan küçük kaynak model **tamamlandı**.
  - Frontend: Müşteri/tedarikçi listesi ve detayından açılış girilir; alacak/borç yönü ve işlem sonrası durum gösterilir; iptal nedeni zorunludur.
  - Migration: Evet; kaynak türü, durum ve iptal bağlantısı alanları.
  - Kabul kriteri: Cari kartı düzenleme bakiyeyi değiştiremez; açılış ters kayıtla iptal edilir.
  - Bağımlılık: P0 ortak kaynak/iptal altyapısı.

- [x] [FIX] [EXTEND] **Kasa/banka kartındaki doğrudan bakiye düzenlemeyi kaldır; hesap açılış işlemi ekle**
  - Amaç: `Account.Balance` değerinin hesap düzenleme formundan hareketsiz değişmesini engellemek.
  - Etkilenen dosyalar: `Account`, DTO/app service, `Pages/Accounts/*`, hareket modeli ve testler.
  - Backend: `CreateAsync`/`UpdateAsync` artık DTO `Balance` değerini uygulamaz; `CreateWithOpeningAsync` mevcut `AccountTransactionType.Opening` ile hesap ve açılışı tek UoW içinde oluşturur. Var olan hesap için özel API de eklendi.
  - Frontend: Yeni ve mevcut hesap açılış akışları, bakiye önizlemesi, iptal nedeni ve hareket durumu tamamlandı.
  - Migration: Hareket durum/iptal/bağlantı alanları için `CompleteP0FinanceSafety` migration'ı eklendi.
  - Test/kabul: Eski DTO bakiyesi yok sayılır; sıfır/negatif, pasif hesap ve ikinci açılış reddedilir; iptal ters kayıt üretir.

- [ ] [HARDEN] [EXTEND] **Ürün açılış stoğunu ürün kartından ayır**
  - Amaç: `Product.Quantity` alanının ürün düzenleme/import yoluyla hareketsiz değiştirilmesini durdurmak.
  - Etkilenen dosyalar: `ProductAppService`, import, `InventoryCountAppService`, `StockMovement`, ürün modalı ve testler.
  - Backend: Açılış stoğu `StockMovement` üretir; sayım mevcut bağımsız kaynak olarak korunur.
  - Frontend: Ürün bilgisi ile açılış stok işlemi ayrılır.
  - Migration: Hayır veya mevcut hareketlere kaynak/durum alanı eklenirse evet.
  - Kabul kriteri: Yeni stok bakiyesi her zaman açılış, onaylı belge, POS, sayım veya iade/transfer kaynağına dayanır.
  - Bağımlılık: P0 kaynak/iptal politikası.

- [x] [HARDEN] [MIGRATION] **Kesinleşmiş finansal işlemler için iptal/ters etki standardı ekle**
  - Amaç: Tahsilat, ödeme, diğer para girişi/çıkışı ve hesap aktarımının soft-delete olmadan geri alınması.
  - Etkilenen dosyalar: Accounts/BusinessPartners services ve modalları, `AccountTransaction`, permissions, localization, testler.
  - Backend: `Status`, `CancelledAt`, `CancelledBy`, `CancellationReason`, `OriginalTransactionId`/`ReversedTransactionId`; çift iptal red; aynı UoW içinde ters bakiye/hareket etkisi.
  - Frontend: “İptal et” gerekçe modalı ve ilgili özgün/ters işlem gösterimi.
  - Migration: Evet.
  - Kabul kriteri: Kesinleşmiş hareket silinmez; iptal sonrası bakiye tam önceki değerine döner.
  - Bağımlılık: Açılış işlemleriyle ortak kaynak modeli.

- [ ] [HARDEN] [MIGRATION] **Tüm P0 kaynak işlemlerinde idempotency ve atomiklik standardı uygula**
  - Amaç: Çift tıklama/retry nedeniyle mükerrer bakiye ve hareket oluşmasını önlemek.
  - Etkilenen dosyalar: DTO’lar, Accounts, BusinessPartners, Documents, POS, unique index/migration, testler.
  - Backend: İstemci operation key; tenant+operation key benzersizliği; kaynak, stok, cari ve hesap etkilerinin tek UoW içinde yazılması.
  - Frontend: Kaydet butonlarında gönderim kilidi ve retry-safe davranış.
  - Migration: Evet; operation key indeksleri ve legacy kayıt işaretleri.
  - Kabul kriteri: Aynı istek iki kez çalıştığında tek finansal/stok etkisi kalır; hata halinde kısmi kayıt kalmaz.
  - Bağımlılık: İptal kaynak modeli.

- [ ] [HARDEN] [MIGRATION] **Decimal ve yuvarlama denetimi ile geriye uyumlu migration planını tamamla**
  - Amaç: Para miktarlarında float kullanımı, gizli yuvarlama ve geçmiş veri kaybını engellemek.
  - Etkilenen dosyalar: EF konfigürasyonları, DTO/UI sayı formatlayıcıları, importlar, migration/runbook, testler.
  - Backend: Para `decimal`, oran `decimal`, kur hassasiyeti ve para birimi bazlı yuvarlama kuralı belgelenir.
  - Migration: Evet; önce backfill/uzlaştırma raporu, sonra yazma yolunun kapatılması. Geçmiş hareketler silinmez.
  - Kabul kriteri: Build/test geçer; eski bakiye ile yeni projection paralel doğrulamada eşleşir.
  - Bağımlılık: Yukarıdaki P0 kaynak işlemleri.

## P1 — İade ve stok bütünlüğü

- [ ] [NEW] [MIGRATION] **Satış iadesi ve alış iadesi kaynak belgelerini ekle**
  - Amaç: Fatura iptalinden farklı olarak kısmi/tarihsel iade yapılabilmesi.
  - Etkilenen dosyalar: Document enum/domain/workflow/UI, StockMovement, cari/hesap etkileri, testler.
  - Backend: İade kaynak faturaya bağlanır; satır/miktar sınırı, stok/cari/peşin hesap etkisi tek UoW içinde oluşur.
  - Frontend: “İade oluştur” aksiyonu, kaynak satır seçimi ve kısmi miktar girişi.
  - Migration: Evet (iade türü/kaynak bağları).
  - Kabul kriteri: İade stok ve cari etkisini bir kez üretir; fatura iptalinden bağımsızdır.
  - Bağımlılık: P0 iptal/idempotency tamamlanmış olmalı.

- [ ] [DEFERRED] [NEW] [MIGRATION] **Depo, raf ve depolar arası stok transferi için geçiş planını uygula**
  - Amaç: Raf bilgisini depo bakiyesinden ayırmak ve kaynak/hedef stok hareketini güvenli üretmek.
  - Etkilenen dosyalar: yeni Warehouse/WarehouseLocation/ProductWarehouseStock, StockMovement, ürün/stock UI, migration/testler.
  - Backend: Varsayılan depo stratejisi, `ProductWarehouseStock`, kaynak/hedef transferi tek transaction.
  - Frontend: Depo seçimi ve aktarım formu; raf yalnız depo altı konumdur.
  - Migration: Evet; mevcut `Product.Quantity` varsayılan depoya açılış stoğu olarak aktarılır, geçmiş hareketler legacy kalır.
  - Kabul kriteri: Depo toplamları eski ürün bakiyesiyle uyumludur; transfer toplam stoku değiştirmez.
  - Bağımlılık: P0 ürün açılış stoku.

## P2 — Basit belge ve ödeme eşleştirmesi

- [ ] [NEW] [MIGRATION] **Aynı para birimli `PaymentAllocation` ile belge kapatmayı ekle**
  - Amaç: Tahsilat/ödemenin hangi satış/alış faturasını kapattığını izlemek.
  - Etkilenen dosyalar: yeni `PaymentAllocation`, Documents/Payments services, modal ve cari detay, migration/testler.
  - Backend: Kısmi/çoklu tahsis, otomatik en eski vade önerisi, manuel belge seçimi; `PaidAmount`/kalan/durum kontrollü özet olur.
  - Frontend: Kullanıcı dostu “Faturaya ödeme bağla” bölümü; muhasebe terimi kullanılmaz.
  - Migration: Evet; geçmiş `PaidAmount` otomatik uydurulmaz, legacy/unmatched kalır.
  - Kabul kriteri: Bir ödeme çok faturaya dağıtılır; tahsis toplamı ödeme/fatura sınırını aşamaz.
  - Bağımlılık: P0 tamamlanmış olmalı.

- [ ] [DEFERRED] [EXTEND] [MIGRATION] **Eski cari virmanı güvenli Cari Mahsup akışına dönüştür**
  - Amaç: Bağımsız müşteri ve tedarikçi arasında bakiye aktarmak yerine yalnız aynı gerçek/tüzel kişinin karşılıklı açık pozisyonlarını mahsup etmek.
  - Etkilenen dosyalar: `BusinessPartnerTransfer`, `BusinessPartnerAppService`, `BusinessPartnerStatementAppService`, `Pages/BusinessPartnerTransfers/*`, Customers/CustomerDetail girişleri, yeni taraf ilişki/eşleştirme modeli ve testler.
  - Backend: Yeni mahsup, satış/alış açık kalemlerinden aynı ticari tarafı doğrular; mevcut `BusinessPartner` tek net bakiye taşıdığı için gerekli taraf bağlantısı/açık kalem modeli migration ile eklenir. Eski transferler silinmez ve legacy olarak görüntülenir.
  - Frontend: Kullanıcı adı “Cari Mahsup”; bağımsız tedarikçi seçici standart akışta yer almaz.
  - Migration: Evet; aynı taraf ilişkisi veya doğrulanmış kimlik bağı ile mahsup/allocation kaynak bağlantıları.
  - Kabul kriteri: Mahsup nakit hareketi yaratmaz, yalnız doğrulanmış aynı tarafın açık alacak/borçlarını azaltır ve ters kayıtla iptal edilebilir.
  - Bağımlılık: P2 `PaymentAllocation`; P0 iptal/idempotency.

## P3 — Basit avans

- [ ] [NEW] [MIGRATION] **Müşteri ve tedarikçi avanslarını ekle**
  - Amaç: Fazla tahsilat/ödemenin sessiz hata olmadan açık avans olarak tutulması.
  - Etkilenen dosyalar: payment DTO/service/UI, `PaymentAllocation`, cari detay, migration/testler.
  - Backend: “Avans olarak kaydet” seçeneği, açık avans bakiyesi, belgeye tahsis ve ters kayıt.
  - Frontend: Fazla tutarda “işlemi azalt / kalanını avans kaydet” seçeneği.
  - Migration: Evet.
  - Kabul kriteri: Avans yalnız kullanıcı seçerse oluşur ve sonradan uygun belgeye bağlanır.
  - Bağımlılık: P2 allocation.

## P4 — Basit vade, limit ve kullanıcı odaklı görünürlük

- [ ] [EXTEND] [MIGRATION] **Vade/limit görünürlüğü ve yetkili aşım akışı ekle**
  - Amaç: Günlük operasyonu destekleyen basit vade, yaşlandırma ve limit uyarıları.
  - Etkilenen dosyalar: BusinessPartner/Document DTO-service, Customers/Suppliers/POS/Invoices/Reports, migration/testler.
  - Backend: Varsayılan vade, 0–30/31–60/61–90/90+ yaşlandırma, limit override gerekçesi/yetkisi.
  - Frontend: Yaklaşan/gecikmiş tahsilat-ödeme ve anlaşılır satış engeli/uyarısı.
  - Migration: Evet.
  - Kabul kriteri: Yetkisiz kullanıcı limit aşımını onaylayamaz; kullanıcı yalnız gerekli ticari özeti görür.
  - Bağımlılık: P2 allocation.

- [x] [EXTEND] **Basit cari/hesap işlem geçmişini kullanıcı diliyle birleştir**
  - Amaç: Teknik ABP audit yerine “kim, ne zaman, ne yaptı” ticari geçmişi göstermek.
  - Etkilenen dosyalar: Customer/Supplier detail, Accounts, mevcut audit/hareket sorguları, testler.
  - Backend: Kaynak işlem referanslı ve tenant-izole timeline projeksiyonu.
  - Frontend: Tahsilat, ödeme, mahsup, iade, iptal ve belge ilişkisini sade etiketlerle gösterir.
  - Migration: Gerekirse kaynak bağları için evet.
  - Kabul kriteri: Kullanıcı teknik defter veya stack trace görmeden bakiye değişiminin nedenini anlayabilir.
  - Bağımlılık: P0 iptal, P2 allocation.

## Kapsam dışı / bilinçli ertelenen çalışmalar

- [ ] [OUT-OF-SCOPE] Genel muhasebe defteri, hesap planı, yevmiye, mizan, e-defter, beyanname, bordro ve konsolidasyon.
- [ ] [OUT-OF-SCOPE] Otomatik kur farkı muhasebesi, gelişmiş finansal enstrüman muhasebesi ve profesyonel banka entegrasyonlu mutabakat.
- [ ] [DEFERRED] P0 tamamlanmadan ödeme tahsisi, avans, iade, genişletilmiş vade/limit ve çok depo geliştirmesi.
