exec(open(__file__.replace('tests.py','edit.py'),encoding='utf8').read().split("edit(S+'Core/AIGoalKinds.cs'")[0])
import json
p=root/'Tools/ai-bridge/test/fixtures/goal-contract.json'; j=json.loads(p.read_text(encoding='utf8'))
for c in j['cases']:
 if 'max' in c and c.get('goal',{}).get('do') in ['gather','craft','place']: c['max']=2147483647
p.write_text(json.dumps(j,ensure_ascii=False,indent=2),encoding='utf8')
edit('Tools/core-tests/NlChatTests.cs',lambda s:s.replace('x1.State == "밀려남" && b5.OpenAll().Count == AIHostOrders.MaxOpen && b5.Find(1) == null && changes.Contains("#1 미룸->밀려남")','x1.State == "미룸" && b5.OpenAll().Count == 4 && b5.Find(1) == x1 && changes.Count == 0').replace('a fourth open instruction pushes the oldest open one out (밀려남, logged)','a fourth open instruction preserves every earlier open request'))
def placetests(s):
 s=s.replace('!AIPlaces.Parse("rect:0,0,5,5", out _, out string e6) && e6.Contains("30칸까지")','AIPlaces.Parse("rect:0,0,5,5", out var r36, out string e6) && AIPlaces.ShapeTiles(r36).Count == 36').replace('36 tiles is over the cap','36 tiles remains a complete request')
 s=s.replace('!AIPlaces.Parse("rect:0,0,1000,1000", out _, out string e7) && e7.Contains("30칸까지") && e7.Contains("1002001칸")','AIPlaces.Parse("rect:0,0,1000,1000", out var big, out string e7) && AIPlaces.ShapeTiles(big).Count == 1002001 && AIPlaces.ShapeTiles(big)[1002000] == (1000,1000)').replace('a huge rect is refused without listing it, with its real tile count (검수 S2)','a million-tile request uses lazy coordinates without allocating every entry')
 s=s.replace('pick.Count == AIPlaces.PlacesMax','pick.Count == lines.Count').replace('lines at most','all supplied place lines retained')
 s=s.replace('pick.FindAll(x => x.StartsWith("chest")).Count == 5 && pick.FindAll(x => x.StartsWith("other")).Count == 2','pick.FindAll(x => x.StartsWith("chest")).Count == 8 && pick.FindAll(x => x.StartsWith("other")).Count == 4').replace('fixed first, mine 5, others 2','fixed first, all own and other stations retained')
 s=s.replace('pick.FindAll(x => x == "ore CopperOre").Count == 1 && more.ContainsKey("ore") && more.ContainsKey("chest") && more["chest"] == 3','pick.FindAll(x => x == "ore CopperOre").Count == 2 && more.Count == 0').replace('one ore per kind, the rest counted','all distinct ore locations retained, even for the same kind')
 s=s.replace('chars <= AIPlaces.PlacesChars && capped.Count == 4 && moreC["chest"] == 6','capped.Count == 10 && moreC.Count == 0').replace('places within','all complete places independent of old character cap')
 s=s.replace('AIPlaces.PlaceCount(w1, 31, out _) != null','AIPlaces.PlaceCount(w1, 200, out int n200) == null && n200 == 200').replace('over 30 refused, 0 is 1','200 accepted, omitted/zero retains the default1')
 return s
edit('Tools/core-tests/PlaceGoalTests.cs',placetests)
edit(S+'AILife.cs',lambda s:s.replace('var result = actual.FindAll(c => c.kind == "continue" || c.kind == "eat" || c.kind == "doorclose");','var result = new List<AICandidate>(actual);'))
edit(S+'AIBase.cs',lambda s:s.replace('if (h < 0 || h >= EatBelow) return res;','if (h < 0) return res;').replace('if (food.Count == 0 && h < TakeBelow)','if (food.Count == 0)'))
def cook(s):
 start=s.index('                if (true) // pot placement')
 end=s.index('\n            }',start)
 # Keep one normal placement entry; remove the now-unreachable base travel branch.
 first=s.index('                else if (AIBase.BaseStand',start)
 s=s[:start]+s[start:first].replace('                if (true) // pot placement has no implicit base-area permission\n','')+s[end:]
 return s
edit(S+'AICook.cs',cook)
def events(s):
 a=s.index('        // same kind merged by count (v2 §8)');b=s.index('        _events.Add(e);',a)
 return s[:a]+'        // Distinct observations remain intact; similar prefixes do not erase their details.\n'+s[b:]
edit(S+'AIPlanner.cs',events)
def brief(s):
 s=s.replace('보고의 plan_max 단계까지','필요한 단계 수만큼').replace('보고의 plan_max','계획 길이(고정 상한 없음)')
 s=s.replace('모두 몇 단계,','모두 몇 단계,').replace('plan_max: 한 계획에 줄 수 있는 단계 수','plan_max: 0은 계획 단계수 제한 없음(이전 설정의 호환 필드)')
 s=s.replace('gather(item·count 필요, 1..30)','gather(item·count 필요, 양의 정수)').replace('craft(item·count 필요, 1..10)','craft(item·count 필요, 양의 정수)')
 s=s.replace('한 번에 30칸까지','전체 요청 범위는 고정 칸수 제한 없음; 실행은 연속 묶음으로 처리').replace('1..9999','양의 정수').replace('모두 합쳐 3개까지이고, 넘치면 가장 오래된 것이 밀려남으로 빠진다','수에 따라 취소되지 않고 모두 보존된다')
 s=s.replace('기지 = 그 둘레 10칸','기지는 귀환 기준점이며 고정 반경으로 행동을 제한하지 않는다')
 s=s.replace('곡괭이·근접무기','실제로 보유한 손에 들 수 있는 물품')
 s+='\n작업량·계획 길이·목록 개수는 임의 상한으로 자르지 않는다. 지식 공개는 본 대상·보유품·본 제작대의 레시피와 재료에 따른다. 알려진 대상이라고 지금 출처·도구·실행 조건까지 충족한 것은 아니다. 가방 공간, 실제 정상 요청 한 묶음 크기와 전체 목표량은 구별한다.\n'
 return s
edit('Tools/ai-bridge/brief.md',brief)
edit(S+'AIActions.cs',lambda s:s.replace('new AttackSkill(c.entity) { NoDoorWait = c.noDoorWait }','new AttackSkill(c.entity) { NoDoorWait = c.noDoorWait, SecondaryUse = c.actionMode == "secondary" }'))
