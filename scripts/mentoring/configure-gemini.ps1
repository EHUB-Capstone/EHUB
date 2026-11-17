#Requires -Version 5.1
param(
    [ValidateSet('gemini-3.8-flash', 'gemini-3.5-flash-lite', 'gemini-3.1-flash-lite')]
    [string]$Model = 'gemini-3.5-flash-lite',
    [switch]$DryRun,
    # Read the key from the clipboard (and clear it) instead of the hidden prompt.
    [switch]$FromClipboard
)

$ErrorActionPreference = 'Stop'
$projectPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../backend/src/EHub.Api/EHub.Api.csproj'))
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw 'EHub.Api project was not found next to this script.'
}
if ($DryRun) {
    Write-Output "Ready: User Secrets for EHub.Api; provider Gemini; primary model $Model. No key read or configuration changed."
    return
}

Write-Host 'Configure Gemini for the local E-HUB backend (Development).'
Write-Host 'Use a Free-tier project with billing disabled; this helper cannot verify billing.'
Write-Host 'Only enable transfer for professional summaries reviewed to exclude personal/confidential information.'
Write-Host 'The application masks known identifiers, but masking does not guarantee anonymity.'
Write-Host 'The key is entered securely and saved to the per-user User Secrets file, never as a command argument or log.'

$secureKey = $null
$keyPointer = [IntPtr]::Zero
$keyValue = $null
$settingsJson = $null
$newSettings = $null
$merged = $null
$existingText = $null
$process = $null
try {
    if ($FromClipboard) {
        # Copy the key in AI Studio first. Pasting into a hidden terminal prompt sometimes drops most of the key.
        $keyValue = ([string](Get-Clipboard -Raw)).Trim()
        Set-Clipboard -Value ' '
    } else {
        $secureKey = Read-Host 'Paste Gemini API key (hidden)' -AsSecureString
        $keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
        $keyValue = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer).Trim()
    }
    Write-Host "Key received: $($keyValue.Length) characters (an AI Studio key is usually about 39 and starts with AIza)."
    if ($keyValue.Length -lt 30) { throw 'The key is too short. Copy the whole key from AI Studio and try again.' }
    if ($keyValue.Length -eq 0 -or $keyValue.Length -gt 2048 -or $keyValue -match '[^\x21-\x7E]' -or
        $keyValue -match '^(https?://|GEMINI_API_KEY=|GOOGLE_API_KEY=)' -or $keyValue.StartsWith('"') -or $keyValue.StartsWith("'")) {
        throw 'Paste only the ASCII API key value from AI Studio.'
    }
    $newSettings = [ordered]@{
        'Mentoring:MatchingProvider' = 'Gemini'
        'Mentoring:Gemini:ApiKey' = $keyValue
        'Mentoring:Gemini:Model' = $Model
        'Mentoring:Gemini:FallbackModels' = 'gemini-3.1-flash-lite'
        'Mentoring:Gemini:DataTransferEnabled' = 'true'
        'Mentoring:Gemini:MinimumRequestIntervalSeconds' = '12'
    }

    # `dotnet user-secrets set` fed through stdin fails on some SDK/PowerShell combinations ("Could not parse the JSON file"),
    # so the helper updates the same per-user secrets.json file directly. Other keys are kept, and the key never appears
    # in a command line, log or repository file.
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
    foreach ($entry in $newSettings.GetEnumerator()) { $merged[$entry.Key] = $entry.Value }
    $settingsJson = $merged | ConvertTo-Json -Depth 100
    $temporaryPath = "$secretsPath.tmp"
    [System.IO.File]::WriteAllText($temporaryPath, $settingsJson, (New-Object System.Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temporaryPath -Destination $secretsPath -Force
    Write-Host 'Saved Gemini configuration in ASP.NET Core User Secrets. No key was written into the repository.'
    Write-Host 'Restart the backend in Development, then use AI preview in Subject Management and accept the reviewed assignments.'
    Write-Host 'No Gemini API request was made by this setup helper.'
}
catch {
    # Do not echo raw CLI errors, key input or secret JSON.
    # Only this script's own fixed messages about the key are shown; they never contain the key itself.
    $knownProblems = @('The key is too short. Copy the whole key from AI Studio and try again.', 'Paste only the ASCII API key value from AI Studio.')
    if ($knownProblems -contains $_.Exception.Message) { Write-Error $_.Exception.Message }
    else { Write-Error 'Gemini configuration was not completed. Check the API project, the secrets file and the copied key value.' }
    exit 1
}
finally {
    $settingsJson = $null
    $keyValue = $null
    $newSettings = $null
    $merged = $null
    $existingText = $null
    $property = $null
    $entry = $null
    if ($keyPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer) }
    if ($null -ne $secureKey) { $secureKey.Dispose() }
    if ($null -ne $process) { $process.Dispose() }
}
