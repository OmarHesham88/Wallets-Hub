[CmdletBinding()]
param(
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"
$repository = $PSScriptRoot
$safeDirectory = $repository.Replace("\", "/")
$actionsUrl = "https://github.com/OmarHesham88/Wallets-Hub/actions"
$releaseUrl = "https://github.com/OmarHesham88/Wallets-Hub/releases/tag/android-preview"
$apkUrl = "https://github.com/OmarHesham88/Wallets-Hub/releases/download/android-preview/wallets-hub-preview.apk"

$configuredDirectories = @(git config --global --get-all safe.directory 2>$null)
if ($configuredDirectories -notcontains $safeDirectory) {
    git config --global --add safe.directory $safeDirectory
    if ($LASTEXITCODE -ne 0) { throw "Could not mark the Wallets Hub repository as safe." }
}

$branch = (git -C $repository branch --show-current).Trim()
if ($LASTEXITCODE -ne 0 -or $branch -ne "main") {
    throw "Wallets Hub must be on the main branch before publishing. Current branch: $branch"
}

$changes = @(git -C $repository status --porcelain)
if ($LASTEXITCODE -ne 0) { throw "Could not inspect the Wallets Hub repository." }
if ($changes.Count -gt 0) {
    Write-Host "Uncommitted Wallets Hub files were found:" -ForegroundColor Yellow
    $changes | ForEach-Object { Write-Host "  $_" }
    throw "Ask Codex to commit these files before publishing."
}

Write-Host "Publishing Wallets Hub main..." -ForegroundColor Cyan
git -C $repository push --set-upstream origin main
if ($LASTEXITCODE -ne 0) { throw "GitHub push failed." }

Write-Host ""
Write-Host "Push complete. GitHub is building the web app, API, and APK." -ForegroundColor Green
Write-Host "Actions:  $actionsUrl"
Write-Host "Release:  $releaseUrl"
Write-Host "APK:      $apkUrl"

if (-not $NoBrowser) {
    Start-Process $actionsUrl
}
