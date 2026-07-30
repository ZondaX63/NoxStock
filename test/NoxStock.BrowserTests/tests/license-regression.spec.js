const { test, expect } = require('@playwright/test');

const hostUser = requiredEnv('NOXSTOCK_E2E_HOST_USER');
const hostPassword = requiredEnv('NOXSTOCK_E2E_HOST_PASSWORD');
const tenantPassword = requiredEnv('NOXSTOCK_E2E_TENANT_PASSWORD');

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

async function appCall(page, servicePath, method, ...args) {
  return page.evaluate(async ({ servicePath, method, args }) => {
    let owner = window;
    for (const part of servicePath.split('.')) owner = owner[part];
    const value = await owner[method](...args);
    return JSON.parse(JSON.stringify(value));
  }, { servicePath, method, args });
}

async function appCallResult(page, servicePath, method, ...args) {
  return page.evaluate(async ({ servicePath, method, args }) => {
    try {
      let owner = window;
      for (const part of servicePath.split('.')) owner = owner[part];
      const value = await owner[method](...args);
      return { ok: true, value: JSON.parse(JSON.stringify(value)) };
    } catch (error) {
      return {
        ok: false,
        code: error?.error?.code || error?.responseJSON?.error?.code || '',
        message: String(error?.error?.message || error?.message || error)
      };
    }
  }, { servicePath, method, args });
}

async function createTenantUserFromHost(page, tenantId, suffix) {
  return appCallResult(page, 'noxStock.licensing.hostTenantUser', 'create', {
    tenantId,
    userName: `pilot.user.${suffix}`,
    email: `pilot.user.${suffix}@noxstock.invalid`,
    password: 'Aa123!test'
  });
}

async function submitLicenseAction(page, tenantName, action) {
  await page.goto('/host/licenses');
  const row = page.locator('tbody tr').filter({ hasText: tenantName });
  await expect(row).toBeVisible();
  await Promise.all([
    page.waitForNavigation(),
    row.locator(`[data-license-action="${action}"]`).click()
  ]);
  return page.locator('tbody tr').filter({ hasText: tenantName });
}

