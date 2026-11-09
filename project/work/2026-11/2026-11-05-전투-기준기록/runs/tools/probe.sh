#!/usr/bin/env bash
# probe.sh "x,z x,z ..." : asks /ai goto for each tile and prints wall/ground/walk from the SKILL 걷기 line (tile probe, no code change)
UI=/c/Dev/_scratch/refactor-runs/ui; L="$USERPROFILE/AppData/LocalLow/Pugstorm/Core Keeper/Player.log"
for p in $1; do x=${p%,*}; z=${p#*,}
  start=$(wc -l < "$L"); bash $UI/chat.sh "/ai goto $x $z" >/dev/null
  r=""; for i in $(seq 1 12); do
    m=$(tail -n +$((start+1)) "$L" | grep -a "SKILL 걷기" | grep -a "($x,$z)" | head -1)
    if [ -n "$m" ]; then
      if echo "$m" | grep -q "top="; then r=$(echo "$m" | grep -o "top=[a-zA-Z]*"); else r="walk-start"; fi; break; fi
    sleep 0.5; done
  echo "$x,$z ${r:-none}"
done
bash $UI/chat.sh "/ai stop" >/dev/null
