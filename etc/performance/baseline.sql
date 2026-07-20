\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
SET pg_stat_statements.track_utility = off;

\set tenant_a '10000000-0000-0000-0000-000000000001'
\set account_a '6ed7e05a-76fe-225c-fdad-aeeb9f6e7f46'
\set product_a 'c8eccc0e-15f0-e22c-f0f3-500e7715f78d'

\o /results/01-product-first-page.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
SELECT p."Id", p."Name", p."Sku", p."Barcode", p."Quantity",
       b."Name" AS brand_name, c."Name" AS category_name
FROM "AppProducts" p
LEFT JOIN "AppBrands" b ON b."Id" = p."BrandId" AND b."TenantId" = :'tenant_a' AND NOT b."IsDeleted"
LEFT JOIN "AppCategories" c ON c."Id" = p."CategoryId" AND c."TenantId" = :'tenant_a' AND NOT c."IsDeleted"
WHERE p."TenantId" = :'tenant_a' AND NOT p."IsDeleted"
ORDER BY p."Name", p."Id"
LIMIT 50 OFFSET 0;

\o /results/02-product-deep-page.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
SELECT p."Id", p."Name", p."Sku", p."Barcode", p."Quantity"
FROM "AppProducts" p
WHERE p."TenantId" = :'tenant_a' AND NOT p."IsDeleted"
ORDER BY p."Name", p."Id"
LIMIT 50 OFFSET 99950;

\o /results/03-product-sku-exact.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
SELECT p.*
FROM "AppProducts" p
WHERE p."TenantId" = :'tenant_a' AND NOT p."IsDeleted"
  AND p."Sku" = 'SKU-1-00050000';

\o /results/04-product-barcode-exact.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
SELECT p.*
FROM "AppProducts" p
WHERE p."TenantId" = :'tenant_a' AND NOT p."IsDeleted"
  AND p."Barcode" = '8691000050000';

\o /results/05-product-oem-exact.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
SELECT p."Id", p."Name", p."Sku", o."OemCode"
FROM "AppProductOEMCodes" o
JOIN "AppProducts" p ON p."Id" = o."ProductId"
  AND p."TenantId" = :'tenant_a' AND NOT p."IsDeleted"
WHERE o."TenantId" = :'tenant_a' AND NOT o."IsDeleted"
  AND o."SearchCode" = '34116775277'
ORDER BY p."Name", p."Id";

\o /results/06-product-text-search.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
SELECT p."Id", p."Name", p."Sku", p."Barcode"
FROM "AppProducts" p
WHERE p."TenantId" = :'tenant_a' AND NOT p."IsDeleted"
  AND (p."Name" LIKE '%Fren Diski%' OR p."Sku" LIKE '%Fren Diski%' OR p."Barcode" LIKE '%Fren Diski%')
ORDER BY p."Name", p."Id"
LIMIT 50;

\o /results/07-pos-product-search.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
SELECT p."Id", p."Name", p."Sku", p."Barcode", p."SalePrice", p."PurchasePrice", p."Quantity"
FROM "AppProducts" p
WHERE p."TenantId" = :'tenant_a' AND NOT p."IsDeleted"
  AND (
    lower(p."Name") LIKE '%34116775277%'
    OR lower(p."Sku") LIKE '%34116775277%'
    OR p."Barcode" LIKE '%34116775277%'
    OR EXISTS (
      SELECT 1 FROM "AppProductOEMCodes" o
      WHERE o."ProductId" = p."Id" AND o."TenantId" = :'tenant_a'
        AND NOT o."IsDeleted" AND o."SearchCode" LIKE '%34116775277%'))
ORDER BY p."Name"
LIMIT 20;

\o /results/08-document-list.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
SELECT d."Id", d."DocNumber", d."Type", d."Status", d."Date", d."GrandTotal", bp."Name"
FROM "AppDocuments" d
JOIN "AppBusinessPartners" bp ON bp."Id" = d."BusinessPartnerId"
  AND bp."TenantId" = :'tenant_a' AND NOT bp."IsDeleted"
WHERE d."TenantId" = :'tenant_a' AND NOT d."IsDeleted"
ORDER BY d."Date" DESC, d."CreationTime" DESC
LIMIT 50;

\o /results/09-account-transaction-history.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
SELECT t.*
FROM "AppAccountTransactions" t
WHERE t."TenantId" = :'tenant_a' AND NOT t."IsDeleted"
  AND t."AccountId" = noxstock_perf.uuid('account:1:1')
ORDER BY t."TransactionDate" DESC, t."Id"
LIMIT 100;

\o /results/10-stock-movement-history.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
SELECT m.*
FROM "AppStockMovements" m
WHERE m."TenantId" = :'tenant_a' AND NOT m."IsDeleted"
  AND m."ProductId" = noxstock_perf.uuid('product:1:1')
ORDER BY m."MovementDate" DESC, m."Id"
LIMIT 100;