test('C1 license states, MaxUsers and CUSTOM entitlements are enforced end to end', async ({ browser, baseURL }) => {
  const runId = Date.now().toString(36).toLowerCase();
  const tenant = {
    name: `c1-${runId}`,
    userName: `c1.admin.${runId}`,
    email: `c1.admin.${runId}@noxstock.invalid`
  };

  const hostContext = await browser.newContext({ baseURL, ignoreHTTPSErrors: true });
  const hostPage = await hostContext.newPage();
  await login(hostPage, '/Account/Login', hostUser, hostPassword);

  await test.step('host creates a tenant with a five-user PILOT trial', async () => {
    await hostPage.goto('/TenantManagement/Tenants');
    await hostPage.locator('#NewTenantButton').click();
    const modal = hostPage.locator('.modal.show');
    await modal.locator('#Input_CompanyName').fill(tenant.name);
    await modal.locator('#Input_AdminUserName').fill(tenant.userName);
    await modal.locator('#Input_AdminEmailAddress').fill(tenant.email);
    await modal.locator('#Input_AdminPassword').fill(tenantPassword);
    await Promise.all([
      hostPage.waitForResponse(response => response.url().includes('/TenantManagement/Tenants/CreateModal') && response.request().method() === 'POST' && response.status() === 204),
      modal.locator('.modal-footer button.btn-primary').click()
    ]);

    await hostPage.goto('/host/licenses');
    const row = hostPage.locator('tbody tr').filter({ hasText: tenant.name });
    await expect(row).toContainText(/Pilot/i);
    await expect(row).toContainText('MaxUsers: 5');
  });

  const tenantContext = await browser.newContext({ baseURL, ignoreHTTPSErrors: true });
  const tenantPage = await tenantContext.newPage();
  await login(tenantPage, `/t/${tenant.name}/Account/Login`, tenant.email, tenantPassword);

  let product;
  let account;
  let blockedInvoice;
  await test.step('active tenant creates product, invoice and POS sale', async () => {
    const brand = await appCall(tenantPage, 'noxStock.brands.brand', 'create', { name: `C1 Brand ${runId}` });
    const category = await appCall(tenantPage, 'noxStock.categories.category', 'create', { name: `C1 Category ${runId}` });
    const customer = await appCall(tenantPage, 'noxStock.businessPartners.businessPartner', 'create', {
      name: `C1 Customer ${runId}`, type: 1, balance: 0, creditLimit: 100000, currency: 'TRY'
    });
    account = await appCall(tenantPage, 'noxStock.accounts.account', 'create', {
      name: `C1 Cash ${runId}`, type: 1, currency: 'TRY', balance: 0, isActive: true
    });
    product = await appCall(tenantPage, 'noxStock.products.product', 'create', {
      categoryId: category.id,
      brandId: brand.id,
      name: `C1 Product ${runId}`,
      sku: `C1-${runId}`,
      barcode: `869${Date.now().toString().slice(-10)}`,
      description: 'C1 license regression',
      unit: 'Adet',
      shelfLocation: 'C1',
      purchasePrice: 50,
      salePrice: 100,
      profitMarkupPercent: 30,
      useCurrentExchangeRateForSale: false,
      pricingMode: 0,
      quantity: 20,
      criticalStockLevel: 2,
      trackStock: true,
      purchaseCurrency: 'TRY',
      oemCodes: `C1OEM${runId}`
    });
    await appCall(tenantPage, 'noxStock.sales.posSale', 'create', {
      operationKey: `C1-POS-${runId}`,
      paymentMethod: 1,
      accountId: account.id,
      note: 'C1 active license',
      items: [{ productId: product.id, quantity: 1, unitPrice: 100, isUnitPriceOverridden: false }]
    });
    blockedInvoice = await appCall(tenantPage, 'noxStock.documents.document', 'create', {
      businessPartnerId: customer.id,
      docNumber: `C1-INV-${runId}`,
      type: 1,
      status: 1,
      currency: 'TRY',
      exchangeRate: 1,
      date: new Date().toISOString(),
      dueDate: new Date(Date.now() + 86400000).toISOString(),
      items: [{ productId: product.id, quantity: 1, unitPrice: 100, vatRate: 20, discountRate: 0, total: 120 }]
    });
    expect(blockedInvoice.status).toBe(1);
  });

  await test.step('PILOT rejects the sixth user through host provisioning', async () => {
    let tenantId;
    await hostPage.goto('/host/licenses');
    const row = hostPage.locator('tbody tr').filter({ hasText: tenant.name });
    await expect(row).toBeVisible();
    tenantId = (await row.locator('td').first().locator('.font-mono').textContent()).trim();

    for (let index = 2; index <= 5; index++) {
      const created = await createTenantUserFromHost(hostPage, tenantId, `${runId}.${index}`);
      expect(created.ok, created.message).toBe(true);
    }
    const rejected = await createTenantUserFromHost(hostPage, tenantId, `${runId}.6`);
    expect(rejected.ok).toBe(false);
    expect(`${rejected.code} ${rejected.message}`).toContain('LICENSE_LIMIT_REACHED');
  });

  await test.step('expired tenant is read-only while dashboard remains available', async () => {
    let row = await submitLicenseAction(hostPage, tenant.name, 'expire');
    await expect(row).toContainText(/Süresi dolmuş|Expired/i);

    const productWrite = await appCallResult(tenantPage, 'noxStock.brands.brand', 'create', { name: `Blocked ${runId}` });
    const posWrite = await appCallResult(tenantPage, 'noxStock.sales.posSale', 'create', {
      operationKey: `BLOCKED-${runId}`, paymentMethod: 1, accountId: account.id,
      items: [{ productId: product.id, quantity: 1, unitPrice: 100 }]
    });
    const invoiceApproval = await appCallResult(tenantPage, 'noxStock.documents.documentWorkflow', 'approve', blockedInvoice.id);
    for (const result of [productWrite, posWrite, invoiceApproval]) {
      expect(result.ok).toBe(false);
      expect(`${result.code} ${result.message}`).toContain('LICENSE_EXPIRED');
    }

    const dashboardResponse = await tenantPage.goto(`/t/${tenant.name}/Dashboard`);
    expect(dashboardResponse.status()).toBe(200);
  });

  await test.step('extension, suspension and reactivation affect the next tenant write', async () => {
    await submitLicenseAction(hostPage, tenant.name, 'extend');
    const activeWrite = await appCallResult(tenantPage, 'noxStock.brands.brand', 'create', { name: `Active ${runId}` });
    expect(activeWrite.ok, activeWrite.message).toBe(true);

    await submitLicenseAction(hostPage, tenant.name, 'suspend');
    const suspendedWrite = await appCallResult(tenantPage, 'noxStock.brands.brand', 'create', { name: `Suspended ${runId}` });
    expect(suspendedWrite.ok).toBe(false);
    expect(`${suspendedWrite.code} ${suspendedWrite.message}`).toContain('LICENSE_SUSPENDED');

    await submitLicenseAction(hostPage, tenant.name, 'reactivate');
    const reactivatedWrite = await appCallResult(tenantPage, 'noxStock.brands.brand', 'create', { name: `Reactivated ${runId}` });
    expect(reactivatedWrite.ok, reactivatedWrite.message).toBe(true);
  });

  await test.step('host edits a simple CUSTOM feature and limit set', async () => {
    await hostPage.goto('/host/licenses');
    let row = hostPage.locator('tbody tr').filter({ hasText: tenant.name });
    const customPlanId = await row.locator('select[name="planId"] option')
      .filter({ hasText: /Özel|Custom/i })
      .first()
      .getAttribute('value');
    await row.locator('select[name="planId"]').selectOption(customPlanId);
    await Promise.all([
      hostPage.waitForNavigation(),
      row.locator('[data-license-action="change-plan"]').click()
    ]);
    row = hostPage.locator('tbody tr').filter({ hasText: tenant.name });
    await Promise.all([
      hostPage.waitForNavigation(),
      row.locator('[data-license-action="details"]').click()
    ]);

    const editor = hostPage.locator('form[asp-page-handler="UpdateEntitlements"], form').filter({ has: hostPage.locator('[data-license-action="save-entitlements"]') });
    await editor.locator('input[name="advancedReports"]').uncheck();
    await editor.locator('input[name="maxUsers"]').fill('20');
    await editor.locator('input[name="maxProducts"]').fill('100000');
    await editor.locator('input[name="maxBranches"]').fill('3');
    await Promise.all([
      hostPage.waitForNavigation(),
      editor.locator('[data-license-action="save-entitlements"]').click()
    ]);

    row = hostPage.locator('tbody tr').filter({ hasText: tenant.name });
    await expect(row).toContainText('MaxUsers: 20');
    await expect(row).toContainText('MaxProducts: 100000');
  });

  await tenantContext.close();
  await hostContext.close();
});
