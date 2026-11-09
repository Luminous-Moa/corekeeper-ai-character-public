#!/usr/bin/env bash
# rep.sh <tag> : the repeated fill of run 2 (same 12 host lines + /ai stop), then the screen with the input closed
# (1.5 s after /ai stop) and opened.
cd "$(dirname "$0")"; P="powershell -NoProfile -ExecutionPolicy Bypass -File"; S=../shots; t=$1
for i in 01 02 03 04 05 06 07 08 09 10 11 12; do bash say.sh "host line $i - chat window test 채팅 시험 줄 $i" >/dev/null; timeout 0.5 tail -f /dev/null; done
bash say.sh "/ai stop" >/dev/null; timeout 1.5 tail -f /dev/null
$P ui.ps1 shot 1 >/dev/null; cp shot.png $S/$t-closed.png
$P gkey.ps1 13 >/dev/null; timeout 1 tail -f /dev/null; $P ui.ps1 shot 1 >/dev/null; cp shot.png $S/$t-open.png; $P gkey.ps1 27 >/dev/null
echo "$(date +%H:%M:%S) rep done $t" >> ../notes.txt
