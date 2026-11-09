#!/usr/bin/env bash
# chat.sh "<line>" : one chat line into Core Keeper (safechat2 checks the foreground and the open chat box)
cd "$(dirname "$0")"
MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*' powershell -NoProfile -ExecutionPolicy Bypass -File safechat2.ps1 "$1"
