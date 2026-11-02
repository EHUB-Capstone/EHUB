param([ValidateRange(1024, 65535)][int]$Port = 5174)
$ErrorActionPreference = 'Stop'
$frontend = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../frontend'))
$bundledNode = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe'
$nodeExecutable = if (Test-Path -LiteralPath $bundledNode) { $bundledNode } else { (Get-Command node -ErrorAction Stop).Source }
$previousMocks = [Environment]::GetEnvironmentVariable('VITE_ENABLE_API_MOCKS', 'Process')
try {
    [Environment]::SetEnvironmentVariable('VITE_ENABLE_API_MOCKS', 'false', 'Process')
    Push-Location -LiteralPath $frontend
    & $nodeExecutable (Join-Path $frontend 'node_modules/vite/bin/vite.js') --host 127.0.0.1 --port $Port --strictPort
    if ($LASTEXITCODE -ne 0) { throw 'The UI demo frontend stopped with an error.' }
}
finally {
    Pop-Location
    [Environment]::SetEnvironmentVariable('VITE_ENABLE_API_MOCKS', $previousMocks, 'Process')
}
