#Requires -Version 5.1
param(
    [ValidateSet('gemini-3.5-flash-lite', 'gemini-3.1-flash-lite', 'gemini-2.5-flash-lite')]
    [string]$Model = 'gemini-3.5-flash-lite',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$projectPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../backend/src/EHub.Api/EHub.Api.csproj'))
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw 'EHub.Api project was not found next to this script.'
}
if ($DryRun) {
    Write-Output "Ready: Showcase assistant User Secrets for EHub.Api; model $Model. No key read or configuration changed."
    return
}

Write-Host 'Configure real Showcase AI for the local E-HUB backend.'
Write-Host 'The key is entered invisibly and saved in ASP.NET Core User Secrets, outside the repository.'
Write-Host 'Only published showcase content and chat questions are sent to the AI provider.'

$secureKey = $null
$keyPointer = [IntPtr]::Zero
$keyValue = $null
$settingsJson = $null
$process = $null
try {
    $secureKey = Read-Host 'Paste Gemini API key from AI Studio (hidden)' -AsSecureString
    $keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
    $keyValue = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer).Trim()
    if ($keyValue.Length -eq 0 -or $keyValue.Length -gt 2048 -or $keyValue -match '[^\x21-\x7E]' -or
        $keyValue -match '^(https?://|GEMINI_API_KEY=|GOOGLE_API_KEY=)' -or $keyValue.StartsWith('"') -or $keyValue.StartsWith("'")) {
        throw 'Paste only the API key value.'
    }
    $settingsJson = @{
        'Showcase:Assistant:Enabled' = 'true'
        'Showcase:Assistant:ApiKey' = $keyValue
        'Showcase:Assistant:Model' = $Model
        'Showcase:Assistant:FallbackModels' = 'gemini-3.1-flash-lite'
    } | ConvertTo-Json -Compress

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'dotnet'
    # The secret goes through stdin, never through command arguments or shell history.
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
    if ($process.ExitCode -ne 0) { throw 'The configuration could not be saved.' }
    Write-Host 'Saved Showcase AI configuration. Restart the backend to use real AI replies.'
    Write-Host 'No key was written into the repository. No AI request was made by this helper.'
}
catch {
    # CLI errors may contain input; never print them or the configuration JSON.
    Write-Error 'Showcase AI setup was not completed. Check dotnet and the copied API key, then run this helper again.'
    exit 1
}
finally {
    $settingsJson = $null
    $keyValue = $null
    if ($keyPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer) }
    if ($null -ne $secureKey) { $secureKey.Dispose() }
    if ($null -ne $process) { $process.Dispose() }
}
