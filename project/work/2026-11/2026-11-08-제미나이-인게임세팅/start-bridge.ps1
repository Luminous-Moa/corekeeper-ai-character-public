$ErrorActionPreference = 'Stop'
$env:AI_BRIDGE_MEMBERS = 'gemini'
$env:AI_BRIDGE_PORT = '8331'
$env:AI_BRIDGE_LOGS = Join-Path $PSScriptRoot 'bridge-logs'
Remove-Item Env:AI_BRIDGE_FAKE_CHAT -ErrorAction SilentlyContinue
Remove-Item Env:AI_BRIDGE_CONFIG -ErrorAction SilentlyContinue
Set-Location -LiteralPath 'C:/Dev/CoreKeeperModSDK/Tools/ai-bridge'
& 'C:/Codex/Apps/Development-SDKs-2026-09-20/node-lts/node.exe' 'C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/bridge.mjs'
exit $LASTEXITCODE
