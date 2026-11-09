@echo off
rem 판단 시험 전체 다시 돌리기 (1차 145회 + 2차 흔들림 84회). 키는 OPENAI_API_KEY 환경 변수.
cd /d "%~dp0"
node run.mjs || exit /b 1
node run.mjs --perturb
