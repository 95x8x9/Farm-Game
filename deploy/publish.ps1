param(
    [string]$Branch = "codex/deploy-production",
    [string]$Message = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$unityExe = "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe"
$buildOutput = Join-Path $repoRoot "Builds\Web"
$deployWeb = Join-Path $repoRoot "deploy\web"

if (-not (Test-Path $unityExe)) {
    throw "Unity Editor not found: $unityExe"
}

Push-Location $repoRoot
try {
    $currentBranch = (git branch --show-current).Trim()
    if ($currentBranch -ne $Branch) {
        throw "Run this script from $Branch (current: $currentBranch)."
    }

    if (git status --porcelain) {
        throw "The deployment worktree must be clean before publishing."
    }

    git fetch origin main
    git merge --no-edit origin/main
    if ($LASTEXITCODE -ne 0) {
        throw "Could not merge origin/main into $Branch."
    }

    $unityArgs = @(
        "-batchmode",
        "-quit",
        "-projectPath", $repoRoot,
        "-executeMethod", "FarmGame.Editor.WebBuildCommand.BuildRelease",
        "-logFile", (Join-Path $repoRoot "Logs\web-build.log")
    )
    $unityProcess = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -Wait -PassThru

    if ($unityProcess.ExitCode -ne 0 -or -not (Test-Path (Join-Path $buildOutput "index.html"))) {
        throw "Unity WebGL build failed. Check Logs/web-build.log."
    }

    # Unity may toggle editor-only project settings during a headless build.
    # The worktree was verified clean above, so these are safe to restore.
    git restore -- ProjectSettings/ProjectSettings.asset ProjectSettings/UnityConnectSettings.asset

    New-Item -ItemType Directory -Force -Path $deployWeb | Out-Null
    $resolvedDeployWeb = (Resolve-Path $deployWeb).Path
    if (-not $resolvedDeployWeb.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe deploy/web path: $resolvedDeployWeb"
    }

    Get-ChildItem -LiteralPath $deployWeb -Force | Remove-Item -Recurse -Force
    Copy-Item -Path (Join-Path $buildOutput "*") -Destination $deployWeb -Recurse -Force

    git add deploy/web
    if (-not (git status --porcelain)) {
        Write-Host "No deployment changes to publish."
        exit 0
    }

    if ([string]::IsNullOrWhiteSpace($Message)) {
        $Message = "deploy: publish WebGL build $((Get-Date).ToString('yyyy-MM-dd HH:mm'))"
    }

    git commit -m $Message
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to commit deployment artifacts."
    }

    git push -u origin $Branch
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to push $Branch."
    }

    Write-Host "Published $Branch."
}
finally {
    Pop-Location
}
