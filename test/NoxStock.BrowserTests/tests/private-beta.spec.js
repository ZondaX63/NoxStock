const { test, expect } = require('@playwright/test');

const hostUser = requiredEnv('NOXSTOCK_E2E_HOST_USER');
const hostPassword = requiredEnv('NOXSTOCK_E2E_HOST_PASSWORD');
const tenantPassword = requiredEnv('NOXSTOCK_E2E_TENANT_PASSWORD');
const atlas = {
  name: 'atlas-demo',
  userName: 'atlas.admin',
  email: 'atlas-admin@noxstock.invalid'
};
const isolation = {
  name: 'beta-isolation',
  userName: 'isolation.admin',
  email: 'isolation-admin@noxstock.invalid'
};
const inactive = {
  name: 'beta-inactive',
  userName: 'inactive.admin',
  email: 'inactive-admin@noxstock.invalid'
};

function requiredEnv(name) {
  const value = process.env[name];
  if (!value) throw new Error(`${name} must be provided at runtime.`);
  return value;
}

async function login(page, path, userName, password) {
  await page.goto(path);
  await page.locator('#LoginInput_UserNameOrEmailAddress').fill(userName);
  await page.locator('#LoginInput_Password').fill(password);
  await Promise.all([
    page.waitForURL(url => !url.pathname.endsWith('/Account/Login')),
    page.getByRole('button', { name: /Giriş yap|Log in|Sign in/i }).click()
  ]);
}

async function logout(page) {
  await Promise.all([
    page.waitForURL(url => url.pathname.endsWith('/Account/Login')),
    page.getByRole('button', { name: /Sign out|Çıkış yap|Logout/i }).click()
  ]);
}

async function ensureTenant(page, tenant) {
  await page.goto('/TenantManagement/Tenants');
  let row = page.locator('tbody tr').filter({ hasText: tenant.name });
  if (await row.count()) return row;

  await page.locator('#NewTenantButton').click();
  const modal = page.locator('.modal.show');
  await expect(modal).toBeVisible();
  await modal.locator('#Input_CompanyName').fill(tenant.name);
  await modal.locator('#Input_AdminUserName').fill(tenant.userName);
  await modal.locator('#Input_AdminEmailAddress').fill(tenant.email);
  await modal.locator('#Input_AdminPassword').fill(tenantPassword);
  await Promise.all([
    page.waitForResponse(response => response.url().includes('/TenantManagement/Tenants/CreateModal') && response.request().method() === 'POST' && response.status() === 204),
    modal.locator('.modal-footer button.btn-primary').click()
  ]);
  await page.waitForLoadState('networkidle');
  row = page.locator('tbody tr').filter({ hasText: tenant.name });
  await expect(row).toBeVisible();
  return row;
}

async function setTenantActive(page, tenantName, active) {
  await page.goto('/TenantManagement/Tenants');
  const row = page.locator('tbody tr').filter({ hasText: tenantName });
  await expect(row).toBeVisible();
  const action = active ? 'activate' : 'deactivate';
  const button = row.locator(`[data-tenant-action="${action}"]`);
  if (await button.count()) {
    await Promise.all([page.waitForNavigation(), button.click()]);
  }
  await expect(row.locator('td').nth(1)).toContainText(active ? 'Aktif' : 'Pasif');
}

async function appCall(page, servicePath, method, ...args) {
  const result = await page.evaluate(async ({ servicePath, method, args }) => {
    let owner = window;
    for (const part of servicePath.split('.')) owner = owner[part];
    const value = await owner[method](...args);
    return JSON.parse(JSON.stringify(value));
  }, { servicePath, method, args });
  return result;
}

async function appCallResult(page, servicePath, method, ...args) {
  return page.evaluate(async ({ servicePath, method, args }) => {
    try {
      let owner = window;
      for (const part of servicePath.split('.')) owner = owner[part];
      const value = await owner[method](...args);
      return { ok: true, value: JSON.parse(JSON.stringify(value)) };
    } catch (error) {
      return { ok: false, message: String(error && (error.message || error)) };
    }
  }, { servicePath, method, args });
}

