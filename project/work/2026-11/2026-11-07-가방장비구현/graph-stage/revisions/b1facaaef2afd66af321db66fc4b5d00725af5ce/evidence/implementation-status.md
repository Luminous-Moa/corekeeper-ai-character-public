# 가방·장비 구현 검증 범위

현재 격리 코드 체크포인트는 `b1facaaef2afd66af321db66fc4b5d00725af5ce`다. 역사적 flat 단독 기능판 `309522644543f196348e642b0e8c352acc4ba918`와 공통 실행 `685dde4d27dd91a782a14d8ae33fb576160fa578`를 구별한다.

분류 기반 트리 체크포인트 b1facaaef2afd66af321db66fc4b5d00725af5ce의 생산자 컴파일0경고·0오류/Core1204/protocol118/실제Loop소스 링크55/단독 메뉴어댑터326 통과 로그와 현재 노드 메뉴 샘플을 고정했다. 연결 trace와 최종 독립검수는 진행 중이며 트리 Unity·설치·실게임·외부 모델 호출·최종 수용은 없다. flat 774/1fc 검수 및 774 Unity는 역사적 근거로만 보존한다.

이번 가방·장비 판은 미실게임이며 최종 수용이 아니다. 게임 인벤토리 소비자·실제 UI 입력·드롭/수령 타이밍을 실행한 근거는 없다. 최소 World 어댑터는 수동 상태 변화로 실제 제품 어댑터를 검사한 범위다. 컴파일/오프라인 통과를 실제 슬롯 교환·수령 성공이나 최종 수용으로 승격하지 않는다.

기존 UI11eb461·리팩토링902721e·화면76563bf 실게임 근거는 원래 커밋과 관측 범위로 보존하며 이번 코드의 실행 확인으로 읽지 않는다. flat 774/1fc 검수와 774 Unity도 새 트리 통과로 승계하지 않는다. 원래 부탁의 완료는 상위 order=done, 작업단계는 공통 finish_goal, 현재 몸 중지는 stop_action, 가방 메뉴 전체 취소는 선택 상태만 닫는 것으로 구별한다.

입력 원문/로그와 SHA-256는 implementation-evidence.json에 있다. evidenceAppliesToFinalRevision은 고정 소스판 대응 여부일 뿐 최종 수용 여부가 아니다.

- agreement.md — 사용자 합의 원문; code=null; pinnedRevision=false; sha256=0ce0661b37dc40b2fb26a3a5623c5964ba8dca60adad82a3eac5a63215d37886
- tree-agreement.md — 사용자 합의 원문; code=null; pinnedRevision=false; sha256=6cd580bcfe94c7bc56b429721afddfee8cde92372a0fc38f2069667592ecf811
- tree-spec.md — 명세·정적 조사/구현 범위 기록; code=null; pinnedRevision=false; sha256=dce9decb47ed57c9c6e7278a86302b90456020d0bafdce627f238bda960b200e
- inventory-category-cap-audit.md — 명세·정적 조사/구현 범위 기록; code=null; pinnedRevision=false; sha256=28aacd933901811340ecd3382fdd0382e82367a4ac6b46dbc29e88769d3f23a0
- cap-removal-spec.md — 명세·정적 조사/구현 범위 기록; code=null; pinnedRevision=false; sha256=4325181e4c12e68868885119715fb2e7d806674acaf7ec5b167e6537246ec1b0
- tree-cap-result.md — 명세·정적 조사/구현 범위 기록; code=null; pinnedRevision=false; sha256=e6beff71f9fc00e558d885a0e4dfef2cb961461328a5c383f670e17da21a9a10
- tree-design-review.md — 설계 독립검토;구현 수용 아님; code=null; pinnedRevision=false; sha256=1086ec282712a2810efc208d0637590fed272cb1a1498148b258993eb17bd46f
- tree-checkpoint-compile.log — 생산자 체크포인트 오프라인 검사; code=b1facaaef2afd66af321db66fc4b5d00725af5ce; pinnedRevision=true; sha256=1f509bff2519018b963f90f07c94621e6ba88810bc3e13cbb63f1fd51cc36d76
- tree-checkpoint-core.log — 생산자 체크포인트 오프라인 검사; code=b1facaaef2afd66af321db66fc4b5d00725af5ce; pinnedRevision=true; sha256=7398eb389f2f2f37b337310ad4c43c8ee11d2a71dbdeb6e0bd92b4895465415d
- tree-checkpoint-protocol.log — 생산자 체크포인트 오프라인 검사; code=b1facaaef2afd66af321db66fc4b5d00725af5ce; pinnedRevision=true; sha256=66908eb26e9acd3e75f4fe14135030f36020baf65f7e3eb1c42d9fa2f944a1ed
- tree-loop-execution.log — 실제 Loop/공통 입구 소스 링크 검사;World/모델 대역; code=b1facaaef2afd66af321db66fc4b5d00725af5ce; pinnedRevision=true; sha256=d721349dc5dc4a3ddf450e10244a3d556614d09d193b6ff3e5c41347077a17af
- tree-menu-adapter/results.log — 메뉴·Inventory 소스 링크 단독 어댑터;게임 소비자 미실행; code=b1facaaef2afd66af321db66fc4b5d00725af5ce; pinnedRevision=true; sha256=be3729f5c380e0aa1e61785c8dbe4dd0742742295d96b668137c8d0c78fa68d8
- tree-menu-adapter/payloads.json — 현재 노드 메뉴 샘플;단독 어댑터 구성;실제 모델 호출 아님; code=b1facaaef2afd66af321db66fc4b5d00725af5ce; pinnedRevision=true; sha256=ec3c9f0a221b36451cf93e2294ab2fcd924930eb9a56d37ec605d4b5cd6d7836
- tree-menu-adapter/Program.cs — 단독 메뉴 소스 링크 재현 fixture;제품 git 소스와 구별; code=b1facaaef2afd66af321db66fc4b5d00725af5ce; pinnedRevision=true; sha256=d6d555ca3ddf721bebc0af12ae76be523d9074a60d755040ae2d70bb45095039
- tree-menu-adapter/Stubs.cs — 단독 메뉴 소스 링크 재현 fixture;제품 git 소스와 구별; code=b1facaaef2afd66af321db66fc4b5d00725af5ce; pinnedRevision=true; sha256=2f6a29203ec4dc60b08f6100eca780eb7f79298adccaf7f5b65f9463eb96b2a6
- tree-menu-adapter/TreeMenu.csproj — 단독 메뉴 소스 링크 재현 fixture;제품 git 소스와 구별; code=b1facaaef2afd66af321db66fc4b5d00725af5ce; pinnedRevision=true; sha256=022d3aafbb9feeb36b2e45c869ce271725c20e1dc6fb34e22c7ea01a0cdbda91
