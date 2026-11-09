from pathlib import Path
p=Path('C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/2026-11/2026-11-08-equipment-give-diagnosis/code-path.md')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('[report1~8 facts](report1-report8-facts.json)', '[report1~8 facts](evidence/20261108-105738-report1-report8-facts.json)')
s=s.replace('실제 파일 배치는 runtime 담당의 최종 보고서를 기준으로 확인한다.', '실행·설치 동일성은 [runtime 보고서](runtime.md)에 정리돼 있다.')
needle='`got -1/1`의-1은give실측수량이계산되지않았다는기본값이다.'
pos=s.index(needle)
s=s[:pos]+'''기존 [공통 실행 합의](../2026-11-07-공통실행-합의.md)와 [연결 계약](../2026-11-07-공통실행자료/contract.md)는 `finish_goal`을AI의명시적단계완료로정하고,코드는수량·도착·실행관측을제공하도록한다. 따라서수령확인없이도이선택을허용하는구조자체를버그라고단정하지않는다. 이번투구에서는모델이전달실행전에단계완료를골랐다는행동판단문제가확인된다.

코드상BuildInput에는건네기목표와자기가방물품이들어간다. Give의확인수량0/1은Gather/Craft처럼목표옆에항상표시되지않는다. AIInventory.PromptLine도S.result가생기기전에는최근수령확인문장이없다. #2 SEND시점의실제전체input원문과모델내부이유는남아있지않아,이정보부족이완료선택의직접원인이었다고확정할수없다. 후보에는현재단계완료·가방진입등이있었고모델이완료를선택했다는것까지확인된다.

'''+s[pos:]
s=s.replace('- 귀속을복원할수없는대기는무기한확인중으로남지않게명시적미확정결과와재판단을제공한다. 재전송중복을방지하면서기존continue/stop계약과연결해야한다.', '- 귀속을복원할수없는관측은미확정으로명확히표시하고수령자측변화를계속측정해새사실을AI재판단에전달한다. 기존continue/stop/finish_goal결정권과재전송중복방지계약을유지하는방향이다.')
s=s.replace('- 모델의단계완료선택과검증된물품전달을상위계획·경험기록에서구분한다. 실제수령근거가없는완료가다음장비전달로자동진행하는조건을검토한다.', '- 모델의단계완료선택과검증된물품전달을상위계획·경험기록에서구분하고,Give의요청여부·확인수량·현재관측을하위입력에명확히제공한다. 완료선택권을코드로강제차단하는정책변경은이번분석에서제안하지않는다.')
p.write_text(s,encoding='utf-8')
