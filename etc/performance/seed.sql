\set ON_ERROR_STOP on
\timing on

CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
CREATE SCHEMA IF NOT EXISTS noxstock_perf;

CREATE OR REPLACE FUNCTION noxstock_perf.uuid(input text)
RETURNS uuid
LANGUAGE sql
IMMUTABLE
STRICT
AS $$ SELECT md5(input)::uuid $$;

BEGIN;
SET LOCAL synchronous_commit = off;
SET LOCAL work_mem = '128MB';

CREATE TEMP TABLE perf_tenants(
  tenant_no integer PRIMARY KEY,
  id uuid NOT NULL,
  code text NOT NULL
) ON COMMIT DROP;

INSERT INTO perf_tenants(tenant_no, id, code) VALUES
  (1, '10000000-0000-0000-0000-000000000001', 'perf-a'),
  (2, '10000000-0000-0000-0000-000000000002', 'perf-b');

DELETE FROM "AppAccountTransactions" WHERE "TenantId" IN (SELECT id FROM perf_tenants);
DELETE FROM "AppStockMovements" WHERE "TenantId" IN (SELECT id FROM perf_tenants);
DELETE FROM "AppDocumentItems" WHERE "TenantId" IN (SELECT id FROM perf_tenants);
DELETE FROM "AppDocuments" WHERE "TenantId" IN (SELECT id FROM perf_tenants);
DELETE FROM "AppProductOEMCodes" WHERE "TenantId" IN (SELECT id FROM perf_tenants);
DELETE FROM "AppProducts" WHERE "TenantId" IN (SELECT id FROM perf_tenants);
DELETE FROM "AppAccounts" WHERE "TenantId" IN (SELECT id FROM perf_tenants);
DELETE FROM "AppBusinessPartners" WHERE "TenantId" IN (SELECT id FROM perf_tenants);
DELETE FROM "AppCategories" WHERE "TenantId" IN (SELECT id FROM perf_tenants);
DELETE FROM "AppBrands" WHERE "TenantId" IN (SELECT id FROM perf_tenants);
DELETE FROM "AbpTenants" WHERE "Id" IN (SELECT id FROM perf_tenants);

INSERT INTO "AbpTenants" (
  "Id", "Name", "NormalizedName", "EntityVersion", "ExtraProperties",
  "ConcurrencyStamp", "CreationTime", "IsDeleted", "IsActive")
SELECT
  id, code, upper(code), 0, '{"IsActive":true}',
  md5('tenant:' || code), :'anchor_date'::date, false, true
FROM perf_tenants;

INSERT INTO "AppBrands" (
  "Id", "TenantId", "Name", "ExtraProperties", "ConcurrencyStamp",
  "CreationTime", "IsDeleted")
SELECT
  noxstock_perf.uuid(format('brand:%s:%s', tenant_no, series_no)),
  id,
  format('Perf Brand %s-%s', tenant_no, lpad(series_no::text, 2, '0')),
  '{}', md5(format('brand:%s:%s', tenant_no, series_no)),
  :'anchor_date'::date - (series_no % 30), false
FROM perf_tenants
CROSS JOIN generate_series(1, 20) AS series_no;

INSERT INTO "AppCategories" (
  "Id", "TenantId", "Name", "ParentId", "ExtraProperties", "ConcurrencyStamp",
  "CreationTime", "IsDeleted")
SELECT
  noxstock_perf.uuid(format('category:%s:%s', tenant_no, series_no)),
  id,
  format('Perf Category %s-%s', tenant_no, lpad(series_no::text, 2, '0')),
  NULL, '{}', md5(format('category:%s:%s', tenant_no, series_no)),
  :'anchor_date'::date - (series_no % 30), false
FROM perf_tenants
CROSS JOIN generate_series(1, 30) AS series_no;

