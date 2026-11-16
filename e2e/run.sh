#!/usr/bin/env sh
# Usage: e2e/run.sh [quick|all] [load-seconds]
set -u
compose="$(dirname "$0")/../docker-compose.e2e.yml"
export E2E_SCOPE="${1:-quick}"
export E2E_LOAD_SECONDS="${2:-60}"

docker compose -f "$compose" up --build --abort-on-container-exit --exit-code-from e2e-tests
exit_code=$?
docker compose -f "$compose" down --volumes --remove-orphans > /dev/null
exit "$exit_code"
