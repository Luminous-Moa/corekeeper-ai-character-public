#!/usr/bin/env bash
# var.sh <tag> <mode> : the repeated fill with one change before /ai stop.
#   hint    — open the input, type "@Gr" (the hint line shows), Backspace x3, Esc; no mention is sent
#   mention — send "@Grok 채팅창 시험이야. 한 줄로 짧게 인사해 줘" and wait for Grok's line
#   plain   — a 13th host line of other text
cd "$(dirname "$0")"; P="powershell -NoProfile -ExecutionPolicy Bypass -File"; S=../shots; t=$1
for i in 01 02 03 04 05 06 07 08 09 10 11 12; do bash say.sh "host line $i - chat window test 채팅 시험 줄 $i" >/dev/null; timeout 0.5 tail -f /dev/null; done
case $2 in
  hint) $P gkey.ps1 13 >/dev/null; timeout 0.5 tail -f /dev/null; $P kc.ps1 type "@Gr" >/dev/null; timeout 1 tail -f /dev/null; for i in 1 2 3; do $P kc.ps1 vk 8 >/dev/null; done; $P gkey.ps1 27 >/dev/null ;;
  mention) bash say.sh "@Grok 채팅창 시험이야. 한 줄로 짧게 인사해 줘" >/dev/null; bash waitlog.sh "@Grok chat reply: \[Grok\]" 60 | cut -c1-120 ;;
  plain) bash say.sh "plain extra line before stop" >/dev/null ;;
esac
bash say.sh "/ai stop" >/dev/null; timeout 1.5 tail -f /dev/null
$P ui.ps1 shot 1 >/dev/null; cp shot.png $S/$t-closed.png
$P gkey.ps1 13 >/dev/null; timeout 1 tail -f /dev/null; $P ui.ps1 shot 1 >/dev/null; cp shot.png $S/$t-open.png; $P gkey.ps1 27 >/dev/null
echo "$(date +%H:%M:%S) var done $t ($2)" >> ../notes.txt
