$ErrorActionPreference = 'Stop'
$frameworks = 'Microsoft.NET\Framework64\v4.0.30319', 'Microsoft.NET\Framework\v4.0.30319'
$compiler = $frameworks | ForEach-Object { Join-Path $env:WINDIR "$_\csc.exe" } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'The .NET Framework 4.x C# compiler is required.' }

Push-Location -LiteralPath $PSScriptRoot
try {
    New-Item -ItemType Directory -Force -Path dist | Out-Null
    & $compiler /nologo /optimize+ /codepage:65001 /target:winexe /out:dist\DeathCountdown.exe /win32manifest:src\widget.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll src\Countdown.cs
    if ($LASTEXITCODE -ne 0) { throw "C# compilation failed ($LASTEXITCODE)." }
    Get-Item -LiteralPath dist\DeathCountdown.exe
} finally {
    Pop-Location
}
