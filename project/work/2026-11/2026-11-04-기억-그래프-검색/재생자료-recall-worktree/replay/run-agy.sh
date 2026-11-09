cd /c/Dev/CoreKeeperModSDK-recall/Tools/ai-bridge
for m in a b; do node replay.mjs run --set data/replay/set16 --mode $m --member gemini --label gemini-flash --port 8392; done
for m in a b; do node replay.mjs run --set data/replay/set16 --mode $m --member gemini --model gpt-oss-120b-medium --label gpt-oss --port 8392; done
echo AGY-DONE
