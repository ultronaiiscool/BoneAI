param([int]$Port = 4500)
$ErrorActionPreference = 'Stop'
$codex = Get-Command codex -ErrorAction Stop
Write-Host "Starting the localhost-only Codex App Server on port $Port."
Write-Host "Keep this window open while using BONELAB AI Agent."
& $codex.Source app-server --listen "ws://127.0.0.1:$Port"