async function getProduct(page, filter) {
  const response = await appCall(page, 'noxStock.products.product', 'getList', { filter, maxResultCount: 20 });
  return response.items.find(item => item.sku === filter || item.oemCodes?.includes(filter));
}

async function getAccount(page, id) {
  return appCall(page, 'noxStock.accounts.account', 'get', id);
}

async function getPartner(page, id) {
  return appCall(page, 'noxStock.businessPartners.businessPartner', 'get', id);
}

async function getDocument(page, docNumber) {
  await expect.poll(async () => {
    return page.evaluate(() => typeof window.noxStock !== 'undefined' && typeof window.noxStock.documents !== 'undefined' && typeof window.noxStock.documents.document !== 'undefined');
  }).toBe(true);
  const response = await appCall(page, 'noxStock.documents.document', 'getList', { filter: docNumber, scope: 1, maxResultCount: 20 });
  return response.items.find(item => item.docNumber === docNumber);
}

async function confirmAbpMessage(page) {
  const confirm = page.locator('.swal2-confirm:visible, .bootbox .btn-primary:visible, .modal.show button.btn-primary:visible').last();
  await expect(confirm).toBeVisible();
  await confirm.click();
}

function closeEnough(actual, expected, precision = 0.02) {
  expect(Math.abs(Number(actual) - Number(expected))).toBeLessThanOrEqual(precision);
}

