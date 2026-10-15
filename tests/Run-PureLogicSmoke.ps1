$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$bin = Join-Path $repo 'vs\bin\Release\net472'
if (-not (Test-Path -LiteralPath $bin)) { throw 'Сначала соберите проект в Release.' }

# SpendSettings наследуется от DialogPage: для загрузки типа нужен Microsoft.VisualStudio.Shell.
$sdk = Get-ChildItem (Join-Path $env:USERPROFILE '.nuget\packages\microsoft.visualstudio.shell.15.0') -Recurse -Filter 'Microsoft.VisualStudio.Shell.15.0.dll' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\lib\\net472\\' } | Sort-Object FullName | Select-Object -Last 1
if (-not $sdk) { throw 'Не найден Microsoft.VisualStudio.Shell.15.0.dll в кэше NuGet (пакет Microsoft.VisualStudio.SDK).' }
$sdkPath = $sdk.FullName
Copy-Item -LiteralPath $sdkPath -Destination $bin -Force

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /out:"$bin\PureLogicSmoke.exe" /reference:"$bin\OpenCodeJesk.dll" /reference:"$bin\Newtonsoft.Json.dll" /reference:"$sdkPath" /reference:System.Net.Http.dll "$PSScriptRoot\PureLogicSmoke.cs"
if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать проверку чистой логики.' }

& "$bin\PureLogicSmoke.exe"
if ($LASTEXITCODE -ne 0) { throw 'Проверка чистой логики не прошла.' }
