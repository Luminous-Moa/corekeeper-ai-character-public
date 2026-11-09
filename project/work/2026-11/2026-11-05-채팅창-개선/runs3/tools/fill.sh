#!/usr/bin/env bash
# fill.sh <tag> : the same chat fill for each setting — 12 host lines, @Grok one line, /ai stop (4 named system lines),
# then input open + full screenshot, @G + Tab screenshots, clear and close.
cd "$(dirname "$0")"; P="powershell -NoProfile -ExecutionPolicy Bypass -File"; S=../shots; t=$1
for i in 01 02 03 04 05 06 07 08 09 10 11 12; do bash say.sh "host line $i - chat window test 채팅 시험 줄 $i" >/dev/null; timeout 0.5 tail -f /dev/null; done
bash say.sh "@Grok 채팅창 시험이야. 한 줄로 짧게 인사해 줘" >/dev/null
bash waitlog.sh "@Grok chat reply: \[Grok\]" 60 | cut -c1-160
bash say.sh "/ai stop" >/dev/null; timeout 1.5 tail -f /dev/null
$P ui.ps1 shot 1 >/dev/null; cp shot.png $S/$t-1-filled-closed.png
$P gkey.ps1 13 >/dev/null; timeout 1 tail -f /dev/null; $P ui.ps1 shot 1 >/dev/null; cp shot.png $S/$t-2-filled-open.png
$P kc.ps1 type "@G" >/dev/null; timeout 1 tail -f /dev/null; $P ui.ps1 shot 1 >/dev/null; cp shot.png $S/$t-3-hint.png
$P kc.ps1 vk 9 >/dev/null; timeout 1 tail -f /dev/null; $P ui.ps1 shot 1 >/dev/null; cp shot.png $S/$t-4-tab.png
for i in 1 2 3 4 5 6 7 8 9 10; do $P kc.ps1 vk 8 >/dev/null; done; $P gkey.ps1 27 >/dev/null
echo "$(date +%H:%M:%S) fill done $t" >> ../notes.txt
