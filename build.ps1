# Сборка единственного бренда OpenCode Jesk.
# Результат (VSIX) копируется в корень репозитория и на Рабочий стол.
$ErrorActionPreference = 'Stop'

$root     = $PSScriptRoot
$vsDir    = Join-Path $root 'vs'
$manifest = Join-Path $vsDir 'source.extension.vsixmanifest'
$project  = Join-Path $vsDir 'OpenCodeJesk.csproj'
$outDir   = Join-Path $vsDir 'bin\Release\net472'
$desktop  = [Environment]::GetFolderPath('Desktop')

# Версия берётся из манифеста.
$version = ([xml](Get-Content -LiteralPath $manifest)).PackageManifest.Metadata.Identity.Version
if (-not $version) { throw "Не удалось определить версию в $manifest" }

# 1. Чистим прошлые сборки в корне и на Рабочем столе.
foreach ($dir in @($root, $desktop)) {
    Get-ChildItem -LiteralPath $dir -Filter 'OpenCodeJesk-*.vsix'   -File -ErrorAction SilentlyContinue | Remove-Item -Force
    Get-ChildItem -LiteralPath $dir -Filter 'OpenCodeJesk-*.vsix' -File -ErrorAction SilentlyContinue | Remove-Item -Force
}

# 2. Сборка Jesk.
dotnet build $project -c Release -t:Rebuild | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Сборка не удалась (код $LASTEXITCODE)" }

$vsix = Join-Path $outDir 'OpenCodeJesk.vsix'
if (-not (Test-Path -LiteralPath $vsix)) { throw "VSIX не найден: $vsix" }

# 3. Копируем как OpenCodeJesk-<версия>.vsix в корень и на Рабочий стол.
$destName = "OpenCodeJesk-$version.vsix"
foreach ($dir in @($root, $desktop)) {
    $dest = Join-Path $dir $destName
    Copy-Item -LiteralPath $vsix -Destination $dest -Force
    Write-Host "Готово: $dest ($([math]::Round((Get-Item -LiteralPath $dest).Length / 1MB, 2)) MB)"
}
