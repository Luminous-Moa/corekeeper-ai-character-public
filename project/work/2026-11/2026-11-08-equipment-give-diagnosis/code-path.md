# 장비 전달 정체의 코드 경로

2026-11-08. 공식 소스 HEAD43c3817을 읽었다. 제품 소스·설정·게임 입력은 변경하지 않았다. 현재 사용자 실행은 aiui1106이며 이전 ai2 최적화 시험과 구분한다.

## 확인된 실행 흐름

- t62.7 요청은 `@Gemini 나 나무장비 풀세트 만들어줘`다. t80.4 상위 계획은 투구→갑옷→바지 건네기였고 당시 보고에 각 장비2개가 있었다.
- t82.6 하위 #2가 `finish_goal`을 선택했다. 투구 give스킬 실행 기록이 없는 상태에서 계획이 WoodBreastArmor 단계로 넘어갔다.
- 그 뒤 가방 메뉴 열기·분류·전체 취소가 반복됐고, t143~144경 슬롯12의 WoodBreastArmor give가 실제로 시작됐다.
- t150.2 관측은 출발 감소1/1, 바닥Entity.Null, 실제 바닥 감소-1, 수령자가방증가0, pickup관측False다.
- t153.5 사용자는 `오 방어구는 들어왔다. 나머지는?`라고 말했다. 이후 AI 저장에서 슬롯12가 사라졌고 갑옷 수량은2→1이었다.
- give스킬 DONE은 없었다. t283.2에는139초째 같은 가방 조작을 계속하는 선택이 적용됐다. 상위 목표는 이미상자보관으로바뀐상태였다.
- t402.7 보고에서도 같은 미확인 관측이 유지됐고, 최신 자료에서 give START1/END0이 지속됐다. 착용 상태는없음으로보고됐다.

실행 원문은 [최신 Player 로그](evidence/20261108-105738-Player.log), 요약 관측은 [report1~8 facts](evidence/20261108-105738-report1-report8-facts.json)에 있다. 실행·설치 동일성은 [runtime 보고서](runtime.md)에 정리돼 있다.

## 갑옷은 전달 이후 완료 확인에서 정체

[AIInventory.DropTick](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIInventory.cs:990)은 다음 순서로 동작한다.

1. 수령자에게접근하고수령자보유량을기록한다.
2. 자기위치에정상DropItem요청을한번보낸다. 출발슬롯감소를별도로확인한다.
3. 요청전없던바닥entity를찾아 `_floor`에묶는다. 동일물품·수량·비중첩물품상태값이맞아야한다.
4. `_floor != Entity.Null && removed == count` 안에서만 지정pickup관측·바닥감소·수령자보유량증가를검사한다.
5. `ObservedDelivery`가긍정일때만Completed/SkillStatus.Done을반환한다.

이번에는출발감소가확인됐지만 `_floor`가끝내Null이다. 그경우수령자가방증가를계산하는줄까지도달하지않는다. 매Tick의 `gained=0` 초기값이로그에남으므로,그0은실제로수령자증가를측정한결과로읽으면안된다. 사용자수령확인과출발슬롯감소가이점과일치한다.

바닥생성직후빠른자동줍기로임시entity를관측하지못했을가능성이있다. 바닥entity의실제생애기록이없어서이세부기전은추정이다. 생성물이조건매칭에서탈락했는지등은이번로그만으로확정하지못한다.

`ObservedDelivery`는귀속된바닥entity·지정수령pickup요청·바닥감소·수령증가를모두요구한다. [수령 판정](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/Core/AIInventoryRules.cs:54)

6초이후에는관측문장을한번추가하고Idle을반환한다. `_floor`를회복할대체증거나종료분기가없으면이상태는계속된다. `ObserveOnce`는상태가같으면다시관측변화를일으키지않는다. 하위모델이continue를고르면해당실행은끝나지않는다. [대기 처리](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIInventory.cs:1052)

## 투구 단계는 수령 관측 없이 완료 선택됨

`FinishChosen`은CompletionObserved가없어도`completed`로단계를끝내고상위계획을진행한다. 이번 #2가이경로다. 이는실물투구전달확인과구분해야한다. [완료 선택](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIGoals.cs:368)

기존 [공통 실행 합의](../2026-11-07-공통실행-합의.md)와 [연결 계약](../2026-11-07-공통실행자료/contract.md)는 `finish_goal`을AI의명시적단계완료로정하고,코드는수량·도착·실행관측을제공하도록한다. 따라서수령확인없이도이선택을허용하는구조자체를버그라고단정하지않는다. 이번투구에서는모델이전달실행전에단계완료를골랐다는행동판단문제가확인된다.

코드상BuildInput에는건네기목표와자기가방물품이들어간다. Give의확인수량0/1은Gather/Craft처럼목표옆에항상표시되지않는다. AIInventory.PromptLine도S.result가생기기전에는최근수령확인문장이없다. #2 SEND시점의실제전체input원문과모델내부이유는남아있지않아,이정보부족이완료선택의직접원인이었다고확정할수없다. 후보에는현재단계완료·가방진입등이있었고모델이완료를선택했다는것까지확인된다.

`got -1/1`의-1은give실측수량이계산되지않았다는기본값이다. `AIExperience.GoalEnding`은Gather/Craft에만got을채운다. -1자체로물품감소나전달실패를판정할수없다. 이번완료판정의근거는finish_goal선택과give실행부재다. [경험 기록](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIExperience.cs:209)

## 새 상자 목표 뒤에도 이전 실행이 남는 이유

`SetGoal`은목표·대기중선택을바꾸지만현재몸스킬을자동중단하지않는다. 이는공통실행계약에서선택한동작유지와명시적stop/새행동전환을분리한동작이다. 상자목표가생긴뒤하위 #38은139초된give의continue를선택했다. 따라서새목표설정만으로미완료give가해제되지않았다. [목표 변경](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIGoals.cs:259)

가방트리는이번실행에서실제enter/category/item/give선택을거쳐give START에도달했다. 전달불능을최적화의원정트리접기만으로설명할근거는없다. 다만가방메뉴선택취소반복은전달시작을지연시킨별도관측이다.

## 수정 방향

- 전달요청의지속가능한귀속정보를게임의요청처리·수령관측과연결하여,임시바닥entity를한프레임놓쳐도완료를검증할수있게한다. 단순히출발감소만으로수령성공을확정하면안된다.
- `_floor`를못찾은상태에서도수령자증가를실제로측정하고,미측정과0증가를로그·모델입력에서구별한다. 비중첩장비의상태값까지일치시키는MatchingCount조건도수령직후착용/상태변경시나리오에서검증해야한다.
- 귀속을복원할수없는관측은미확정으로명확히표시하고수령자측변화를계속측정해새사실을AI재판단에전달한다. 기존continue/stop/finish_goal결정권과재전송중복방지계약을유지하는방향이다.
- 모델의단계완료선택과검증된물품전달을상위계획·경험기록에서구분하고,Give의요청여부·확인수량·현재관측을하위입력에명확히제공한다. 완료선택권을코드로강제차단하는정책변경은이번분석에서제안하지않는다.

현재장비보유·수령자근접·give실행시작이확인되므로물품제작불가나장비미보유만으로이번정체를설명할수없다. 분석범위에서갑옷전달후완료확인정체와투구완료선택을확인했으며구현은수행하지않았다.