\o /results/11-dashboard-summary.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
SELECT
  (SELECT coalesce(sum(d."GrandTotal"), 0) FROM "AppDocuments" d
   WHERE d."TenantId" = :'tenant_a' AND NOT d."IsDeleted"
     AND d."Type" IN (1, 8) AND d."Status" IN (3, 6, 7, 8)
     AND d."Date" >= date '2026-07-01') AS monthly_revenue,
  (SELECT coalesce(sum(a."Balance"), 0) FROM "AppAccounts" a
   WHERE a."TenantId" = :'tenant_a' AND NOT a."IsDeleted") AS account_balance,
  (SELECT count(*) FROM "AppProducts" p
   WHERE p."TenantId" = :'tenant_a' AND NOT p."IsDeleted"
     AND p."TrackStock" AND p."Quantity" <= p."CriticalStockLevel") AS critical_stock_count,
  (SELECT count(*) FROM "AppDocuments" d
   WHERE d."TenantId" = :'tenant_a' AND NOT d."IsDeleted"
     AND d."Type" IN (1, 2) AND d."Status" IN (1, 2, 3)) AS pending_invoice_count,
  (SELECT count(*) FROM "AppBusinessPartners" bp
   WHERE bp."TenantId" = :'tenant_a' AND NOT bp."IsDeleted" AND bp."Type" IN (1, 3)) AS active_customer_count;

\o /results/12-critical-stock.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
SELECT p."Id", p."Name", p."Sku", p."Quantity", p."CriticalStockLevel"
FROM "AppProducts" p
WHERE p."TenantId" = :'tenant_a' AND NOT p."IsDeleted"
  AND p."TrackStock" AND p."Quantity" <= p."CriticalStockLevel"
ORDER BY p."Quantity", p."Name"
LIMIT 50;

\o /results/13-due-documents.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
SELECT d."Id", d."DocNumber", d."Type", d."DueDate",
       d."GrandTotal" - d."PaidAmount" AS outstanding, bp."Name"
FROM "AppDocuments" d
JOIN "AppBusinessPartners" bp ON bp."Id" = d."BusinessPartnerId"
  AND bp."TenantId" = :'tenant_a' AND NOT bp."IsDeleted"
WHERE d."TenantId" = :'tenant_a' AND NOT d."IsDeleted"
  AND d."Type" IN (1, 2) AND d."Status" IN (3, 8)
  AND d."DueDate" IS NOT NULL AND d."DueDate" <= date '2026-07-08'
ORDER BY d."DueDate", d."Date"
LIMIT 100;

\o /results/14-report-snapshot-envelope.json
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, FORMAT JSON)
WITH date_documents AS MATERIALIZED (
  SELECT d."Id", d."Type", d."Status", d."GrandTotal", d."ExchangeRate"
  FROM "AppDocuments" d
  WHERE d."TenantId" = :'tenant_a' AND NOT d."IsDeleted"
    AND d."Date" >= date '2026-06-01' AND d."Date" < date '2026-07-02'
), completed_items AS MATERIALIZED (
  SELECT i."DocumentId", i."ProductId", i."Quantity", i."Total", i."VatRate"
  FROM "AppDocumentItems" i
  JOIN date_documents d ON d."Id" = i."DocumentId" AND d."Status" IN (3, 6, 7, 8)
  WHERE i."TenantId" = :'tenant_a' AND NOT i."IsDeleted"
)
SELECT
  (SELECT count(*) FROM "AppProducts" p WHERE p."TenantId" = :'tenant_a' AND NOT p."IsDeleted") AS product_entities,
  (SELECT count(*) FROM "AppBusinessPartners" bp WHERE bp."TenantId" = :'tenant_a' AND NOT bp."IsDeleted") AS partner_entities,
  (SELECT count(*) FROM "AppAccounts" a WHERE a."TenantId" = :'tenant_a' AND NOT a."IsDeleted") AS account_entities,
  (SELECT count(*) FROM date_documents) AS document_entities,
  (SELECT count(*) FROM completed_items) AS item_entities,
  (SELECT count(*) FROM "AppAccountTransactions" t
   WHERE t."TenantId" = :'tenant_a' AND NOT t."IsDeleted"
     AND t."TransactionDate" >= date '2026-06-01' AND t."TransactionDate" < date '2026-07-02') AS transaction_entities,
  (SELECT count(*) FROM "AppStockMovements" m
   WHERE m."TenantId" = :'tenant_a' AND NOT m."IsDeleted"
     AND m."MovementDate" >= date '2026-06-01' AND m."MovementDate" < date '2026-07-02') AS movement_entities,
  (SELECT coalesce(sum(d."GrandTotal" * d."ExchangeRate"), 0) FROM date_documents d WHERE d."Type" IN (1, 8)) AS sales,
  (SELECT coalesce(sum(d."GrandTotal" * d."ExchangeRate"), 0) FROM date_documents d WHERE d."Type" = 2) AS purchases;

\o
