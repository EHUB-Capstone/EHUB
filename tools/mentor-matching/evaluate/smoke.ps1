param(
    [string]$ModelPath = (Join-Path $PSScriptRoot '../../../backend/src/EHub.Infrastructure/Mentoring/Models/bootstrap-model.json')
)

# Every fixture is fictional and generated into a unique temporary directory.
# Never pass real profiles or labels to this smoke test.
$projectPath = Join-Path $PSScriptRoot 'Evaluate.csproj'
$modelFile = [System.IO.Path]::GetFullPath($ModelPath)
$tempBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$smokeFolder = Join-Path $tempBase ('ehub-mentor-evaluate-smoke-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $smokeFolder -ErrorAction Stop | Out-Null
$utf8 = [System.Text.UTF8Encoding]::new($false)
$passed = 0

function Assert-Smoke([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:passed++
}
function Write-SmokeLines([string]$Name, [string[]]$Lines) {
    [System.IO.File]::WriteAllLines((Join-Path $smokeFolder $Name), $Lines, $utf8)
}
function Run-SmokeEvaluation {
    param(
        [string]$Profiles = 'profiles.jsonl',
        [string]$Projects = 'projects.json',
        [string]$Labels = 'labels.jsonl',
        [string]$Output = 'report.json',
        [string]$Model = $modelFile,
        [int]$ExpectedExit = 0
    )
    $runArguments = @('run', '--project', $projectPath, '--no-build', '--',
        '--profiles', (Join-Path $smokeFolder $Profiles), '--projects', (Join-Path $smokeFolder $Projects),
        '--model', $Model, '--output', (Join-Path $smokeFolder $Output))
    if ($Labels) { $runArguments += @('--labels', (Join-Path $smokeFolder $Labels)) }
    # Rejected cases intentionally exit 1; capture their diagnostics rather than echoing fixtures.
    & dotnet @runArguments 2>&1 | Out-Null
    Assert-Smoke ($LASTEXITCODE -eq $ExpectedExit) ('Unexpected evaluator exit for smoke case: ' + $Profiles + '/' + $Projects + '/' + $Labels)
}

try {
    & dotnet build $projectPath --verbosity quiet
    Assert-Smoke ($LASTEXITCODE -eq 0) 'Evaluator build failed.'
    $profiles = @(
        '{"mentorRef":"S01","profile":{"mentorType":"Business","experience":"Đã phụ trách marketing và phát triển kênh bán hàng.","bio":null,"expertise":[],"startupDomains":[],"technologySkills":[],"tags":[],"experiences":[]}}',
        '{"mentorRef":"S02","profile":{"mentorType":"Business","experience":null,"bio":"Đã học marketing.","expertise":[],"startupDomains":[],"technologySkills":[],"tags":[],"experiences":[]}}',
        '{"mentorRef":"S03","profile":{"mentorType":"Business","experience":"Bảo tồn một loại gốm cổ.","bio":null,"expertise":[],"startupDomains":[],"technologySkills":[],"tags":[],"experiences":[]}}'
    )
    $projects = '{"projects":[{"projectId":"smoke-marketing","title":"Dự án kênh bán","description":"Cần tư vấn marketing."},{"projectId":"smoke-unknown","title":"Ký hiệu cổ chưa rõ","description":"Cần bảo tồn hệ ký hiệu tiền sử chưa rõ chuyên môn tương ứng."}]}'
    $labels = @(
        '{"projectId":"smoke-marketing","mentorRef":"S01","relevance":3}',
        '{"projectId":"smoke-marketing","mentorRef":"S02","relevance":1}',
        '{"projectId":"smoke-marketing","mentorRef":"S03","relevance":0}',
        '{"projectId":"smoke-unknown","mentorRef":"S01","relevance":0}',
        '{"projectId":"smoke-unknown","mentorRef":"S02","relevance":0}',
        '{"projectId":"smoke-unknown","mentorRef":"S03","relevance":0}'
    )
    Write-SmokeLines 'profiles.jsonl' $profiles
    Write-SmokeLines 'labels.jsonl' $labels
    [System.IO.File]::WriteAllText((Join-Path $smokeFolder 'projects.json'), $projects, $utf8)
    Run-SmokeEvaluation
    $report = Get-Content -LiteralPath (Join-Path $smokeFolder 'report.json') -Raw | ConvertFrom-Json
    Assert-Smoke ($report.projects[0].recommendations[0].mentorRef -eq 'S01') 'Practical experience must outrank education only.'
    Assert-Smoke ($report.projects[0].recommendations.Count -eq 2 -and $report.projects[1].recommendations.Count -eq 0) 'Runtime evidence filtering/abstention changed.'
    Assert-Smoke ($report.summary.meanNdcgAt5 -eq 1 -and $report.summary.meanRecallAt3 -eq 1 -and
        [Math]::Abs($report.summary.meanPrecisionAt3 - 1.0 / 3) -lt 1e-12 -and
        $report.summary.top1RelevantRate -eq 1 -and $report.summary.noDocumentedFitAbstentionRate -eq 1) 'Report metrics did not reconcile.'
    foreach ($pair in @(@('profilesSha256', 'profiles.jsonl'), @('projectsSha256', 'projects.json'), @('labelsSha256', 'labels.jsonl'))) {
        Assert-Smoke ($report.inputs.($pair[0]) -eq (Get-FileHash -LiteralPath (Join-Path $smokeFolder $pair[1]) -Algorithm SHA256).Hash.ToLowerInvariant()) 'Input hash did not match exact bytes.'
    }
    Assert-Smoke ($report.model.sha256 -eq (Get-FileHash -LiteralPath $modelFile -Algorithm SHA256).Hash.ToLowerInvariant()) 'Baseline model hash mismatch.'
    Run-SmokeEvaluation -Output 'repeat.json'
    $repeat = Get-Content -LiteralPath (Join-Path $smokeFolder 'repeat.json') -Raw | ConvertFrom-Json
    Assert-Smoke (($report.projects[0].recommendations | ConvertTo-Json -Depth 8 -Compress) -eq
        ($repeat.projects[0].recommendations | ConvertTo-Json -Depth 8 -Compress)) 'Repeated rank order, values or evidence differed.'
    Assert-Smoke (($report.projects[1].allCandidates.mentorRef -join ',') -eq 'S01,S02,S03') 'Exact ties must follow stable mentor references.'

    $candidate = Get-Content -LiteralPath $modelFile -Raw | ConvertFrom-Json
    $candidate.bias = [double]$candidate.bias + 0.1
    $candidateFile = Join-Path $smokeFolder 'candidate.json'
    [System.IO.File]::WriteAllText($candidateFile, ($candidate | ConvertTo-Json -Depth 20), $utf8)
    Run-SmokeEvaluation -Model $candidateFile -Output 'candidate-report.json'
    $candidateReport = Get-Content -LiteralPath (Join-Path $smokeFolder 'candidate-report.json') -Raw | ConvertFrom-Json
    Assert-Smoke ($candidateReport.model.sha256 -ne $report.model.sha256 -and
        $candidateReport.model.sha256 -eq (Get-FileHash -LiteralPath $candidateFile -Algorithm SHA256).Hash.ToLowerInvariant() -and
        $candidateReport.projects[0].recommendations[0].rankingValue -ne $report.projects[0].recommendations[0].rankingValue) 'Candidate model path/hash was not used by runtime scoring.'

    Write-SmokeLines 'incomplete-labels.jsonl' $labels[0..4]
    Run-SmokeEvaluation -Labels 'incomplete-labels.jsonl' -ExpectedExit 1
    Write-SmokeLines 'duplicate-labels.jsonl' ($labels + $labels[0])
    Run-SmokeEvaluation -Labels 'duplicate-labels.jsonl' -ExpectedExit 1
    Write-SmokeLines 'duplicate-profiles.jsonl' ($profiles + $profiles[0])
    Run-SmokeEvaluation -Profiles 'duplicate-profiles.jsonl' -ExpectedExit 1
    Write-SmokeLines 'invalid-role.jsonl' @($profiles[0].Replace('"Business"', '"Unknown"'), $profiles[1], $profiles[2])
    Run-SmokeEvaluation -Profiles 'invalid-role.jsonl' -ExpectedExit 1
    Write-SmokeLines 'missing-role.jsonl' @($profiles[0].Replace('"mentorType":"Business",', ''), $profiles[1], $profiles[2])
    Run-SmokeEvaluation -Profiles 'missing-role.jsonl' -ExpectedExit 1
    $duplicateProjects = $projects.Replace('"smoke-unknown"', '"smoke-marketing"')
    [System.IO.File]::WriteAllText((Join-Path $smokeFolder 'duplicate-projects.json'), $duplicateProjects, $utf8)
    Run-SmokeEvaluation -Projects 'duplicate-projects.json' -ExpectedExit 1
    $sourceHash = (Get-FileHash -LiteralPath (Join-Path $smokeFolder 'profiles.jsonl') -Algorithm SHA256).Hash
    Run-SmokeEvaluation -Output 'profiles.jsonl' -ExpectedExit 1
    Assert-Smoke ((Get-FileHash -LiteralPath (Join-Path $smokeFolder 'profiles.jsonl') -Algorithm SHA256).Hash -eq $sourceHash) 'Overwrite rejection changed an input.'
    Run-SmokeEvaluation -Labels '' -Output 'unlabeled.json'
    $unlabeled = Get-Content -LiteralPath (Join-Path $smokeFolder 'unlabeled.json') -Raw | ConvertFrom-Json
    Assert-Smoke ($unlabeled.summary.labeledPairs -eq 0 -and $null -eq $unlabeled.summary.meanNdcgAt5) 'Unlabeled input must not create evaluation labels.'
    @{ passed = $true; assertions = $passed; fixtures = 'fictional, generated in a unique temporary directory' } | ConvertTo-Json -Compress
}
finally {
    # Check the resolved absolute target stays in the intended temp root before recursive deletion.
    $resolvedTarget = [System.IO.Path]::GetFullPath($smokeFolder)
    $tempPrefix = $tempBase.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    if ($resolvedTarget.StartsWith($tempPrefix, $comparison) -and
        [System.IO.Path]::GetFileName($resolvedTarget).StartsWith('ehub-mentor-evaluate-smoke-')) {
        Remove-Item -LiteralPath $resolvedTarget -Recurse -Force -ErrorAction Stop
    }
    else { throw 'Refusing to delete an unexpected smoke directory.' }
}
