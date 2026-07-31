#!/usr/bin/env bash
set -euo pipefail

: "${NOXSTOCK_BASE_URL:?Set NOXSTOCK_BASE_URL, for example https://stock.example.com}"
: "${NOXSTOCK_COOKIE_FILE:?Set NOXSTOCK_COOKIE_FILE to an authenticated Netscape cookie jar}"

tenant_path="${NOXSTOCK_TENANT_PATH:-}"
runs="${NOXSTOCK_PERF_RUNS:-5}"
output="${NOXSTOCK_PERF_OUTPUT:-/tmp/noxstock-navigation-http.csv}"
paths=("/Dashboard" "/Products" "/Sales" "/Invoices" "/Reports")

printf 'phase,path,run,http_code,ttfb_seconds,total_seconds,download_bytes,server_timing,correlation_id\n' > "$output"

measure() {
    local phase="$1"
    local path="$2"
    local run="$3"
    local headers
    headers="$(mktemp)"
    local metrics
    metrics="$(curl --silent --show-error --location --compressed \
        --cookie "$NOXSTOCK_COOKIE_FILE" \
        --dump-header "$headers" \
        --output /dev/null \
        --write-out '%{http_code},%{time_starttransfer},%{time_total},%{size_download}' \
        "${NOXSTOCK_BASE_URL%/}${tenant_path}${path}")"
    local server_timing
    server_timing="$(awk 'BEGIN{IGNORECASE=1} /^server-timing:/{sub(/^[^:]+:[[:space:]]*/,""); sub(/\r$/,""); print; exit}' "$headers")"
    local correlation_id
    correlation_id="$(awk 'BEGIN{IGNORECASE=1} /^x-correlation-id:/{sub(/^[^:]+:[[:space:]]*/,""); sub(/\r$/,""); print; exit}' "$headers")"
    rm -f -- "$headers"
    printf '%s,%s,%s,%s,"%s","%s"\n' "$phase" "$path" "$run" "$metrics" "$server_timing" "$correlation_id" >> "$output"
}

for path in "${paths[@]}"; do
    measure cold "$path" 1
    for run in $(seq 1 "$runs"); do
        measure warm "$path" "$run"
    done
done

printf 'Saved HTTP navigation measurements to %s\n' "$output"
