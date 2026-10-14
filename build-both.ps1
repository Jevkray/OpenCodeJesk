# Собирает оба бренда из одной кодовой базы: OpenCode Studio (без флага) и
# OpenCode Jesk (-p:Brand=Jesk). В рабочем дереве остаётся Jesk-манифест.
$ErrorActionPreference = 'Stop'

$root      = $PSScriptRoot
$vsDir     = Join-Path $root 'vs'
$manifest  = Join-Path $vsDir 'source.extension.vsixmanifest'
$studio    = Join-Path $vsDir 'source.studio.extension.vsixmanifest'
$jesk      = Join-Path $vsDir 'source.jesk.extension.vsixmanifest'
$project   = Join-Path $vsDir 'OpenCodeStudio.csproj'
$outDir    = Join-Path $vsDir 'bin\Release\net472'
$targetDir = 'C:\Users\semmi\Desktop\Текущие проекты (сборки)\OpenCodeStudio'
$desktop   = [Environment]::GetFolderPath('Desktop')

# 1. Сохранить текущий (Jesk) манифест, если копии ещё нет.
if (-not (Test-Path -LiteralPath $jesk)) {
    Copy-Item -LiteralPath $manifest -Destination $jesk
    Write-Host "Создан Jesk-манифест: $jesk"
}

function Build-Brand([string]$brand, [string]$manifestSource, [string]$destName) {
    Copy-Item -LiteralPath $manifestSource -Destination $manifest -Force
    if ($brand) {
        dotnet build $project -c Release -t:Rebuild -p:Brand=$brand | Out-Host
    } else {
        dotnet build $project -c Release -t:Rebuild | Out-Host
    }
    if ($LASTEXITCODE -ne 0) { throw "Сборка ($brand) не удалась (код $LASTEXITCODE)" }

    $vsix = Join-Path $outDir 'OpenCodeStudio.vsix'
    if (-not (Test-Path -LiteralPath $vsix)) { throw "VSIX не найден: $vsix" }
    $dest = Join-Path $root $destName
    Copy-Item -LiteralPath $vsix -Destination $dest -Force
    Write-Host "Готово: $dest ($([math]::Round((Get-Item -LiteralPath $dest).Length/1MB,2)) MB)"
    return $dest
}

# 2. Studio
$studioVsix = Build-Brand '' $studio 'OpenCodeStudio-1.2.0.14.vsix'
# 3. Jesk
$jeskVsix = Build-Brand 'Jesk' $jesk 'OpenCodeJesk-1.2.0.14.vsix'
# 4. В дереве остаётся Jesk-манифест (после шага 3 он уже Jesk).

# 5. Разложить и почистить прошлую версию.
foreach ($dir in @($root, $targetDir, $desktop)) {
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    # В корне репозитория файлы уже созданы сборкой — копировать их самих в себя не нужно.
    if (-not [string]::Equals($dir, $root, [System.StringComparison]::OrdinalIgnoreCase)) {
        if ([string]::Equals($dir, $targetDir, [System.StringComparison]::OrdinalIgnoreCase)) { Copy-Item -LiteralPath $studioVsix -Destination $dir -Force }
        Copy-Item -LiteralPath $jeskVsix   -Destination $dir -Force
    }
    Get-ChildItem -LiteralPath $dir -Filter 'OpenCode*-1.2.0.13.vsix' | Remove-Item -Force
}
Write-Host "Оба VSIX скопированы в: $targetDir и $desktop"
