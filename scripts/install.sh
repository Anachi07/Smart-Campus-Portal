#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
"$DOTNET_BIN" restore queue/SmartQueueSystemGUI.csproj
"$DOTNET_BIN" build queue/SmartQueueSystemGUI.csproj --no-restore
