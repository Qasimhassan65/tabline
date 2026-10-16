$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework C# compiler was not found.' }
& $compiler /nologo /target:exe /platform:x64 /optimize+ /out:"$PSScriptRoot\TablineCompanion-2.2.0-rope.exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\TablineCompanion.cs" "$PSScriptRoot\RopeForm.cs" "$PSScriptRoot\LayeredCanvas.cs" "$PSScriptRoot\ToastForm.cs" "$PSScriptRoot\TestInput.cs" "$PSScriptRoot\AppThumbnailWindow.cs"
if ($LASTEXITCODE -ne 0) { throw 'Companion build failed.' }
Write-Output 'Built TablineCompanion.exe.'











