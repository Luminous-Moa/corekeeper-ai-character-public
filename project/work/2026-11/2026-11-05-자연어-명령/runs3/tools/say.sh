#!/usr/bin/env bash
# say.sh "<text>" : Korean-safe chat line (UTF-8 file -> Unicode packets), Enter
cd "$(dirname "$0")"; printf '%s' "$1" > msg.txt
MSYS_NO_PATHCONV=1 powershell -NoProfile -ExecutionPolicy Bypass -File unif.ps1 -file "$(cygpath -w "$PWD/msg.txt")" -enter
