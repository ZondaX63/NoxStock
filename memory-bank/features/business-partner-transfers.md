# Müşteriden Tedarikçiye Cari Virman

Last updated: 2026-08-06

## Kapsam

Bir müşterinin NoxStock kullanıcısına olan açık borcunu, müşterinin doğrudan ödeme yaptığı tedarikçiye aktarır. Bu işlem nakit veya banka hareketi değildir.

## Muhasebe Kuralı

- Müşterinin pozitif alacak bakiyesi virman tutarı kadar azalır.
- Tedarikçinin negatif borç bakiyesi aynı tutar kadar sıfıra yaklaşır.
- Kasa/banka hesabı ve `AccountTransaction` kaydı oluşturulmaz.
- Müşteri ve tedarikçi para birimleri eşit olmalıdır.
- Tutar hem müşteri alacağını hem tedarikçi borcunu aşamaz.

## Denetim

- Her işlem `AppBusinessPartnerTransfers` tablosunda saklanır; referans numarası tenant içinde `VIR-yyyyMMdd-##` biçiminde tektir.
- Cari hareket ekranı işlem yönünü, karşı cariyi, açıklamayı ve referansı gösterir.
- Hareket silinmez/değiştirilmez; ters işlem için ileride ayrı, izlenebilir bir virman ters kayıt komutu tasarlanmalıdır.

## UI

- Müşteri listesindeki **Tedarikçiye virman yap** aksiyonu modalı açar.
- Modal sadece aynı para birimindeki açık borçlu tedarikçileri sunar.
- Müşteri ve tedarikçi create/edit modalları bölüm bazlı ortak bir tasarım kullanır.

## Migration

- `20260805211438_Add_Business_Partner_Transfers`
