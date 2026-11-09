# 큰 제작 목표의 실제 실행 묶음 연결

소유 변경은 AICraft.cs와 Tools/life-craft-tests다. 상위 목표의 count를 한 번의 커서 출력량으로 취급하던 ReadRecipe 경로를 수정했다. 큰 목표량은 candidate.life.RequestedCount에 보존하고, recipe.ExpectedOutput/Calls/CraftAmount는 정상 게임 요청 한 번의 커서 용량에 맞춘다. 결과 수령 뒤 현재 main 가방 증가분을 빼서 다음 후보가 남은 총량을 제공한다. 따라서 CraftSkill 한 번이 Done이어도 큰 Craft goal 전체를 완료 처리하지 않는다.

ceil 나눗셈과 요청량·비용 계산은 long 중간값을 사용한다. 한 레시피의 기본 출력은 checked 연산으로 검증한다. 최종 커서 출력9999 제한은 게임 원본 InventoryHandler.cs:282~284 및 Inventory/InventoryUtility.cs:1632,1649의 정상 슬롯 조건이며 전체 목표 수량 상한으로 쓰지 않는다. 제작 요청마다 커서는 비어 있어야 하고 재료/선택 슬롯/레시피의 변화를 재검증한다. mainCount와 legacy Materials 곱셈도 overflow로 음수 수량이 나오지 않도록 보완했다.

검증: [craft-tests.log](craft-tests.log) 29개 PASS, 0 FAIL. 생산 AICraft/ExactCandidate/CraftSkill을 직접 링크하고 ECS/DB/UI 큐 경계만 fake로 둔 기존 harness를 확장했다. 요청20000, 레시피당2개 출력에서9998+9998+4의 실제 수령을 순차 주입해 같은 목표객체/총량 유지와 잔량10002→4→0, 정확 UI요청, cursor잔류 중 중복요청·거짓완료 없음, 마지막 추가요청 없음까지 확인했다. int.Max 목표량의 ceil overflow 없이 안전한9998개 묶음이 생기는 것도 검증했다. 기존 소량 제작·일부 재료 부족·recipe stale·보너스·커서 variation 테스트19개도 유지됐다.

[craft-compile.log](craft-compile.log): 전체 compile-check 경고0·오류0. 실제 게임 실행/제작 소비자는 미관측이다. 테스트에서 수령/소비 변화는 명시적으로 주입했다. 새로운 KnowledgeBoundary는 제작 정책 외부인 관측 지식 경계만 제공하며 새 AIHandCapabilities는 생산파일을 직접 링크했다.
