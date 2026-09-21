#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_dir/../.." && pwd)"
compose_file="$script_dir/docker-compose.postgresql.yml"
compose=(docker compose -f "$compose_file")

cleanup() {
  "${compose[@]}" down --volumes
}
trap cleanup EXIT

"${compose[@]}" up --detach --wait

export NOXSTOCK_POSTGRES_TEST_CONNECTION="Host=localhost;Port=55432;Database=noxstock_concurrency_test;Username=noxstock_test;Password=noxstock_test"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp/codex-dotnet}"

dotnet test "$repository_root/test/NoxStock.EntityFrameworkCore.Tests/NoxStock.EntityFrameworkCore.Tests.csproj" \
  -c Debug \
  --filter "FullyQualifiedName~PostgreSqlCommercialConcurrencyTests|FullyQualifiedName~Tenant_A_Cannot_Approve_Tenant_B_Sale_Invoice_Or_Change_Its_Stock" \
  --no-restore \
  -m:1
