#!/usr/bin/env bash
# round.sh <n> <x> <z> [radius] : one baseline round (arena go -> look/inv -> spawn 1 MushroomBrute -> kill / down / 120 s -> inv/look)
# every wait counts the log lines BEFORE the chat line is sent (the first version counted after and missed instant replies)
UI=/c/Dev/_scratch/refactor-runs/ui; N=/c/Dev/_scratch/combat-baseline/notes.txt; R=${4:-5}
L="$USERPROFILE/AppData/LocalLow/Pugstorm/Core Keeper/Player.log"
note() { echo "$(date +%T) $*" | tee -a $N; }
waitfrom() { local s=$1 pat=$2 lim=$3 end=$((SECONDS + $3)); while [ $SECONDS -lt $end ]; do
  m=$(tail -n +$((s + 1)) "$L" | grep -a -E -e "$pat" | head -1); if [ -n "$m" ]; then echo "${m:0:300}" | tee -a $N; return 0; fi
  timeout 1 tail -f /dev/null 2>/dev/null; done; echo "TIMEOUT $pat" | tee -a $N; return 1; }
sayw() { local s=$(wc -l < "$L"); bash $UI/chat.sh "$1" | tee -a $N; waitfrom $s "$2" $3; }
note "R$1 begin: arena go $2 $3, spawn radius $R"
sayw "/ai claude arena go $2 $3" "TEST-ONLY arena go" 20; sleep 8
sayw "/ai look" "SKILL 보기 DONE" 15
sayw "/ai inv" "SKILL 가방 DONE" 15
start=$(wc -l < "$L")
sayw "/ai arena spawn 1 MushroomBrute $R" "TEST-ONLY arena spawn" 20
end=$((SECONDS + 120)); why=timeout
while [ $SECONDS -lt $end ]; do
  new=$(tail -n +$((start + 1)) "$L")
  if echo "$new" | grep -aq "VITALS: DOWN #"; then why=down; break; fi
  if echo "$new" | grep -aqE "SKILL 공격 DONE.*처치|MushroomBrute hp [0-9]+->0"; then why=kill; break; fi
  timeout 1 tail -f /dev/null 2>/dev/null
done
note "R$1 end: $why (wall $((120 - (end - SECONDS)))s after spawn)"
s2=$(wc -l < "$L")
sayw "/ai inv" "SKILL 가방 DONE" 15
if [ $why = down ]; then waitfrom $s2 "stood up again" 60; sleep 2; fi
sayw "/ai look" "SKILL 보기 DONE" 15
note "R$1 done"
