#!/bin/sh
set -eu

profile="${NOXSTOCK_PERF_PROFILE:-small}"
action="${NOXSTOCK_PERF_ACTION:-seed}"
anchor_date="${NOXSTOCK_PERF_ANCHOR_DATE:-2026-07-01}"

case "$profile" in
  small)
    products=1000; oem_per_product=2; partners=200; documents=500
    items_per_document=3; movements=2000; transactions=1000; accounts=12
    ;;
  medium)
    products=25000; oem_per_product=2; partners=2500; documents=10000
    items_per_document=4; movements=50000; transactions=20000; accounts=20
    ;;
  target)
    products=100000; oem_per_product=3; partners=10000; documents=50000
    items_per_document=4; movements=200000; transactions=100000; accounts=30
    ;;
  *)
    echo "Unknown performance profile: $profile" >&2
    exit 2
    ;;
esac

if [ "$action" = "clean" ]; then
  psql -X -v ON_ERROR_STOP=1 -f /performance/clean.sql
  exit 0
fi

if [ "$action" != "seed" ]; then
  echo "Unknown performance action: $action" >&2
  exit 2
fi

echo "Seeding profile=$profile tenants=2 products_per_tenant=$products documents_per_tenant=$documents"
started_at="$(date +%s)"
psql -X -v ON_ERROR_STOP=1 \
  -v profile="$profile" \
  -v anchor_date="$anchor_date" \
  -v products="$products" \
  -v oem_per_product="$oem_per_product" \
  -v partners="$partners" \
  -v documents="$documents" \
  -v items_per_document="$items_per_document" \
  -v movements="$movements" \
  -v transactions="$transactions" \
  -v accounts="$accounts" \
  -f /performance/seed.sql
elapsed="$(( $(date +%s) - started_at ))"
echo "Performance seed completed in ${elapsed}s."
