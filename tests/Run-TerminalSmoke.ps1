param([string]$V1 = "$env:APPDATA\npm\node_modules\opencode-ai\bin\opencode.exe",
      [string]$V2 = "$env:APPDATA\npm\node_modules\@opencode-ai\cli\bin\opencode2.exe")
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$bin = Join-Path $repo 'vs\bin\Release\net472'
if (-not (Test-Path -LiteralPath $bin)) { throw 'Сначала соберите проект в Release.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /out:"$bin\TerminalSmoke.exe" /reference:"$bin\OpenCodeStudio.dll" /reference:"$bin\Newtonsoft.Json.dll" /reference:System.Net.Http.dll "$PSScriptRoot\TerminalSmoke.cs"
if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать проверку терминала.' }
& "$bin\TerminalSmoke.exe" $V1 $V2 "$env:APPDATA\npm\opencode.cmd" "$env:APPDATA\npm\opencode2.cmd"
if ($LASTEXITCODE -ne 0) { throw 'Проверка терминала не прошла.' }
$wpf = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
& $compiler /nologo /platform:x64 /out:"$bin\TerminalViewSmoke.exe" /reference:System.Xaml.dll /reference:"$bin\OpenCodeStudio.dll" /reference:"$bin\Newtonsoft.Json.dll" /reference:"$bin\Microsoft.Web.WebView2.Core.dll" /reference:"$bin\Microsoft.Web.WebView2.Wpf.dll" /reference:"$wpf\WindowsBase.dll" /reference:"$wpf\PresentationFramework.dll" /reference:"$wpf\PresentationCore.dll" "$PSScriptRoot\TerminalViewSmoke.cs"
if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать проверку WebView2.' }
& "$bin\TerminalViewSmoke.exe"
if ($LASTEXITCODE -ne 0) { throw 'Проверка WebView2 не прошла.' }
