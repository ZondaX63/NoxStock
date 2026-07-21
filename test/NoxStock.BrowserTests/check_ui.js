const { chromium } = require('playwright');
const path = require('path');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1280, height: 800 }
  });
  const page = await context.newPage();

  try {
    console.log('1. Host Admin login sayfasına gidiliyor...');
    await page.goto('http://localhost:8080/Account/Login');
    await page.waitForSelector('#LoginInput_UserNameOrEmailAddress');
    await page.locator('#LoginInput_UserNameOrEmailAddress').fill('admin');
    await page.locator('#LoginInput_Password').fill('Aa123!');
    
    console.log('Host Admin giriş yapılıyor...');
    await Promise.all([
      page.waitForURL(url => !url.pathname.endsWith('/Account/Login')),
      page.getByRole('button', { name: /Giriş yap|Log in|Sign in/i }).click()
    ]);

    console.log('2. Tenant listesine gidiliyor...');
    await page.goto('http://localhost:8080/TenantManagement/Tenants');
    await page.waitForTimeout(1000);
    await page.screenshot({ path: '/home/zondax/.gemini/antigravity/brain/3422beb1-ae25-4859-a0d9-b53db9c7f8d3/.tempmediaStorage/tenants_list.png' });

    // Host session'dan çıkış yap
    await page.goto('http://localhost:8080/Account/Logout');

    // Yeni context
    const tenantContext = await browser.newContext({
      viewport: { width: 1280, height: 800 }
    });
    const tenantPage = await tenantContext.newPage();

    console.log('3. Tenant Giriş...');
    await tenantPage.goto('http://localhost:8080/t/asd/Account/Login');
    await tenantPage.waitForSelector('#LoginInput_UserNameOrEmailAddress');
    await tenantPage.locator('#LoginInput_UserNameOrEmailAddress').fill('asd');
    await tenantPage.locator('#LoginInput_Password').fill('asdasd');
    
    await Promise.all([
      tenantPage.waitForURL(url => !url.pathname.endsWith('/Account/Login')),
      tenantPage.getByRole('button', { name: /Giriş yap|Log in|Sign in/i }).click()
    ]);
    console.log('Tenant Giriş başarılı.');

    console.log('4. Products...');
    await tenantPage.goto('http://localhost:8080/t/asd/Products');
    await tenantPage.waitForTimeout(2000);
    await tenantPage.screenshot({ path: '/home/zondax/.gemini/antigravity/brain/3422beb1-ae25-4859-a0d9-b53db9c7f8d3/.tempmediaStorage/products_page.png' });

    // Yeni ürün ekleme modalı açıp screenshot alalım
    const newProductButton = tenantPage.locator('#NewProductButton');
    if (await newProductButton.count() > 0) {
      await newProductButton.click();
      await tenantPage.waitForTimeout(1500);
      await tenantPage.screenshot({ path: '/home/zondax/.gemini/antigravity/brain/3422beb1-ae25-4859-a0d9-b53db9c7f8d3/.tempmediaStorage/new_product_modal.png' });
    }

    console.log('5. Purchase Invoices Listesi...');
    await tenantPage.goto('http://localhost:8080/t/asd/Invoices');
    await tenantPage.waitForTimeout(2000);
    await tenantPage.screenshot({ path: '/home/zondax/.gemini/antigravity/brain/3422beb1-ae25-4859-a0d9-b53db9c7f8d3/.tempmediaStorage/invoices_page.png' });

    // Alış faturası oluşturma ekranına gidelim
    console.log('Purchase Invoice Create sayfasına gidiliyor...');
    await tenantPage.goto('http://localhost:8080/t/asd/Invoices/Create?type=Purchase');
    await tenantPage.waitForTimeout(2000);
    await tenantPage.screenshot({ path: '/home/zondax/.gemini/antigravity/brain/3422beb1-ae25-4859-a0d9-b53db9c7f8d3/.tempmediaStorage/purchase_invoice_create.png' });

    // Satış faturası oluşturma ekranına gidelim
    console.log('Sales Invoice Create sayfasına gidiliyor...');
    await tenantPage.goto('http://localhost:8080/t/asd/Invoices/Create?type=Sale');
    await tenantPage.waitForTimeout(2000);
    await tenantPage.screenshot({ path: '/home/zondax/.gemini/antigravity/brain/3422beb1-ae25-4859-a0d9-b53db9c7f8d3/.tempmediaStorage/sales_invoice_create.png' });

    console.log('6. POS...');
    await tenantPage.goto('http://localhost:8080/t/asd/POS');
    await tenantPage.waitForTimeout(2000);
    await tenantPage.screenshot({ path: '/home/zondax/.gemini/antigravity/brain/3422beb1-ae25-4859-a0d9-b53db9c7f8d3/.tempmediaStorage/pos_page.png' });

  } catch (error) {
    console.error('Hata oluştu:', error);
  } finally {
    await browser.close();
    console.log('Tarayıcı kapatıldı.');
  }
})();
