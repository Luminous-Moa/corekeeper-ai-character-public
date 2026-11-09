# R3 제작 경계 재현

기준: 24c61c246053237784c057cf97282b9ca0c5d564. 생산 스냅샷 다섯 파일은 git show 원문이다. 원본 Git blob ID는 fixed-git-blobs.txt에 기록했다.

Program.cs는 기존 고정 Tools/life-craft-tests/Program.cs의 Setup을 사용하고 Main을 두 경계에 한정했다. 첫 표본은 Wood2/요청 Torch3/한 회 기본출력2에서 생산 ExactCandidate와 옛 Materials/HasMaterials를 비교한다. WantedItems 전체는 실행하지 않았으며 실제 함수 결과와 고정 AIBrain.Prep.cs:76,143의 호출 순서를 정적으로 대조한다.

Stubs.cs 변경은 경계 데이터 공급 두 곳뿐이다: PugDatabase.GetObjectInfo가 Fake DB의 requiredObjectsToCraft를 노출하고 AIBody.InventoryCounts가 표본 main 가방을 읽는다. 실제 비용/후보 정책은 생산 소스를 사용한다. station 이동 표본은 실제 게임의 동일 entity 이동 가능성을 확인하지 못해 중요 finding에서 제외했다.

실행: dotnet run --project Review.csproj -c Release --nologo
실제 표준출력/오류는 run.log, exit code는 exit.txt에 있다. 모델/API/실제 게임 소비자는 실행하지 않았다.