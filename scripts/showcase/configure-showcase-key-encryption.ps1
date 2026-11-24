#Requires -Version 5.1
param(
    [switch]$DryRun
)

# Lets users of the Showcase assistant save their own Gemini key to their account, encrypted on the server.
# This helper creates the server-side encryption key for the LOCAL backend (Development) and keeps it in the per-user
# User Secrets file. It never prints the key, never passes it on a command line and never writes it into the repository.
# If a key is already configured it changes nothing, because replacing it would make every key saved so far unreadable.

$ErrorActionPreference = 'Stop'
$projectPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../backend/src/EHub.Api/EHub.Api.csproj'))
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw 'EHub.Api project was not found next to this script.'
}
if ($DryRun) {
    Write-Output 'Ready: would create a 256-bit encryption key for saved Showcase assistant keys in the EHub.Api User Secrets, unless one exists. Nothing was changed.'
    return
}

$activeName = 'Showcase:Assistant:KeyEncryption:ActiveKeyId'
$keyName = 'Showcase:Assistant:KeyEncryption:Keys:1'
$bytes = $null
$encoded = $null
$merged = $null
$existingText = $null
try {
    $secretsId = ([xml](Get-Content -LiteralPath $projectPath -Raw)).Project.PropertyGroup.UserSecretsId |
        Where-Object { $_ } | Select-Object -First 1
    if (-not $secretsId -or $secretsId -notmatch '^[A-Za-z0-9._-]+$') { throw 'UserSecretsId was not found in EHub.Api.csproj.' }
    $secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretsId\secrets.json"
    $merged = [ordered]@{}
    if (Test-Path -LiteralPath $secretsPath -PathType Leaf) {
        $existingText = [System.IO.File]::ReadAllText($secretsPath)
        # A file that cannot be parsed stops here, before anything is written.
        if ($existingText.Trim().Length -gt 0) {
            foreach ($property in ($existingText | ConvertFrom-Json).PSObject.Properties) { $merged[$property.Name] = $property.Value }
        }
    } else {
        [void](New-Item -ItemType Directory -Force -Path (Split-Path -Parent $secretsPath))
    }
    $alreadyConfigured = -not [string]::IsNullOrWhiteSpace([string]$merged[$activeName]) -or
        -not [string]::IsNullOrWhiteSpace([string]$merged[$keyName])
    if ($alreadyConfigured) {
        Write-Host 'An encryption key for saved Showcase assistant keys is already configured. Nothing was changed.'
        return
    }

    $bytes = New-Object byte[] 32
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    $encoded = [Convert]::ToBase64String($bytes)
    $merged[$activeName] = '1'
    $merged[$keyName] = $encoded
    $temporaryPath = "$secretsPath.tmp"
    [System.IO.File]::WriteAllText($temporaryPath, ($merged | ConvertTo-Json -Depth 100), (New-Object System.Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temporaryPath -Destination $secretsPath -Force
    Write-Host 'Saved a new encryption key in ASP.NET Core User Secrets. It was not printed and nothing was written into the repository.'
    Write-Host 'Restart the backend in Development; users can then choose "My account" when they enter their Gemini key.'
    Write-Host 'Keep this key: keys users saved with it cannot be read without it. Database backups hold only encrypted keys.'
}
catch {
    # Do not echo raw errors, which could include secret JSON.
    Write-Error 'The encryption key was not configured. Check the API project and the User Secrets file, then run this helper again.'
    exit 1
}
finally {
    if ($null -ne $bytes) { [Array]::Clear($bytes, 0, $bytes.Length) }
    $encoded = $null
    $merged = $null
    $existingText = $null
    $property = $null
}
