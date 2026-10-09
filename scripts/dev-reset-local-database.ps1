param(
    [switch]$IUnderstandThisDeletesLocalData
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repositoryRoot 'docker-compose.local.yml'

if (-not $IUnderstandThisDeletesLocalData) {
    throw 'Refusing to reset data. Re-run with -IUnderstandThisDeletesLocalData after confirming this is the local Development database.'
}

if (-not (Test-Path -LiteralPath $composeFile)) {
    throw "Local compose file was not found at $composeFile"
}

if ($env:ASPNETCORE_ENVIRONMENT -and $env:ASPNETCORE_ENVIRONMENT -ne 'Development') {
    throw "Refusing to reset while ASPNETCORE_ENVIRONMENT is '$env:ASPNETCORE_ENVIRONMENT'."
}

Write-Warning 'This deletes every Docker volume declared by docker-compose.local.yml. It cannot target production because it uses the repository-local compose file.'
docker compose -f $composeFile down --volumes
docker compose -f $composeFile up -d

Write-Host 'Local containers restarted with a clean database. The API will apply migrations and seed Development data on startup.'
