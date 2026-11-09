#!/usr/bin/env bash
# csdiff.sh <commit>: installed .cs vs commit (sha256 without CR); also prints per-file hash list
cd /c/Dev/CoreKeeperModSDK
MOD="/c/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike"
n=0; d=0
while IFS= read -r f; do rel=${f#"$MOD/Scripts/Scripts/"}; rel=${rel#"$MOD/Scripts/"}; src="Assets/AIPlayerSpike/Scripts/$rel"; n=$((n+1))
  a=$(tr -d '\r' < "$f" | sha256sum | cut -c1-16); b=$(git show "$1":"$src" 2>/dev/null | tr -d '\r' | sha256sum | cut -c1-16)
  echo "$a $rel"
  [ "$a" = "$b" ] || { d=$((d+1)); echo "DIFF $rel"; }; done < <(find "$MOD/Scripts" -name "*.cs" | sort)
echo "installed .cs $n, different from $1: $d"
