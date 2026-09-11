[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "calendar-cli.csproj"
$publishPath = Join-Path $PSScriptRoot "publish"

if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --output $publishPath

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

Write-Host "Published executable to $publishPath"