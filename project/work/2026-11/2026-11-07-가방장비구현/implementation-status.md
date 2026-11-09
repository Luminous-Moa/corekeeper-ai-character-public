# 가방·장비 구현 검증 범위

현재 격리 코드 체크포인트는 `39b22df96dffe869ab0ccd474b18d31462b71d70`다. 역사적 flat 단독 기능판 `309522644543f196348e642b0e8c352acc4ba918`와 공통 실행 `685dde4d27dd91a782a14d8ae33fb576160fa578`를 구별한다.

최신 공식c4 기획을 보존하고 생활c4+가방741을 합친 최종39 소스를 고정했다. 최신 compile0/0·Loop55·생활96·생산자 연결192/192(로컬요청32/nav30)와 독립 최신199/199·compile0/0·생활96, 자체Unity exit0·sourceSDK103를 각각 구별한다. broad7dc Core1298/protocol154/menu348/cook51/craft19/inventory91은 해당 입력 불변 대조와 함께 보존한다. SDK는103 .cs+manifest의 소스후보이며 producer/export raw103일치, review raw102+EOL-only1다. 소스/오프라인/build 한정 HEAD수용과 예상ff-only만 기록하며 공식graph 적용·설치·외부모델·게임consumer·실게임 승격은 하지 않는다.

이번 가방·장비 판은 미실게임이며 최종 수용이 아니다. 게임 인벤토리 소비자·실제 UI 입력·드롭/수령 타이밍을 실행한 근거는 없다. 최소 World 어댑터는 수동 상태 변화로 실제 제품 어댑터를 검사한 범위다. 컴파일/오프라인 통과를 실제 슬롯 교환·수령 성공이나 최종 수용으로 승격하지 않는다.

기존 UI11eb461·리팩토링902721e·화면76563bf 실게임 근거는 원래 커밋과 관측 범위로 보존하며 이번 코드의 실행 확인으로 읽지 않는다. flat 774/1fc 검수와 774 Unity도 새 트리 통과로 승계하지 않는다. 원래 부탁의 완료는 상위 order=done, 작업단계는 공통 finish_goal, 현재 몸 중지는 stop_action, 가방 메뉴 전체 취소는 선택 상태만 닫는 것으로 구별한다.

입력 원문/로그와 SHA-256는 implementation-evidence.json에 있다. evidenceAppliesToFinalRevision은 고정 소스판 대응 여부일 뿐 최종 수용 여부가 아니다.

