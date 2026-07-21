const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ baseURL: 'http://localhost:8080', ignoreHTTPSErrors: true });
  const page = await context.newPage();

  try {
    console.log('1. Tenant login sayfasına gidiliyor...');
    await page.goto('/t/asd/Account/Login');
    await page.waitForSelector('#LoginInput_UserNameOrEmailAddress');
    await page.locator('#LoginInput_UserNameOrEmailAddress').fill('asd');
    await page.locator('#LoginInput_Password').fill('asdasd');
    
    console.log('Giriş yapılıyor...');
    await Promise.all([
      page.waitForURL(url => !url.pathname.endsWith('/Account/Login')),
      page.getByRole('button', { name: /Giriş yap|Log in|Sign in/i }).click()
    ]);
    console.log('Giriş başarılı. URL:', page.url());

    console.log('2. Products sayfasına gidiliyor...');
    await page.goto('/t/asd/Products');
    await page.waitForLoadState('networkidle');
    console.log('Products sayfası yüklendi. URL:', page.url());

    // Sayfa başlığını yazdıralım
    const title = await page.title();
    console.log('Sayfa Başlığı:', title);

    console.log('Yeni ürün ekleme modalı açılıyor...');
    await page.locator('#NewProductButton').click();
    
    // Modalın görünmesini bekleyelim
    await page.waitForSelector('.modal.show', { timeout: 5000 });
    console.log('Modal açıldı.');

    // Modal içerisindeki form elementlerini listeleyelim
    const inputs = await page.evaluate(() => {
      const modal = document.querySelector('.modal.show');
      if (!modal) return 'Modal bulunamadı';
      
      const elements = Array.from(modal.querySelectorAll('input, select, button'));
      return elements.map(el => ({
        tagName: el.tagName,
        id: el.id,
        name: el.name,
        type: el.type,
        value: el.value,
        outerHTML: el.outerHTML.substring(0, 200)
      }));
    });

    console.log('Modal form elementleri:', JSON.stringify(inputs, null, 2));

  } catch (error) {
    console.error('Hata:', error);
  } finally {
    await browser.close();
  }
})();
