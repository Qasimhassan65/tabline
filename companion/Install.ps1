param([string]$ExtensionId)
$ErrorActionPreference = 'Stop'
if (-not $ExtensionId) { $ExtensionId = Read-Host 'Paste the extension ID from Tabline settings' }
$ExtensionId = $ExtensionId.Trim()
if ($ExtensionId -cnotmatch '^[a-p]{32}$') { throw 'A Chrome extension ID must contain exactly 32 lowercase letters from a to p.' }
$executable = Join-Path $PSScriptRoot 'TablineCompanion-2.2.0-curved.exe'
if (-not (Test-Path -LiteralPath $executable)) { & (Join-Path $PSScriptRoot 'Build.ps1') }
$manifestPath = Join-Path $PSScriptRoot 'com.tabline.companion.json'
$manifest = @{ name='com.tabline.companion'; description='Local Tabline Windows window previews and screenshot clipboard'; path=$executable; type='stdio'; allowed_origins=@("chrome-extension://$ExtensionId/") }
[System.IO.File]::WriteAllText($manifestPath,($manifest | ConvertTo-Json -Depth 4),(New-Object System.Text.UTF8Encoding($false)))
$registryPath = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.tabline.companion'
New-Item -Path $registryPath -Force | Out-Null
Set-Item -Path $registryPath -Value $manifestPath
Write-Output 'Connected for this Windows user. Return to Tabline settings and click Check connection.'
Write-Output 'Keep the Tabline project folder in its current location; no startup task or service was installed.'