- agreement.md — 합의·최종 통합 설명·소비자 범위·실행 준비 원문; code=null; pinnedRevision=false; sha256=0ce0661b37dc40b2fb26a3a5623c5964ba8dca60adad82a3eac5a63215d37886
- tree-agreement.md — 합의·최종 통합 설명·소비자 범위·실행 준비 원문; code=null; pinnedRevision=false; sha256=6cd580bcfe94c7bc56b429721afddfee8cde92372a0fc38f2069667592ecf811
- tree-spec.md — 합의·최종 통합 설명·소비자 범위·실행 준비 원문; code=null; pinnedRevision=false; sha256=dce9decb47ed57c9c6e7278a86302b90456020d0bafdce627f238bda960b200e
- integration-agreement.md — 합의·최종 통합 설명·소비자 범위·실행 준비 원문; code=null; pinnedRevision=false; sha256=b64d77c99285eb5d87e184fb0e02f5431e1e806e1ffac02a08b04009d4e6a4bd
- integration-report.md — 합의·최종 통합 설명·소비자 범위·실행 준비 원문; code=null; pinnedRevision=false; sha256=1935a557cfd0160315190fb4f11c05e4348708133a00d6eeb0f7dcd05abf19eb
- integration-runtime-plan.md — 합의·최종 통합 설명·소비자 범위·실행 준비 원문; code=null; pinnedRevision=false; sha256=19b4a168213d697dfc01ade17ae2eb7024bb4edcbf273ba79f2fd8a99df3d275
- review-integrated.md — 최종 통합 독립 검수;graph 한정검수 마지막 부록 별도; code=null; pinnedRevision=false; sha256=bda60df20b996d7d9ae8667a4e8266edbdd6faefd348e06da414fc7c8b840fe2
- source-acceptance.md — HEAD 소스/오프라인/build 한정 수용과예상ff-only승인;실게임/공식graph적용승인아님; code=null; pinnedRevision=false; sha256=fd6effcd567daacf3e749b1c258b3427e3818e0ed2442975012cfb2b06202d8f
- source-integration.md — 합의·최종 통합 설명·소비자 범위·실행 준비 원문; code=null; pinnedRevision=false; sha256=9818582b94906d969f36e0b32a1227a2b734f59d903e1ebbea648d7bc0e8b8f5
- inventory-category-cap-audit.md — 합의·최종 통합 설명·소비자 범위·실행 준비 원문; code=null; pinnedRevision=false; sha256=28aacd933901811340ecd3382fdd0382e82367a4ac6b46dbc29e88769d3f23a0
- cap-removal-spec.md — 합의·최종 통합 설명·소비자 범위·실행 준비 원문; code=null; pinnedRevision=false; sha256=4325181e4c12e68868885119715fb2e7d806674acaf7ec5b167e6537246ec1b0
- tree-cap-result.md — 합의·최종 통합 설명·소비자 범위·실행 준비 원문; code=null; pinnedRevision=false; sha256=e6beff71f9fc00e558d885a0e4dfef2cb961461328a5c383f670e17da21a9a10
- integration/life-regression.md — 합의·최종 통합 설명·소비자 범위·실행 준비 원문; code=null; pinnedRevision=false; sha256=f5ad00a1a65f79c93fbfd7c38125531e347d5ded1555474427b961a9ce1e3422
- integration/compile-final.log — 실제 생산자 검사 원로그;latest와 불변 broad checkpoint를 구별; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=bc60647accea71927390139691f40621295bc010cbea0a27f2223ea228a3446f
- integration/execution-final.log — 실제 생산자 검사 원로그;latest와 불변 broad checkpoint를 구별; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=64be43f87bd8eac28eb3c8b999ae6b5026055d50c61105aef803c0bb378c9a23
- integration/life-main-final.log — 실제 생산자 검사 원로그;latest와 불변 broad checkpoint를 구별; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=533679f09a7aee903f91908f28b5ded9d7f49830c12ea4e1be8c2adddf50fab5
- integration/connected-final.log — 실제 생산자 검사 원로그;latest와 불변 broad checkpoint를 구별; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=16a90fe5663955c77b669c3679761f746f07e552479a40906d5a1573174a2b1c
- integration/core.log — 실제 생산자 검사 원로그;latest와 불변 broad checkpoint를 구별; code=7dc9c7e781f1b50a95cce1c89acaed260334d934; pinnedRevision=false; sha256=52f7ccb11e49cd2a869c3cfc6a997533032b10ef84d91c4e46aa94ae08421f29
- integration/protocol.log — 실제 생산자 검사 원로그;latest와 불변 broad checkpoint를 구별; code=7dc9c7e781f1b50a95cce1c89acaed260334d934; pinnedRevision=false; sha256=599115931db1da401709401e65fd27a05319e72b1c3b94a7dc0834930a269467
- integration/menu.log — 실제 생산자 검사 원로그;latest와 불변 broad checkpoint를 구별; code=7dc9c7e781f1b50a95cce1c89acaed260334d934; pinnedRevision=false; sha256=6b76d3cc280ada1a9aad3afb249e827171722e1e283f079d876117d9f1f4c184
- integration/life-cook.log — 실제 생산자 검사 원로그;latest와 불변 broad checkpoint를 구별; code=7dc9c7e781f1b50a95cce1c89acaed260334d934; pinnedRevision=false; sha256=dc5474d4b41a3fae119d9beaa079646ad418bc1864c0d8d0138a582905b196e1
- integration/life-craft.log — 실제 생산자 검사 원로그;latest와 불변 broad checkpoint를 구별; code=7dc9c7e781f1b50a95cce1c89acaed260334d934; pinnedRevision=false; sha256=1b15ba1666aeb7f0ee6139d9895360cbd976f4cf1ad82b5270ad648b76059416
- integration/life-inventory.log — 실제 생산자 검사 원로그;latest와 불변 broad checkpoint를 구별; code=7dc9c7e781f1b50a95cce1c89acaed260334d934; pinnedRevision=false; sha256=68f4b6577bf5600dd1fd733bac3bef1ebf10160722c2f726fa74ff5ad0b47ba5
- integration/validation-continuity.json — 7dc broad 입력과 최종39의 실제 불변 범위; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=fbb26845d1968e9b3f87c994b4658340b89d599e3d2802dcdfcbb886377a572c
- tree-menu-adapter/payloads.json — 가방 현재 노드 단독 표본;실제 통합 전송/외부모델과 구별; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=ec3c9f0a221b36451cf93e2294ab2fcd924930eb9a56d37ec605d4b5cd6d7836
- integration/source-integration-before.json — HEAD승인main ff-only의원시해시/줄끝·환경/설치불변기록;graph적용/실게임아님; code=c4a0f5e38016c79567f880bbaea5e56e0eeae347; pinnedRevision=false; sha256=e46b7585ebb7d24931a8063d3c2b2e4c0555ad0c10d03ba340cb61350d1cac34
- integration/source-integration-after.json — HEAD승인main ff-only의원시해시/줄끝·환경/설치불변기록;graph적용/실게임아님; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=aa4607a6f69b3bc50d012101d3be5c725c60f9f0514badce70f1c4eedf3ef285
- integration/tree-connected/run.ps1 — 최종39의 실제 생활+가방 source link/추출 연결 fixture;대역World/소비상태; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=320b97834fd72743267053d6cb3ea514afdd96c454edffef5319df6734716791
- integration/tree-connected/Connected.csproj — 최종39의 실제 생활+가방 source link/추출 연결 fixture;대역World/소비상태; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=82ba57b7ad9fc653e21f63dfb742f78734535cfb7c1d561392a6a8f6379d1772
- integration/tree-connected/Program.cs — 최종39의 실제 생활+가방 source link/추출 연결 fixture;대역World/소비상태; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=5db25b02c937dc8adfa13a7a22f4d97d68a630e4dc666b1ed0e2d16bdf56734d
- integration/tree-connected/Stubs.cs — 최종39의 실제 생활+가방 source link/추출 연결 fixture;대역World/소비상태; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=c63733efa552ac4e74184d74a5491112f177c84ed077e71ecf7b9d1cff224ca3
- integration/tree-connected/Boundary.cs — 최종39의 실제 생활+가방 source link/추출 연결 fixture;대역World/소비상태; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=d562461b4f1cf2db5e7c572596c939a700e93123048028e82b2f6513c7e3ffa0
- integration/tree-connected/LifeBoundary.cs — 최종39의 실제 생활+가방 source link/추출 연결 fixture;대역World/소비상태; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=d163469a617eaccb97999541a907033590dc2abac0bd522a229739602d21f9e7
- integration/tree-connected/FeatureEntry.cs — 최종39의 실제 생활+가방 source link/추출 연결 fixture;대역World/소비상태; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=fd1687f9d2547a8e2681acc4ff60842f327485d00e4a33adeb18c1db29b8988b
- integration/tree-connected/Production.cs — 최종39의 실제 생활+가방 source link/추출 연결 fixture;대역World/소비상태; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=d6f319a5c460305229cbe357e9eaa7d626aca8369a128d4137061a6c9e301a21
- integration/tree-connected/source-pin.json — 최종39의 실제 생활+가방 source link/추출 연결 fixture;대역World/소비상태; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=45c59037b58dffa9621de8ef5bc4aefd120b3114c3ef1c42ea784174d622cabb
- integration/tree-connected/connected-trace.json — 최종39의 실제 생활+가방 source link/추출 연결 fixture;대역World/소비상태; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=d2d575739615df118e7f2af437fbc4dc5f233307a80501cd710d049f73aae9e8
- integration/final-39b22df/unity-input.json — 39 자체 Unity compile/설치없는SDK source 후보;바이너리릴리스/실게임 아님; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=24d1961cbf3c374272061a0b5bc06822429266c6688da27eb366a5293188122a
- integration/final-39b22df/unity-settings.json — 39 자체 Unity compile/설치없는SDK source 후보;바이너리릴리스/실게임 아님; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=41b9ff1c5c078a9bc04d0d8f1201cd5bf0eff66bc09c5654eed1977fa823478c
- integration/final-39b22df/settings-after.json — 39 자체 Unity compile/설치없는SDK source 후보;바이너리릴리스/실게임 아님; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=3bface526b2bc581e7c99fb4e52c858c6d1e15891493e4cec65d968edd16b1c6
- integration/final-39b22df/unity-exit.json — 39 자체 Unity compile/설치없는SDK source 후보;바이너리릴리스/실게임 아님; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=9ffd5becf4a65a2cf843d87a03affc213412627486b9eb0d714d4f2904be15ca
- integration/final-39b22df/artifact-manifest.json — 39 자체 Unity compile/설치없는SDK source 후보;바이너리릴리스/실게임 아님; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=d8090e5df0474c3430b78ef09538875cbe9c0ac0e7d32bd03c41d593b549cc16
- integration/final-39b22df/package.json — 39 자체 Unity compile/설치없는SDK source 후보;바이너리릴리스/실게임 아님; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=e5c297238aaba6c2bbf68767d167456589baf664c388489d2c0f736f4c299607
- integration/final-39b22df/package-verification.json — 39 자체 Unity compile/설치없는SDK source 후보;바이너리릴리스/실게임 아님; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=921493eeea0a324d1e6e4db9b3efd8a8d91ae3ff2cf36e515882424395f393a6
- integration/final-39b22df/bridge-candidate.json — 39 자체 Unity compile/설치없는SDK source 후보;바이너리릴리스/실게임 아님; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=2b126d422a0805bce449a82e3be38eaad4ac4bb1cdc740d2d2599602589ea1e2
- integration/final-39b22df/unity-export.log — 39 자체 Unity compile/설치없는SDK source 후보;바이너리릴리스/실게임 아님; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=c766df8fce44488c317e3395b131c06c070da90f7cce905dbf35de09a868517d
- integration/final-39b22df/BagCandidateExport.cs — 39 자체 Unity compile/설치없는SDK source 후보;바이너리릴리스/실게임 아님; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=2d0485fcee977b9db749269f01115690628fbc41e3ef7b3dc2fc79b6b6bc1a22
- independent-integrated/compile-final39.log — 독립 검수 원로그/입력대응;producer192와 independent199를 합산하지 않음; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=d4bf77bbc0604a9555c0403bcc1c5797e2c1797175ed727d7c7340df518f1f49
- independent-integrated/life-final39.log — 독립 검수 원로그/입력대응;producer192와 independent199를 합산하지 않음; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=fb6f67f433fdb8a9db074fd4ae16d45c147bd51b50246f0c161eb60e2d61f57c
- independent-integrated/connected-final39.log — 독립 검수 원로그/입력대응;producer192와 independent199를 합산하지 않음; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=8642994448150a1d527be8221a984a32797f1edc0fac91fff87f14e8dce90f22
- independent-integrated/39b22df-source-pin.json — 독립 검수 원로그/입력대응;producer192와 independent199를 합산하지 않음; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=524ec6549a7707a0b261f0ee540a52ab0720a764f2df6d261e2601fe1110ae6e
- independent-integrated/export-final39-verification.json — 독립 검수 원로그/입력대응;producer192와 independent199를 합산하지 않음; code=39b22df96dffe869ab0ccd474b18d31462b71d70; pinnedRevision=true; sha256=d84d9362be3e662e13b567993d6000cc7935fd4df43074c0f4b64c0083f5e3f4
- independent-integrated/core.log — 독립 검수 원로그/입력대응;producer192와 independent199를 합산하지 않음; code=7dc9c7e781f1b50a95cce1c89acaed260334d934; pinnedRevision=false; sha256=90f1b764cddedc4d25ce4b4deaea99084ec3cda3524cea5ee1234eb657254012
- independent-integrated/protocol.log — 독립 검수 원로그/입력대응;producer192와 independent199를 합산하지 않음; code=7dc9c7e781f1b50a95cce1c89acaed260334d934; pinnedRevision=false; sha256=599115931db1da401709401e65fd27a05319e72b1c3b94a7dc0834930a269467
- independent-integrated/menu.log — 독립 검수 원로그/입력대응;producer192와 independent199를 합산하지 않음; code=7dc9c7e781f1b50a95cce1c89acaed260334d934; pinnedRevision=false; sha256=d8056a5951735b7c7eee739908a662d67a811c4862163a0b9e524ef5bb00e2b3
- independent-integrated/cook.log — 독립 검수 원로그/입력대응;producer192와 independent199를 합산하지 않음; code=7dc9c7e781f1b50a95cce1c89acaed260334d934; pinnedRevision=false; sha256=f23ae181212dbeb90ef48da961acdc5f0a9de2aab3d053ef855b1f5196b8211b
- independent-integrated/craft.log — 독립 검수 원로그/입력대응;producer192와 independent199를 합산하지 않음; code=7dc9c7e781f1b50a95cce1c89acaed260334d934; pinnedRevision=false; sha256=2b5a87636389a919614955d5a6bee10114975adb62234be0d784b82014de869f
- independent-integrated/inventory.log — 독립 검수 원로그/입력대응;producer192와 independent199를 합산하지 않음; code=7dc9c7e781f1b50a95cce1c89acaed260334d934; pinnedRevision=false; sha256=1e445a8584b3246acbe233dfdd2b7bf893560f9ed1a98662992a6fc4b8d06b7d
