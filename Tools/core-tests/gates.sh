#!/usr/bin/env bash
# gates.sh — the offline gates of every refactor step (plan 2-계획.md C.0), one summary line each.
#   bash Tools/core-tests/gates.sh [--unity] [--tag <name>]
# --unity also runs the Unity batchmode compile (HeadlessSdk.CompileCheck, no install; ~2 min; Unity must not have
# this project open; it refuses, as a failed gate, while CoreKeeper.exe or Unity.exe runs). KG_DATA defaults to Tools/knowledge/data (made by Tools/knowledge/build.mjs; memgraph is
# skipped with a note when it is missing). Exit code = number of failed gates.
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
UNITY="/c/Program Files/Unity/Hub/Editor/6000.0.59f2/Editor/Unity.exe"
TAG="gates"; RUN_UNITY=0
while [ $# -gt 0 ]; do case "$1" in --unity) RUN_UNITY=1 ;; --tag) TAG="$2"; shift ;; esac; shift; done
export KG_DATA="${KG_DATA:-$ROOT/Tools/knowledge/data}"
FAIL=0
line() { printf '%-22s %s\n' "$1" "$2"; }
gate() { # name, ok-regex, command...
  local name="$1" re="$2"; shift 2
  local out; out="$("$@" 2>&1)"; local code=$?
  local last; last="$(printf '%s\n' "$out" | grep -E "$re" | tail -1)"
  if [ $code -eq 0 ] && [ -n "$last" ]; then line "$name" "OK   $last"; else line "$name" "FAIL (exit $code) $(printf '%s\n' "$out" | grep -E 'FAIL|error|오류' | head -3 | tr '\n' ' ')"; FAIL=$((FAIL+1)); fi
}
gate compile-check '오류 0개|0 Error' dotnet build "$ROOT/Tools/compile-check" -c Release -nologo
gate core-tests 'ALL CHECKS OK' dotnet run -c Release --project "$ROOT/Tools/core-tests"
gate exp-sim 'ALL CHECKS OK' dotnet run -c Release --project "$ROOT/Tools/exp-sim"
gate recall-loop 'CHECKS OK' node "$ROOT/Tools/ai-bridge/test/recall-loop.test.mjs"
gate bridge-wiring 'CHECKS OK' node "$ROOT/Tools/ai-bridge/test/bridge-wiring.test.mjs"
if [ -f "$KG_DATA/current.txt" ]; then gate memgraph 'CHECKS OK' node "$ROOT/Tools/ai-bridge/test/memgraph.test.mjs"; else line memgraph "SKIPPED (no KG data at $KG_DATA)"; fi
gate protocol 'CHECKS OK' node "$ROOT/Tools/ai-bridge/test/protocol.test.mjs"
gate fake-bridge 'SELFTEST OK' node "$ROOT/Tools/ai-bridge/test/fake-bridge.mjs" selftest
gate logshape 'SELFTEST OK' node "$ROOT/Tools/core-tests/logshape.mjs" selftest
if [ -f "$ROOT/Tools/core-tests/movecheck.mjs" ]; then gate movecheck 'SELFTEST OK' node "$ROOT/Tools/core-tests/movecheck.mjs" selftest; fi
if [ $RUN_UNITY -eq 1 ] && tasklist 2>/dev/null | grep -qiE '^(CoreKeeper|Unity)\.exe'; then
  # the game or a Unity editor is running (another conversation's live run may be on): never compile then
  line unity-compile "FAIL not run: CoreKeeper.exe or Unity.exe is running (rerun when both are closed)"; FAIL=$((FAIL+1))
elif [ $RUN_UNITY -eq 1 ]; then
  mkdir -p "$ROOT/Logs"; LOG="$ROOT/Logs/compile-$TAG.log"
  "$UNITY" -batchmode -quit -projectPath "$(cygpath -w "$ROOT")" -executeMethod HeadlessSdk.CompileCheck -logFile "$(cygpath -w "$LOG")" >/dev/null 2>&1; code=$?
  n=$(grep -c "error CS" "$LOG" 2>/dev/null); ok=$(grep -c "CompileCheck: ok" "$LOG" 2>/dev/null)
  if [ $code -eq 0 ] && [ "$n" = "0" ] && [ "$ok" != "0" ]; then line unity-compile "OK   exit 0, error CS 0 ($LOG)"; else line unity-compile "FAIL exit $code, error CS $n ($LOG)"; FAIL=$((FAIL+1)); fi
fi
echo "failed gates: $FAIL"
exit $FAIL
