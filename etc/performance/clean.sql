\set ON_ERROR_STOP on

BEGIN;

CREATE SCHEMA IF NOT EXISTS noxstock_perf;

CREATE OR REPLACE FUNCTION noxstock_perf.uuid(input text)
RETURNS uuid
LANGUAGE sql
IMMUTABLE
STRICT
AS $$ SELECT md5(input)::uuid $$;

CREATE TEMP TABLE perf_tenants(id uuid PRIMARY KEY) ON COMMIT DROP;
INSERT INTO perf_tenants(id) VALUES
  ('10000000-0000-0000-0000-000000000001'),
  ('10000000-0000-0000-0000-000000000002');

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

DROP TABLE IF EXISTS noxstock_perf.seed_manifest;

COMMIT;

VACUUM (ANALYZE) "AppProducts";
VACUUM (ANALYZE) "AppProductOEMCodes";
VACUUM (ANALYZE) "AppDocuments";
VACUUM (ANALYZE) "AppDocumentItems";
VACUUM (ANALYZE) "AppStockMovements";
VACUUM (ANALYZE) "AppAccountTransactions";

SELECT 'performance data cleaned' AS result;