INSERT INTO "AppBusinessPartners" (
  "Id", "TenantId", "Name", "Type", "Email", "Phone", "Address",
  "TaxNumber", "TaxOffice", "Balance", "CreditLimit", "Currency",
  "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
SELECT
  noxstock_perf.uuid(format('partner:%s:%s', tenant_no, series_no)),
  id,
  format('Perf Cari %s %s', tenant_no, lpad(series_no::text, 7, '0')),
  CASE series_no % 3 WHEN 0 THEN 3 WHEN 1 THEN 1 ELSE 2 END,
  format('cari%s-%s@example.test', tenant_no, series_no),
  format('+90-555-%07s', series_no),
  format('Deterministic performance address %s/%s', tenant_no, series_no),
  format('%010s', tenant_no * 100000000 + series_no),
  format('Perf Tax Office %s', series_no % 25),
  CASE WHEN series_no % 2 = 0 THEN (series_no % 10000) * 10.25 ELSE -((series_no % 5000) * 7.75) END,
  250000 + (series_no % 1000) * 100,
  CASE series_no % 3 WHEN 0 THEN 'USD' WHEN 1 THEN 'TRY' ELSE 'EUR' END,
  '{}', md5(format('partner:%s:%s', tenant_no, series_no)),
  :'anchor_date'::date - (series_no % 730), false
FROM perf_tenants
CROSS JOIN generate_series(1, :partners) AS series_no;

INSERT INTO "AppAccounts" (
  "Id", "TenantId", "Name", "Type", "Currency", "Balance", "IsActive",
  "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
SELECT
  noxstock_perf.uuid(format('account:%s:%s', tenant_no, series_no)),
  id,
  format('Perf Account %s-%s', tenant_no, lpad(series_no::text, 3, '0')),
  CASE series_no % 4 WHEN 0 THEN 3 WHEN 1 THEN 1 WHEN 2 THEN 2 ELSE 99 END,
  CASE series_no % 3 WHEN 0 THEN 'USD' WHEN 1 THEN 'TRY' ELSE 'EUR' END,
  10000 + series_no * 1250.50,
  series_no % 10 <> 0,
  '{}', md5(format('account:%s:%s', tenant_no, series_no)),
  :'anchor_date'::date - (series_no % 90), false
FROM perf_tenants
CROSS JOIN generate_series(1, :accounts) AS series_no;

INSERT INTO "AppProducts" (
  "Id", "TenantId", "CategoryId", "BrandId", "Name", "Sku", "Barcode",
  "Description", "Unit", "ShelfLocation", "PurchasePrice", "SalePrice",
  "Quantity", "CriticalStockLevel", "TrackStock", "Currency",
  "PurchaseCurrency", "SaleCurrency", "ExtraProperties", "ConcurrencyStamp",
  "CreationTime", "IsDeleted", "ProfitMarkupPercent", "UseCurrentExchangeRateForSale")
SELECT
  noxstock_perf.uuid(format('product:%s:%s', tenant_no, series_no)),
  id,
  noxstock_perf.uuid(format('category:%s:%s', tenant_no, ((series_no - 1) % 30) + 1)),
  noxstock_perf.uuid(format('brand:%s:%s', tenant_no, ((series_no - 1) % 20) + 1)),
  CASE
    WHEN series_no % 1000 BETWEEN 0 AND 4 THEN format('BMW F10 Fren Diski Alternatif %s', series_no % 5)
    WHEN series_no % 7 = 0 THEN format('Fren Diski Premium %s', lpad(series_no::text, 7, '0'))
    WHEN series_no % 7 = 1 THEN format('Yag Filtresi %s', lpad(series_no::text, 7, '0'))
    WHEN series_no % 7 = 2 THEN format('Hava Filtresi %s', lpad(series_no::text, 7, '0'))
    WHEN series_no % 7 = 3 THEN format('Debriyaj Seti %s', lpad(series_no::text, 7, '0'))
    WHEN series_no % 7 = 4 THEN format('Amortisor %s', lpad(series_no::text, 7, '0'))
    WHEN series_no % 7 = 5 THEN format('Rot Basi %s', lpad(series_no::text, 7, '0'))
    ELSE format('Aks Rulmani %s', lpad(series_no::text, 7, '0'))
  END,
  format('SKU-%s-%s', tenant_no, lpad(series_no::text, 8, '0')),
  format('869%s%s', tenant_no, lpad(series_no::text, 9, '0')),
  format('Deterministic search and scale product %s for tenant %s', series_no, tenant_no),
  CASE series_no % 3 WHEN 0 THEN 'Takim' WHEN 1 THEN 'Adet' ELSE 'Kutu' END,
  format('%s-%s-%s', chr(65 + (series_no % 20)), 1 + series_no % 40, 1 + series_no % 12),
  round((10 + (series_no % 50000) / 100.0)::numeric, 4),
  round((15 + (series_no % 70000) / 100.0)::numeric, 4),
  CASE series_no % 10
    WHEN 0 THEN 0
    WHEN 1 THEN 2
    WHEN 2 THEN 5
    ELSE 20 + (series_no % 500)
  END,
  5 + (series_no % 8),
  series_no % 20 <> 0,
  CASE series_no % 3 WHEN 0 THEN 'USD' WHEN 1 THEN 'TRY' ELSE 'EUR' END,
  CASE series_no % 3 WHEN 0 THEN 'USD' WHEN 1 THEN 'TRY' ELSE 'EUR' END,
  CASE series_no % 4 WHEN 0 THEN 'USD' WHEN 1 THEN 'TRY' WHEN 2 THEN 'EUR' ELSE 'TRY' END,
  '{}', md5(format('product:%s:%s', tenant_no, series_no)),
  :'anchor_date'::date - (series_no % 730), false,
  5 + (series_no % 46),
  series_no % 2 = 0
FROM perf_tenants
CROSS JOIN generate_series(1, :products) AS series_no;

INSERT INTO "AppProductOEMCodes" (
  "Id", "TenantId", "ProductId", "OemCode", "SearchCode",
  "CreationTime", "IsDeleted")
SELECT
  noxstock_perf.uuid(format('oem:%s:%s:%s', tenant_no, product_no, oem_no)),
  id,
  noxstock_perf.uuid(format('product:%s:%s', tenant_no, product_no)),
  CASE
    WHEN product_no % 1000 BETWEEN 0 AND 4 AND oem_no = 1 THEN '34 11 6 775 277'
    WHEN oem_no % 2 = 0 THEN format('OEM-%s-%s-%s', tenant_no, lpad(product_no::text, 8, '0'), oem_no)
    ELSE format('OEM %s %s %s', tenant_no, lpad(product_no::text, 8, '0'), oem_no)
  END,
  CASE
    WHEN product_no % 1000 BETWEEN 0 AND 4 AND oem_no = 1 THEN '34116775277'
    ELSE format('OEM%s%s%s', tenant_no, lpad(product_no::text, 8, '0'), oem_no)
  END,
  :'anchor_date'::date - (product_no % 730), false
FROM perf_tenants
CROSS JOIN generate_series(1, :products) AS product_no
CROSS JOIN generate_series(1, :oem_per_product) AS oem_no;

INSERT INTO "AppDocuments" (
  "Id", "TenantId", "BusinessPartnerId", "DocNumber", "Type", "Status",
  "Subtotal", "VatTotal", "DiscountTotal", "GrandTotal", "PaidAmount",
  "Currency", "ExchangeRate", "Date", "DueDate", "Description",
  "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted",
  "SourceDocumentId", "OperationKey")
SELECT
  noxstock_perf.uuid(format('document:%s:%s', tenant_no, series_no)),
  id,
  noxstock_perf.uuid(format('partner:%s:%s', tenant_no, ((series_no - 1) % :partners) + 1)),
  format('PERF-%s-%s-%s', tenant_no, 1 + (series_no % 8), lpad(series_no::text, 9, '0')),
  1 + (series_no % 8),
  CASE
    WHEN series_no % 8 IN (0, 1) THEN
      CASE (series_no / 8) % 4 WHEN 0 THEN 1 WHEN 1 THEN 2 WHEN 2 THEN 3 ELSE 8 END
    WHEN series_no % 8 = 2 THEN 3
    WHEN series_no % 8 = 3 THEN 6
    WHEN series_no % 8 = 4 THEN 7
    WHEN series_no % 8 = 5 THEN 8
    WHEN series_no % 8 = 6 THEN 5
    ELSE 3
  END,
  round((100 + (series_no % 100000) / 10.0)::numeric, 4),
  round(((100 + (series_no % 100000) / 10.0) * 0.20)::numeric, 4),
  round(((series_no % 5) * 2.5)::numeric, 4),
  round(((100 + (series_no % 100000) / 10.0) * 1.20 - ((series_no % 5) * 2.5))::numeric, 4),
  CASE WHEN series_no % 8 IN (3, 4) THEN round(((100 + (series_no % 100000) / 10.0) * 1.20)::numeric, 4) WHEN series_no % 8 = 5 THEN 50 ELSE 0 END,
  CASE series_no % 3 WHEN 0 THEN 'USD' WHEN 1 THEN 'TRY' ELSE 'EUR' END,
  CASE series_no % 3 WHEN 0 THEN 47.0517 WHEN 1 THEN 1 ELSE 53.9514 END,
  :'anchor_date'::date - (series_no % 730),
  (:'anchor_date'::date - (series_no % 730)) + ((series_no % 91) - 45),
  format('Deterministic performance document %s/%s', tenant_no, series_no),
  '{}', md5(format('document:%s:%s', tenant_no, series_no)),
  (:'anchor_date'::date - (series_no % 730)) + interval '8 hours', false,
  NULL, NULL
FROM perf_tenants
CROSS JOIN generate_series(1, :documents) AS series_no;

INSERT INTO "AppDocumentItems" (
  "Id", "TenantId", "DocumentId", "ProductId", "Quantity", "UnitPrice",
  "VatRate", "DiscountRate", "Total", "CreationTime", "IsDeleted")
SELECT
  noxstock_perf.uuid(format('item:%s:%s:%s', tenant_no, document_no, item_no)),
  id,
  noxstock_perf.uuid(format('document:%s:%s', tenant_no, document_no)),
  noxstock_perf.uuid(format('product:%s:%s', tenant_no, ((document_no * :items_per_document + item_no - 1) % :products) + 1)),
  1 + ((document_no + item_no) % 8),
  round((20 + ((document_no * item_no) % 10000) / 10.0)::numeric, 4),
  CASE item_no % 3 WHEN 0 THEN 10 WHEN 1 THEN 20 ELSE 1 END,
  CASE WHEN document_no % 10 = 0 THEN 5 ELSE 0 END,
  round(((1 + ((document_no + item_no) % 8)) * (20 + ((document_no * item_no) % 10000) / 10.0))::numeric, 4),
  :'anchor_date'::date - (document_no % 730), false
FROM perf_tenants
CROSS JOIN generate_series(1, :documents) AS document_no
CROSS JOIN generate_series(1, :items_per_document) AS item_no;

INSERT INTO "AppStockMovements" (
  "Id", "TenantId", "ProductId", "DocumentId", "Type", "Quantity",
  "BalanceAfter", "MovementDate", "Description", "CreationTime", "IsDeleted")
SELECT
  noxstock_perf.uuid(format('movement:%s:%s', tenant_no, series_no)),
  id,
  noxstock_perf.uuid(format('product:%s:%s', tenant_no, ((series_no - 1) % least(:products, 1000)) + 1)),
  noxstock_perf.uuid(format('document:%s:%s', tenant_no, ((series_no - 1) % :documents) + 1)),
  1 + (series_no % 8),
  CASE WHEN series_no % 3 = 0 THEN -(1 + series_no % 5) ELSE 1 + series_no % 10 END,
  series_no % 500,
  (:'anchor_date'::date - (series_no % 730)) + ((series_no % 86400) * interval '1 second'),
  format('Deterministic stock movement %s/%s', tenant_no, series_no),
  (:'anchor_date'::date - (series_no % 730)) + ((series_no % 86400) * interval '1 second'), false
FROM perf_tenants
CROSS JOIN generate_series(1, :movements) AS series_no;

INSERT INTO "AppAccountTransactions" (
  "Id", "TenantId", "AccountId", "DocumentId", "Type", "Amount", "Currency",
  "TransactionDate", "Description", "CreationTime", "IsDeleted", "OperationKey")
SELECT
  noxstock_perf.uuid(format('transaction:%s:%s', tenant_no, series_no)),
  id,
  noxstock_perf.uuid(format('account:%s:%s', tenant_no, ((series_no - 1) % :accounts) + 1)),
  noxstock_perf.uuid(format('document:%s:%s', tenant_no, ((series_no - 1) % :documents) + 1)),
  1 + (series_no % 8),
  round((10 + (series_no % 500000) / 100.0)::numeric, 4),
  CASE series_no % 3 WHEN 0 THEN 'USD' WHEN 1 THEN 'TRY' ELSE 'EUR' END,
  (:'anchor_date'::date - (series_no % 730)) + ((series_no % 86400) * interval '1 second'),
  format('Deterministic account transaction %s/%s', tenant_no, series_no),
  (:'anchor_date'::date - (series_no % 730)) + ((series_no % 86400) * interval '1 second'), false,
  NULL
FROM perf_tenants
CROSS JOIN generate_series(1, :transactions) AS series_no;

DROP TABLE IF EXISTS noxstock_perf.seed_manifest;
CREATE TABLE noxstock_perf.seed_manifest AS
SELECT
  :'profile'::text AS profile,
  :'anchor_date'::date AS anchor_date,
  now() AS seeded_at,
  2::integer AS tenant_count,
  :products::bigint AS products_per_tenant,
  :oem_per_product::bigint AS oem_per_product,
  :partners::bigint AS partners_per_tenant,
  :documents::bigint AS documents_per_tenant,
  :items_per_document::bigint AS items_per_document,
  :movements::bigint AS movements_per_tenant,
  :transactions::bigint AS transactions_per_tenant,
  :accounts::bigint AS accounts_per_tenant;

COMMIT;

ANALYZE "AppBrands";
ANALYZE "AppCategories";
ANALYZE "AppBusinessPartners";
ANALYZE "AppAccounts";
ANALYZE "AppProducts";
ANALYZE "AppProductOEMCodes";
ANALYZE "AppDocuments";
ANALYZE "AppDocumentItems";
ANALYZE "AppStockMovements";
ANALYZE "AppAccountTransactions";

SELECT * FROM noxstock_perf.seed_manifest;

SELECT "TenantId", count(*) AS products
FROM "AppProducts"
WHERE "TenantId" IN (
  '10000000-0000-0000-0000-000000000001',
  '10000000-0000-0000-0000-000000000002')
GROUP BY "TenantId"
ORDER BY "TenantId";
