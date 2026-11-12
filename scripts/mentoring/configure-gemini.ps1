#Requires -Version 5.1
param(
    [ValidateSet('gemini-3.8-flash', 'gemini-3.5-flash-lite', 'gemini-3.1-flash-lite')]
    [string]$Model = 'gemini-3.8-flash',
    [switch]$DryRun
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
Write-Host 'The key is entered securely and sent to dotnet through stdin, never as a command argument or log.'

$secureKey = $null
$keyPointer = [IntPtr]::Zero
$keyValue = $null
$settingsJson = $null
$process = $null
try {
    $secureKey = Read-Host 'Paste Gemini API key (hidden)' -AsSecureString
    $keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
    $keyValue = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer).Trim()
    if ($keyValue.Length -eq 0 -or $keyValue.Length -gt 2048 -or $keyValue -match '[^\x21-\x7E]' -or
        $keyValue -match '^(https?://|GEMINI_API_KEY=|GOOGLE_API_KEY=)' -or $keyValue.StartsWith('"') -or $keyValue.StartsWith("'")) {
        throw 'Paste only the ASCII API key value from AI Studio.'
    }
    $settingsJson = @{
        'Mentoring:MatchingProvider' = 'Gemini'
        'Mentoring:Gemini:ApiKey' = $keyValue
        'Mentoring:Gemini:Model' = $Model
        'Mentoring:Gemini:FallbackModels' = 'gemini-3.5-flash-lite,gemini-3.1-flash-lite'
        'Mentoring:Gemini:DataTransferEnabled' = 'true'
        'Mentoring:Gemini:MinimumRequestIntervalSeconds' = '12'
    } | ConvertTo-Json -Compress

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'dotnet'
    # This argument contains only the repository's project path. The key goes through stdin below.
    $startInfo.Arguments = 'user-secrets set --project "' + $projectPath + '"'
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.StandardInput.WriteLine($settingsJson)
    $process.StandardInput.Close()
    $process.WaitForExit()
    [void]$stdoutTask.GetAwaiter().GetResult()
    [void]$stderrTask.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0) { throw 'dotnet User Secrets could not save the configuration.' }
    Write-Host 'Saved Gemini configuration in ASP.NET Core User Secrets. No key was written into the repository.'
    Write-Host 'Restart the backend in Development, then use AI preview in Subject Management and accept the reviewed assignments.'
    Write-Host 'No Gemini API request was made by this setup helper.'
}
catch {
    # Do not echo raw CLI errors, key input or secret JSON.
    Write-Error 'Gemini configuration was not completed. Check dotnet availability, the API project and the copied key value.'
    exit 1
}
finally {
    $settingsJson = $null
    $keyValue = $null
    if ($keyPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer) }
    if ($null -ne $secureKey) { $secureKey.Dispose() }
    if ($null -ne $process) { $process.Dispose() }
}
