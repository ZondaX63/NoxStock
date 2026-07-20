\set ON_ERROR_STOP on

CREATE TEMP TABLE release_integrity_results (
  check_name text PRIMARY KEY,
  result bigint NOT NULL
);

INSERT INTO release_integrity_results VALUES
  ('tenants', (SELECT count(*) FROM "AbpTenants" WHERE NOT "IsDeleted")),
  ('products', (SELECT count(*) FROM "AppProducts" WHERE NOT "IsDeleted")),
  ('invoices', (SELECT count(*) FROM "AppDocuments" WHERE NOT "IsDeleted" AND "Type" IN (1, 2))),
  ('stock_movements', (SELECT count(*) FROM "AppStockMovements" WHERE NOT "IsDeleted")),
  ('business_partners', (SELECT count(*) FROM "AppBusinessPartners" WHERE NOT "IsDeleted")),
  ('oem_product_tenant_mismatch', (
    SELECT count(*) FROM "AppProductOEMCodes" o
    JOIN "AppProducts" p ON p."Id" = o."ProductId"
    WHERE o."TenantId" IS DISTINCT FROM p."TenantId")),
  ('document_partner_tenant_mismatch', (
    SELECT count(*) FROM "AppDocuments" d
    JOIN "AppBusinessPartners" p ON p."Id" = d."BusinessPartnerId"
    WHERE d."TenantId" IS DISTINCT FROM p."TenantId")),
  ('document_item_tenant_mismatch', (
    SELECT count(*) FROM "AppDocumentItems" i
    JOIN "AppDocuments" d ON d."Id" = i."DocumentId"
    JOIN "AppProducts" p ON p."Id" = i."ProductId"
    WHERE i."TenantId" IS DISTINCT FROM d."TenantId"
       OR i."TenantId" IS DISTINCT FROM p."TenantId")),
  ('stock_movement_tenant_mismatch', (
    SELECT count(*) FROM "AppStockMovements" m
    JOIN "AppProducts" p ON p."Id" = m."ProductId"
    WHERE m."TenantId" IS DISTINCT FROM p."TenantId")),
  ('account_transaction_tenant_mismatch', (
    SELECT count(*) FROM "AppAccountTransactions" t
    JOIN "AppAccounts" a ON a."Id" = t."AccountId"
    WHERE t."TenantId" IS DISTINCT FROM a."TenantId"));

DO $$
DECLARE
  mismatch_total bigint;
BEGIN
  SELECT sum(result) INTO mismatch_total
  FROM release_integrity_results
  WHERE check_name LIKE '%_mismatch';

  IF mismatch_total <> 0 THEN
    RAISE EXCEPTION 'Restore tenant-integrity checks failed with % mismatches.', mismatch_total;
  END IF;

  IF (SELECT result FROM release_integrity_results WHERE check_name = 'tenants') < 1 THEN
    RAISE EXCEPTION 'Restore contains no pilot tenant.';
  END IF;

  IF (SELECT result FROM release_integrity_results WHERE check_name = 'products') < 1 OR
     (SELECT result FROM release_integrity_results WHERE check_name = 'invoices') < 1 OR
     (SELECT result FROM release_integrity_results WHERE check_name = 'stock_movements') < 1 OR
     (SELECT result FROM release_integrity_results WHERE check_name = 'business_partners') < 1 THEN
    RAISE EXCEPTION 'Restore lacks representative product, invoice, stock, or current-account data.';
  END IF;
END $$;

SELECT check_name, result
FROM release_integrity_results
ORDER BY check_name;
