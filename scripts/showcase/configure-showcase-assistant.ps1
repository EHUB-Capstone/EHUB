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
    Write-Output "Ready: Showcase assistant User Secrets for EHub.Api; model $Model. Nothing was changed."
    return
}

Write-Host 'Turn on the Showcase AI chat (Hubby) for the local E-HUB backend.'
Write-Host 'The server keeps no AI key for it: each user pastes their own Gemini API key in the chat.'
Write-Host 'Only published showcase content and chat questions are sent to Google, under that user key.'

# These are switches and model names, not secrets, so they can be passed as arguments.
$settings = [ordered]@{
    'Showcase:Assistant:Enabled'        = 'true'
    'Showcase:Assistant:Model'          = $Model
    'Showcase:Assistant:FallbackModels' = 'gemini-3.1-flash-lite'
}
foreach ($name in $settings.Keys) {
    & dotnet user-secrets set $name $settings[$name] --project $projectPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error 'Showcase AI setup was not completed. Check that dotnet is installed, then run this helper again.'
        exit 1
    }
}
Write-Host 'Saved. Restart the backend; users are asked for their own Gemini key the first time they open Hubby.'
