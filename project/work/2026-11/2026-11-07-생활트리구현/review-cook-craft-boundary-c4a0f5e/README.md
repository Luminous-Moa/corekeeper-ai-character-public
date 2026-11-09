# c4a0f5e 수정 영향 독립 표본

고정 커밋: c4a0f5e38016c79567f880bbaea5e56e0eeae347. 각 production/boundary 소스는 git show 고정판으로 복사했다. fixed-git-blobs.txt에 원본 Git blob ID를 남겼다. 제품 파일은 수정하지 않았다.

craft는 고정 기존 Program의 Setup을 재사용하고 Main만 두 표본으로 한정했다. 전체 AIBrain.Prep.cs 및 AICraft/AIInventory/Core를 직접 컴파일한다. 기존 TestPrep wrapper는 실제 WantedItems를 호출한다. AILife binding은 원문 prefix 추출이며 life-prefix-check.txt에 정규화 비교 결과를 남겼다. Wood2/요청Torch3/출력2의 직접·트리 key/station/Calls/amount/예상출력/비용 및 불필요부족없음, Wood1의 부족1을 확인한다.

cook은 고정 기존 Program의 Scene/입력 소비 어댑터를 재사용하고 Main을 분리stack 1+1의 정확한 두 source 실행과 분리stack 2+2+2+2의 전체가능4/고정묶음2 비교로 한정했다. 전체 생산 AICook/CookSkill을 컴파일한다.

실행:
- dotnet run --project craft/Review.csproj -c Release --nologo
- dotnet run --project cook/Review.csproj -c Release --nologo

각 디렉터리의 run.log는 실제 실행 출력, exit.txt는 종료코드다. craft 2/0, cook 4/0, 둘 다 exit0. 이 표본은 모델/API·실제 게임 소비자 또는 최종 Unity 검증이 아니다.