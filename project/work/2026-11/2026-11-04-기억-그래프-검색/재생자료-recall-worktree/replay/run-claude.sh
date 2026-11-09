cd /c/Dev/CoreKeeperModSDK-recall/Tools/ai-bridge
for m in a b; do node replay.mjs run --set data/replay/set16 --mode $m --member claude --label opus --port 8391; done
for m in a b; do node replay.mjs run --set data/replay/set16 --mode $m --member claude --model claude-haiku-5-5 --effort low --label haiku --port 8391; done
echo CLAUDE-DONE
