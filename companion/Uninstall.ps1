$ErrorActionPreference = 'Stop'
$registryPath = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.tabline.companion'
if (Test-Path -LiteralPath $registryPath) { Remove-Item -LiteralPath $registryPath }
Write-Output 'Tabline native-host registration removed. No app data or other registry entries were changed.'
