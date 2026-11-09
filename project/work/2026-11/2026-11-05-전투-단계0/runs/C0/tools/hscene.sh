#!/usr/bin/env bash
# hscene.sh <tag> <spotx> <spotz> <seconds> [pattern] : host scene near the core. AI to the spot, capture on, spawn 1 brute
# 4 east of the spot, AI off (host is the only target), host strafes east-west (pattern: "A:700 D:1200 A:1200 ...").
UI=/c/Dev/_scratch/refactor-runs/ui; T=/c/Dev/_scratch/combat-s0/tools; D=/c/Dev/_scratch/combat-s0/C0; N=$D/notes.txt
note() { echo "$(date +%T.%3N) $*" | tee -a $N; }
key() { MSYS_NO_PATHCONV=1 powershell -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w $UI/gkey.ps1)" $1 $2 >/dev/null; }
note "H$1 begin: spot $2 $3, ${4}s, pattern '${5:-}'"
bash $UI/chat.sh "/ai claude arena go $2 $3" | tee -a $N; sleep 9
MSYS_NO_PATHCONV=1 powershell -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w $T/cap.ps1)" -out "$(cygpath -w $D/cap-H$1)" -seconds $(( $4 + 12 )) > /dev/null 2>&1 &
sleep 1
bash $UI/chat.sh "/ai arena spawn 1 MushroomBrute 4" | tee -a $N; note "H$1 spawned"
bash $UI/chat.sh "/ai off" | tee -a $N; note "H$1 ai off"
key 83 300   # S: down to the row in front of the south wall
end=$((SECONDS + $4)); pat=${5:-"A:700 D:1200 A:1200 D:1200"}
while [ $SECONDS -lt $end ]; do for p in $pat; do k=${p%:*}; ms=${p#*:}; case $k in A) vk=65;; D) vk=68;; W) vk=87;; S) vk=83;; P) sleep $(awk "BEGIN{print $ms/1000}"); continue;; esac; key $vk $ms; [ $SECONDS -ge $end ] && break; done; done
note "H$1 strafing done"; wait; note "H$1 capture done"
