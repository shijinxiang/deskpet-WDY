param(
    [switch]$SelfContained = $false
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Publishing TranslatePet Windows App" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$publishDir = Join-Path $PSScriptRoot "publish"
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

$dotnetArgs = @(
    "publish",
    "src/TranslatePet.UI/TranslatePet.UI.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "-o", $publishDir
)

if ($SelfContained) {
    Write-Host "Mode: Self-contained (Standalone binary)..." -ForegroundColor Yellow
    $dotnetArgs += "--self-contained"
    $dotnetArgs += "true"
} else {
    Write-Host "Mode: Framework-dependent (.NET 8 runtime required)..." -ForegroundColor Yellow
    $dotnetArgs += "--self-contained"
    $dotnetArgs += "false"
}

& dotnet @dotnetArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "SUCCESS: Application published to $publishDir" -ForegroundColor Green
    Write-Host "Executable: $(Join-Path $publishDir 'TranslatePet.UI.exe')" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "ERROR: Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
}
