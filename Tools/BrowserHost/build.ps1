param([switch]$SkipRestore)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$projectFile = Join-Path $PSScriptRoot 'PrGame.BrowserHost.csproj'
$packageRoot = Join-Path $projectRoot 'Builds/NuGet'
$runtimeRoot = Join-Path $projectRoot 'BrowserRuntime/Windows'
if (-not $SkipRestore) {
    dotnet restore $projectFile --locked-mode --packages $packageRoot --source https://api.nuget.org/v3/index.json --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Browser dependency restore failed.' }
}
dotnet publish $projectFile -c Release --no-restore -o $runtimeRoot --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Browser host build failed.' }
$licenseRoot = Join-Path $runtimeRoot 'Licenses'
New-Item -ItemType Directory -Force $licenseRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $packageRoot 'cefsharp.common.netcore/152.0.100/LICENSE') -Destination (Join-Path $licenseRoot 'CefSharp.txt')
Copy-Item -LiteralPath (Join-Path $packageRoot 'chromiumembeddedframework.runtime.win-x64/152.0.10/LICENSE.txt') -Destination (Join-Path $licenseRoot 'CEF.txt')
foreach ($dependency in @('microsoft.netcore.app.runtime.win-x64', 'microsoft.windowsdesktop.app.runtime.win-x64')) {
    $dependencyRoot = Join-Path $packageRoot $dependency
    Get-ChildItem -LiteralPath $dependencyRoot -Directory | ForEach-Object {
        foreach ($notice in @('LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT')) {
            $noticePath = Join-Path $_.FullName $notice
            if (Test-Path -LiteralPath $noticePath) {
                Copy-Item -LiteralPath $noticePath -Destination (Join-Path $licenseRoot ($dependency + '-' + $_.Name + '-' + $notice))
            }
        }
    }
}
Write-Output "Browser runtime ready: $runtimeRoot"
