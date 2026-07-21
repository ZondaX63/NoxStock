const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

(async () => {
  console.log('Google Chrome (Chromium) başlatılıyor... Ekranınızda tarayıcı penceresi açılacaktır.');
  
  // Headless: false ve slowMo ile kullanıcının izlemesini kolaylaştırıyoruz
  const browser = await chromium.launch({ 
    headless: false,
    slowMo: 1500 // Adımların yavaşça görünmesi için 1.5 saniye bekleme
  });
  
  const context = await browser.newContext({
    baseURL: 'http://localhost:8080',
    viewport: { width: 1280, height: 800 },
    ignoreHTTPSErrors: true
  });
  const page = await context.newPage();

  try {
    console.log('1. Giriş sayfasına gidiliyor...');
    await page.goto('/t/asd/Account/Login');
    await page.waitForSelector('#LoginInput_UserNameOrEmailAddress');
    await page.locator('#LoginInput_UserNameOrEmailAddress').fill('asd');
    await page.locator('#LoginInput_Password').fill('asdasd');
    
    console.log('Giriş yapılıyor...');
    await Promise.all([
      page.waitForURL(url => !url.pathname.endsWith('/Account/Login')),
      page.getByRole('button', { name: /Giriş yap|Log in|Sign in/i }).click()
    ]);
    console.log('Giriş başarılı.');

    // 2. Yeni Ürün Oluşturuluyor
    console.log('2. Ürünler sayfasına geçiliyor...');
    await page.goto('/t/asd/Products');
    await page.waitForLoadState('networkidle');

    // Eğer editor panel açıksa kapat
    const closeBtn = page.locator('#CloseProductEditor');
    if (await closeBtn.isVisible()) {
      await closeBtn.click();
    }

    console.log('Yeni ürün ekleme formu açılıyor...');
    await page.locator('#NewProductButton').click();
    await page.waitForSelector('#ProductEditor form:visible');

    const runId = Date.now().toString(36).toUpperCase();
    const sku = `YAG-${runId}`;
    const name = `Yağ Pompası X5 ${runId}`;

    await page.locator('#Product_Name').fill(name);
    await page.locator('#Product_Sku').fill(sku);
    await page.locator('#Product_Barcode').fill(`869${Date.now().toString().slice(-10)}`);
    await page.locator('#Product_PurchasePrice').fill('25');
    await page.locator('#Product_PurchaseCurrency').selectOption('USD');
    await page.locator('#Product_SaleCurrency').selectOption('TRY');
    await page.locator('#Product_ProfitMarkupPercent').fill('30');
    
    console.log('Dinamik fiyatlandırma moduna alınıyor...');
    await page.locator('[data-pricing-mode-picker] [data-pricing-mode="1"]').click();
    
    await page.waitForTimeout(1000);

    // Formu gönder
    console.log('Ürün kartı oluşturuluyor...');
    await Promise.all([
      page.waitForNavigation(),
      page.locator('.product-editor-submit').click()
    ]);
    console.log('Ürün oluşturuldu. SKU:', sku);

    // 3. Alış faturası oluştur
    console.log('3. Alış faturası sayfasına geçiliyor...');
    await page.goto('/t/asd/Invoices/Create?type=2'); // Purchase Invoice = 2
    await page.waitForLoadState('networkidle');

    const invDocNumber = `AF-${runId}`;
    await page.locator('#InvoiceDocNumber').fill(invDocNumber);

    // Cari hesap seç (İlk tedarikçiyi seçelim)
    console.log('Cari hesap ve ürün ekleniyor...');
    const partnerSelect = page.locator('#Invoice_BusinessPartnerId');
    await partnerSelect.selectOption({ index: 1 });

    // Satır ekle ve ürünü seç
    await page.locator('.product-select').first().selectOption({ label: name });
    await page.locator('.quantity-input').first().fill('10');
    await page.locator('.price-input').first().fill('1000'); // 1000 TRY birim alış fiyatı

    await page.waitForTimeout(2000); // Önizlemenin görünmesi için bekleme

    // Faturayı kaydet
    console.log('Alış faturası taslak olarak kaydediliyor...');
    await Promise.all([
      page.waitForURL(url => url.pathname.endsWith('/Invoices')),
      page.getByRole('button', { name: 'Belgeyi Kaydet' }).click()
    ]);

    // Faturayı onayla
    console.log('Fatura onaylanıyor...');
    const row = page.locator('#InvoicesTable tbody tr').filter({ hasText: invDocNumber });
    await row.locator('button.dropdown-toggle').click();
    await row.getByText('Onayla', { exact: true }).click();
    await page.waitForTimeout(2000);

    // 4. Ürün detayına girip maliyet geçmişini ve yeni öneriyi doğrula
    console.log('4. Ürün sayfasına geri dönülüyor...');
    await page.goto('/t/asd/Products');
    await page.waitForLoadState('networkidle');
    
    await page.locator('#ProductSearch').fill(sku);
    await page.waitForTimeout(1000);
    
    console.log('Ürün kartı detayları açılıyor...');
    const productRow = page.locator('#ProductsTable tbody tr').filter({ hasText: sku });
    await productRow.locator('.products-action-edit').click();
    
    await page.waitForSelector('#ProductEditForm:visible');
    await page.waitForTimeout(4000); // Kullanıcının kart detayını ve döviz kur hesaplamasını görmesi için bekletme

    // Modalı kapat
    await page.locator('#ProductEditForm button[data-bs-dismiss="modal"]').click();

    // 5. POS ekranına git
    console.log('5. POS sayfasına geçiliyor...');
    await page.goto('/t/asd/Sales');
    await page.waitForLoadState('networkidle');

    await page.locator('#PosSearch').fill(sku);
    await page.waitForTimeout(1000);

    // Sepete ekle
    console.log('Ürün sepete ekleniyor...');
    await page.locator('.pos-product-card').filter({ hasText: sku }).click();
    await page.waitForTimeout(1000);

    // Detay panelini aç
    console.log('POS fiyat hesabı detayları gösteriliyor...');
    await page.locator('.pos-price-detail-toggle').click();
    await page.waitForTimeout(3000); // İnceleme için bekletme

    // Satışı tamamla
    console.log('Satış tamamlanıyor...');
    await page.locator('#PosAccount').selectOption({ index: 1 });
    await page.locator('#CompletePosSaleButton').click();
    await page.waitForTimeout(3000); // Başarı bildirimini görmek için bekletme

    console.log('Tüm akış başarıyla test edildi!');

  } catch (error) {
    console.error('Hata oluştu:', error);
  } finally {
    await browser.close();
    console.log('Tarayıcı kapatıldı.');
  }
})();
