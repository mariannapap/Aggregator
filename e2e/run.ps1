param(
    [ValidateSet("quick", "all")]
    [string]$Scope = "quick",
    [int]$LoadSeconds = 60
)

$ErrorActionPreference = "Stop"
$compose = Join-Path $PSScriptRoot "..\docker-compose.e2e.yml"
$env:E2E_SCOPE = $Scope
$env:E2E_LOAD_SECONDS = "$LoadSeconds"

try {
    docker compose -f $compose up --build --abort-on-container-exit --exit-code-from e2e-tests
    $exitCode = $LASTEXITCODE
}
finally {
    docker compose -f $compose down --volumes --remove-orphans | Out-Null
}

exit $exitCode
