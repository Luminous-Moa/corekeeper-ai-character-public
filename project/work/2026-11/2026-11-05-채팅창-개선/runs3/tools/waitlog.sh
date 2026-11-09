#!/usr/bin/env bash
# waitlog.sh <regex> <seconds> : waits until a NEW Player.log line (after the call) matches; prints it (or TIMEOUT)
L="$USERPROFILE/AppData/LocalLow/Pugstorm/Core Keeper/Player.log"
start=$(wc -l < "$L"); end=$((SECONDS + $2))
while [ $SECONDS -lt $end ]; do
  m=$(tail -n +$((start + 1)) "$L" | grep -a -E -e "$1" | head -1)
  if [ -n "$m" ]; then echo "${m:0:300}"; exit 0; fi
  timeout 3 tail -f /dev/null 2>/dev/null
done
echo "TIMEOUT $1"; exit 1
