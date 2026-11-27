param(
    [string]$OutputDirectory = 'artifacts\trial\win-x64',
    [switch]$CreateZip
)

$ErrorActionPreference = 'Stop'
$appDirectory = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $appDirectory 'src\FUREQ.Workspace.App\FUREQ.Workspace.App.csproj'
$outputPath = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $appDirectory $OutputDirectory))
}
$appRoot = [System.IO.Path]::GetFullPath($appDirectory)

if (-not $outputPath.StartsWith($appRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must remain inside the FUREQ Workspace application folder.'
}

dotnet publish $project --configuration Release --runtime win-x64 --self-contained true --output $outputPath
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }

$exePath = Join-Path $outputPath 'FUREQ.Workspace.App.exe'
if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) { throw 'Published executable is missing.' }
Get-ChildItem -LiteralPath $outputPath -Filter '*.pdb' -File | Remove-Item -Force

$metadata = [ordered]@{
    App = 'FUREQ Workspace'
    Version = '0.1.0-trial'
    Runtime = 'win-x64'
    SelfContained = $true
    BuiltAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    Executable = 'FUREQ.Workspace.App.exe'
    Checks = @('Startup and sign-in should be checked on a clean Windows account.', 'GitHub authentication requires interactive user approval.', 'LFS payloads are not included.')
}
$metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputPath 'trial-manifest.json') -Encoding utf8

if ($CreateZip) {
    $zipPath = "$outputPath.zip"
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $outputPath '*') -DestinationPath $zipPath
}

Write-Output "Published trial: $outputPath"
if ($CreateZip) { Write-Output "ZIP: $outputPath.zip" }