test('private beta critical commercial paths preserve business state and tenant isolation', async ({ browser, baseURL }, testInfo) => {
  const runId = Date.now().toString(36).toUpperCase();
  const sku = `BETA-${runId}`;
  const barcode = `869${Date.now().toString().slice(-10)}`;
  const oem = '34116775277';
  const invoiceNumber = `BETA-INV-${runId}`;
  const failures = [];
  const hostContext = await browser.newContext({ baseURL, ignoreHTTPSErrors: true });
  const hostPage = await hostContext.newPage();

  hostPage.on('pageerror', error => failures.push(`host pageerror: ${error.message}`));
  hostPage.on('response', response => {
    if (response.status() >= 500) failures.push(`host HTTP ${response.status()}: ${response.url()}`);
  });

  await test.step('provision active A/B and inactive pilot tenants through host UI', async () => {
    await login(hostPage, '/Account/Login', hostUser, hostPassword);
    await ensureTenant(hostPage, atlas);
    await ensureTenant(hostPage, isolation);
    await ensureTenant(hostPage, inactive);
    await setTenantActive(hostPage, atlas.name, true);
    await setTenantActive(hostPage, isolation.name, true);
    await setTenantActive(hostPage, inactive.name, false);
    await logout(hostPage);
  });
  await hostContext.close();

  const contextA = await browser.newContext({ baseURL, ignoreHTTPSErrors: true });
  const page = await contextA.newPage();
  page.on('pageerror', error => {
    if (error.message.includes("setSelectionRange")) return;
    failures.push(`tenant A pageerror: ${error.message}`);
  });
  page.on('response', response => {
    if (response.status() >= 500) failures.push(`tenant A HTTP ${response.status()}: ${response.url()}`);
  });

  let account;
  let customer;
  let product;
  let initialAccountBalance;
  let initialStock;

  await test.step('tenant URL login, logout redirect, and login again', async () => {
    await login(page, `/t/${atlas.name}/Account/Login`, atlas.email, tenantPassword);
    await expect(page).toHaveURL(new RegExp(`/t/${atlas.name}/Dashboard`));
    await logout(page);
    await expect(page).toHaveURL(new RegExp(`/t/${atlas.name}/Account/Login`));
    await login(page, `/t/${atlas.name}/Account/Login`, atlas.email, tenantPassword);
  });

  await test.step('prepare isolated cash account and customer using authenticated application contracts', async () => {
    account = await appCall(page, 'noxStock.accounts.account', 'create', {
      name: `Beta Kasa ${runId}`,
      type: 1,
      currency: 'TRY',
      balance: 1000,
      isActive: true
    });
    customer = await appCall(page, 'noxStock.businessPartners.businessPartner', 'create', {
      name: `Beta Müşteri ${runId}`,
      type: 1,
      email: `beta-${runId.toLowerCase()}@noxstock.invalid`,
      phone: '5550000000',
      address: 'Private beta smoke',
      taxNumber: runId,
      taxOffice: 'Beta',
      balance: 0,
      creditLimit: 100000,
      currency: 'TRY'
    });
    initialAccountBalance = account.balance;
  });
  await test.step('add a foreign-cost product and find it by OEM through the UI', async () => {
    const testBrand = await appCall(page, 'noxStock.brands.brand', 'create', { name: `Beta Marka ${runId}` });
    const testCategory = await appCall(page, 'noxStock.categories.category', 'create', { name: `Beta Kategori ${runId}` });

    await page.goto(`/t/${atlas.name}/Products`);
    await page.locator('#NewProductButton').click();
    await page.locator('#Product_Name').fill(`BMW F10 Fren Diski ${runId}`);
    await page.locator('#Product_Sku').fill(sku);
    await page.locator('#Product_Barcode').fill(barcode);
    await page.locator('#Product_CategoryId').selectOption(testCategory.id);
    await page.locator('#Product_BrandId').selectOption(testBrand.id);
    await page.locator('#Product_OemCodes').fill(`${oem}, 34 11 6 775 277`);
    await page.locator('#Product_Description').fill('BMW E2E Test Product Description');
    await page.locator('#Product_PurchasePrice').fill('10');
    await page.locator('#Product_PurchaseCurrency').selectOption('USD');
    await page.locator('#Product_SaleCurrency').selectOption('TRY');
    await page.locator('#Product_ProfitMarkupPercent').fill('25');
    await page.locator('#Product_UseCurrentExchangeRateForSale').check();
    await page.locator('#Product_Quantity').fill('25');
    await page.locator('#Product_CriticalStockLevel').fill('5');
    await page.locator('#Product_ShelfLocation').fill('A-1-1');
    await page.locator('#CalculateSalePrice').click();
    await expect.poll(async () => Number(await page.locator('#Product_SalePrice').inputValue())).toBeGreaterThan(0);
    const productForm = page.locator('#ProductEditor form');
    const validation = await productForm.evaluate(form => ({
      valid: form.checkValidity(),
      invalid: [...form.elements]
        .filter(element => typeof element.checkValidity === 'function' && !element.checkValidity())
        .map(element => ({ name: element.name, message: element.validationMessage }))
    }));
    expect(validation.valid, JSON.stringify(validation.invalid)).toBeTruthy();
    await Promise.all([
      page.waitForNavigation(),
      page.locator('#ProductEditor form button[type="submit"]').click()
    ]);
    await page.locator('#ProductCategoryFilter').selectOption(testCategory.id);
    await page.locator('#ProductSearch').fill(oem);
    await expect.poll(async () => {
      const row = page.locator('#ProductsTable tbody tr').filter({ hasText: sku });
      if (await row.count() === 0) return false;
      return (await row.textContent()).includes(oem);
    }).toBe(true);
    product = await getProduct(page, sku);
    expect(product).toBeTruthy();
    expect(product.purchaseCurrency).toBe('USD');
    expect(product.saleCurrency).toBe('TRY');
    initialStock = product.quantity;
  });

  let posSale;
  let posTotal;
  await test.step('complete and cancel POS sale; verify stock, account, document, and reversal movements', async () => {
    await page.goto(`/t/${atlas.name}/Sales`);
    await page.locator('#PosSaleCurrency').selectOption('TRY');
    await page.locator('#PosSearch').fill(oem);
    const productCard = page.locator('.pos-product-card').filter({ hasText: sku });
    await expect(productCard).toBeVisible();
    await productCard.click();
    await page.locator('#PosPaymentMethods').getByRole('button', { name: 'Nakit' }).click();
    await page.locator('#PosAccount').selectOption(account.id);
    await page.locator('#PosNote').fill(`Private beta ${runId}`);
    await page.locator('#CompletePosSaleButton').click();
    await expect(page.locator('.toast-success, .abp-toast-success, div[role="alert"]')).toContainText('POS satışı tamamlandı');

    await expect.poll(async () => (await appCall(page, 'noxStock.sales.posSale', 'getRecent'))[0]?.receiptNumber).toBeTruthy();
    posSale = (await appCall(page, 'noxStock.sales.posSale', 'getRecent'))[0];
    posTotal = posSale.grandTotal;
    expect(posSale.status).toBe(6);
    closeEnough((await getProduct(page, sku)).quantity, initialStock - 1);
    closeEnough((await getAccount(page, account.id)).balance, initialAccountBalance + posTotal);
    let stockMovements;
    let accountTransactions;
    await expect.poll(async () => {
      stockMovements = await appCall(page, 'noxStock.stockMovements.stockMovement', 'getList', { documentId: posSale.id, maxResultCount: 20 });
      return stockMovements.items.filter(item => item.documentId === posSale.id).length;
    }).toBe(1);
    await expect.poll(async () => {
      accountTransactions = await appCall(page, 'noxStock.accounts.account', 'getTransactions', account.id, { maxResultCount: 100 });
      return accountTransactions.items.filter(item => item.documentId === posSale.id).length;
    }).toBe(1);

    const saleRow = page.locator('#RecentPosSalesBody tr').filter({ hasText: posSale.receiptNumber });
    await saleRow.locator('.pos-cancel-sale').click();
    await confirmAbpMessage(page);
    await expect.poll(async () => (await appCall(page, 'noxStock.sales.posSale', 'getRecent')).find(item => item.id === posSale.id)?.status).toBe(5);
    closeEnough((await getProduct(page, sku)).quantity, initialStock);
    closeEnough((await getAccount(page, account.id)).balance, initialAccountBalance);
    await expect.poll(async () => {
      stockMovements = await appCall(page, 'noxStock.stockMovements.stockMovement', 'getList', { documentId: posSale.id, maxResultCount: 20 });
      return stockMovements.items.filter(item => item.documentId === posSale.id).length;
    }).toBe(2);
    await expect.poll(async () => {
      accountTransactions = await appCall(page, 'noxStock.accounts.account', 'getTransactions', account.id, { maxResultCount: 100 });
      return accountTransactions.items.filter(item => item.documentId === posSale.id).length;
    }).toBe(2);
    closeEnough(accountTransactions.items.filter(item => item.documentId === posSale.id).reduce((sum, item) => sum + item.amount, 0), 0);
  });

  let invoice;
  let paymentAmount;
  await test.step('create and approve a sale invoice through UI', async () => {
    await page.goto(`/t/${atlas.name}/Invoices/Create?type=1`);
    await page.locator('#Invoice_DocNumber').fill(invoiceNumber);
    await page.locator('#Invoice_BusinessPartnerId').selectOption(customer.id);
    await page.locator('.product-select').selectOption(product.id);
    await expect.poll(async () => Number(await page.locator('.price-input').inputValue())).toBeGreaterThan(0);
    
    // Format and fill Date/DueDate to satisfy tr-TR parsed formats in backend
    const todayStr = new Date().toISOString().split('T')[0];
    const dueDateStr = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0];
    await page.locator('#Invoice_Date').fill(todayStr);
    await page.locator('#Invoice_DueDate').fill(dueDateStr);
    await page.locator('h2').first().click(); // Click heading to dismiss datepicker overlay

    await page.locator('.quantity-input').fill('2');
    await page.locator('.vat-input').fill('20');
    await Promise.all([
      page.waitForURL(new RegExp(`/t/${atlas.name}/Invoices`)),
      page.getByRole('button', { name: 'Belgeyi Kaydet' }).click()
    ]);
    await expect.poll(async () => {
      invoice = await getDocument(page, invoiceNumber);
      return invoice?.status;
    }).toBe(1);
    closeEnough((await getProduct(page, sku)).quantity, initialStock);

    const row = page.locator('#InvoicesTable tbody tr').filter({ hasText: invoiceNumber });
    await expect(row).toBeVisible();
    await row.locator('button.dropdown-toggle').click();
    await row.getByText('Onayla', { exact: true }).click();
    await expect.poll(async () => (await getDocument(page, invoiceNumber)).status).toBe(3);
    invoice = await getDocument(page, invoiceNumber);
    closeEnough((await getProduct(page, sku)).quantity, initialStock - 2);
    closeEnough((await getPartner(page, customer.id)).balance, invoice.grandTotal);
    const movements = await appCall(page, 'noxStock.stockMovements.stockMovement', 'getList', { documentId: invoice.id, maxResultCount: 20 });
    expect(movements.items.filter(item => item.documentId === invoice.id)).toHaveLength(1);
  });

  await test.step('record collection and verify current-account and cash history', async () => {
    paymentAmount = Number((invoice.grandTotal / 2).toFixed(2));
    const row = page.locator('#InvoicesTable tbody tr').filter({ hasText: invoiceNumber });
    await row.locator('button.dropdown-toggle').click();
    await row.getByText('Tahsilat Kaydet', { exact: true }).click();
    const modal = page.locator('.modal.show');
    await expect(modal).toBeVisible();
    await modal.locator('#Payment_AccountId').selectOption(account.id);
    await modal.locator('#Payment_Amount').fill(String(paymentAmount));
    await modal.locator('#Payment_Description').fill(`Beta tahsilat ${runId}`);
    await modal.locator('.modal-footer button.btn-primary').click();
    await expect(modal).toBeHidden();

    await expect.poll(async () => (await getDocument(page, invoiceNumber)).status).toBe(8);
    invoice = await getDocument(page, invoiceNumber);
    closeEnough(invoice.paidAmount, paymentAmount);
    closeEnough((await getPartner(page, customer.id)).balance, invoice.grandTotal - paymentAmount);
    closeEnough((await getAccount(page, account.id)).balance, initialAccountBalance + paymentAmount);
    const transactions = await appCall(page, 'noxStock.accounts.account', 'getTransactions', account.id, { maxResultCount: 100 });
    expect(transactions.items.filter(item => item.documentId === invoice.id)).toHaveLength(1);
    const history = await appCall(page, 'noxStock.businessPartners.businessPartnerStatement', 'getPaymentHistory', customer.id);
    expect(history.some(item => item.documentId === invoice.id && Math.abs(item.amount - paymentAmount) <= 0.02)).toBeTruthy();
  });

  await test.step('open customer sales history, change the period, and follow the invoice detail link', async () => {
    await page.goto(`/t/${atlas.name}/CustomerDetail?id=${customer.id}`);
    await page.getByRole('link', { name: 'Satış Geçmişi' }).click();
    await expect(page).toHaveURL(new RegExp(`/t/${atlas.name}/Customers/${customer.id}/SalesHistory`));
    await expect(page.getByRole('heading', { name: customer.name })).toBeVisible();
    await expect(page.locator('tbody tr').filter({ hasText: invoiceNumber })).toBeVisible();
    await page.locator('#PeriodPreset').selectOption('1');
    await page.getByRole('button', { name: 'Filtreleri uygula' }).click();
    await expect(page.locator('tbody tr').filter({ hasText: invoiceNumber })).toBeVisible();
    await expect(page.locator(`a[href*="/Invoices/Print?id=${invoice.id}"]`)).toBeVisible();
  });

  await test.step('dashboard, dark theme, and TR/EN switching stay operational', async () => {
    await page.goto(`/t/${atlas.name}/Dashboard`);
    await expect(page.getByRole('heading', { name: /Genel Bakış|Overview/i })).toBeVisible();
    const dashboard = await appCall(page, 'noxStock.dashboard.dashboard', 'getSummary');
    expect(dashboard.recentDocuments.length).toBeGreaterThan(0);
    expect(dashboard.activeCustomerCount).toBeGreaterThan(0);

    const themeButton = page.locator('header button').filter({ has: page.locator('span.material-icons-round', { hasText: 'dark_mode' }) });
    await themeButton.click();
    await expect(page.locator('html')).toHaveClass(/dark/);
    await page.locator('header a[href*="culture=en"]').click();
    await expect(page.locator('html')).toHaveAttribute('lang', /^en/);
    await expect(page.locator('html')).toHaveClass(/dark/);
    await page.locator('header a[href*="culture=tr"]').click();
    await expect(page.locator('html')).toHaveAttribute('lang', /^tr/);
    const lightButton = page.locator('header button').filter({ has: page.locator('span.material-icons-round', { hasText: 'light_mode' }) });
    await lightButton.click();
    await expect(page.locator('html')).not.toHaveClass(/dark/);
  });

  await logout(page);
  await contextA.close();

  await test.step('tenant B cannot list or fetch tenant A commercial data', async () => {
    const contextB = await browser.newContext({ baseURL, ignoreHTTPSErrors: true });
    const pageB = await contextB.newPage();
    pageB.on('pageerror', error => failures.push(`tenant B pageerror: ${error.message}`));
    pageB.on('response', response => {
      if (response.status() >= 500) failures.push(`tenant B HTTP ${response.status()}: ${response.url()}`);
    });
    await login(pageB, `/t/${isolation.name}/Account/Login`, isolation.email, tenantPassword);
    const products = await appCall(pageB, 'noxStock.products.product', 'getList', { filter: sku, maxResultCount: 20 });
    expect(products.totalCount).toBe(0);
    const directRead = await appCallResult(pageB, 'noxStock.products.product', 'get', product.id);
    expect(directRead.ok).toBeFalsy();
    const documents = await appCall(pageB, 'noxStock.documents.document', 'getList', { filter: invoiceNumber, scope: 1, maxResultCount: 20 });
    expect(documents.totalCount).toBe(0);
    const dashboard = await appCall(pageB, 'noxStock.dashboard.dashboard', 'getSummary');
    expect(dashboard.recentDocuments.some(item => item.id === invoice.id)).toBeFalsy();
    await contextB.close();
  });

  await test.step('inactive tenant path is denied before login', async () => {
    const inactiveContext = await browser.newContext({ baseURL, ignoreHTTPSErrors: true });
    const inactivePage = await inactiveContext.newPage();
    const response = await inactivePage.goto(`/t/${inactive.name}/Account/Login`);
    expect(response.status()).toBe(403);
    await expect(inactivePage.locator('body')).toContainText('Tenant is inactive');
    await inactiveContext.close();
  });

  expect(failures, failures.join('\n')).toEqual([]);
  await testInfo.attach('commercial-state-summary', {
    body: Buffer.from(JSON.stringify({
      tenant: atlas.name,
      isolationTenant: isolation.name,
      inactiveTenant: inactive.name,
      productId: product.id,
      sku,
      oem,
      posDocumentId: posSale.id,
      invoiceId: invoice.id,
      invoiceStatus: invoice.status,
      invoicePaidAmount: invoice.paidAmount
    }, null, 2)),
    contentType: 'application/json'
  });
});
