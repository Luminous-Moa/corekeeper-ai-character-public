import json
import re
from pathlib import Path

HERE = Path(__file__).resolve().parent
A = HERE.parent
DOC = 'A2/screens.md'
nodes, edges, mappings, sections = [], [], [], []

def read(name):
    return (A / name).read_text(encoding='utf-8-sig')

def compact(value):
    return json.dumps(value, ensure_ascii=False, separators=(',', ':'))

def add_screen(id, name, obs, classification, url, state, raws, overview, flow, condition):
    title = f'{id} {name}'
    screen = dict(id=id, type='화면', summary=name, source=dict(document=DOC, section=title),
                  observationId=obs, classification=classification, url=url, state=state,
                  rawFiles=raws, policyIds=[], elements=[])
    nodes.append(screen)
    screen['_doc'] = [f'## {title}', '', f'- ID: `{id}`', f'- 이름: {name}',
        f'- 분류: `{classification}`', f'- 관측: `{obs}`', f'- URL: {url or "해당 없음 — 프로세스 실행 출력"}',
        f'- 상태: {state}', '- 원문 파일: ' + ', '.join(f'`{p}`' for p in raws),
        '- 그림/공통 영역: 없음. CLI/API 출력 원문과 아래 줄 위치·JSON pointer가 위치 매칭본이다.', '',
        f'### {id} 개요', '', overview, '', f'### {id} 화면 흐름', '', flow, '',
        f'### {id} 화면요소', '',
        f'공통 조작: 출력 읽기. 출력 자체의 버튼·입력·링크 조작은 관측되지 않았다. 공통 표시 조건: {condition}', '',
        '| 번호 / 요소 ID | 보이는 값·문구 | 원문 위치 | 조작 / 조건 |', '|---|---|---|---|']
    return screen

def elem(screen, number, label, value, raw, pointer=None, lines=None, parent=None):
    id = f'{screen["id"]}#{number}'
    location = dict(rawFile=raw)
    if pointer is not None:
        location['jsonPointer'] = pointer
        location['line'] = 1
    if lines is not None:
        location['lines'] = lines
    shown = compact(value) if pointer is not None else (value if isinstance(value, str) else compact(value))
    node = dict(id=id, type='화면요소', summary=label,
                source=dict(document=DOC, section=f'{screen["id"]} 화면요소'),
                number=number, visibleValue=value, location=location,
                interaction='출력 읽기; 직접 조작 관측 없음',
                observedCondition=f'{screen["observationId"]}의 기록된 실행·요청 조건에서 관측')
    nodes.append(node)
    screen['elements'].append(id)
    edges.append(dict(source=screen['id'], target=id, type='화면→화면요소'))
    if parent:
        edges.append(dict(source=f'{screen["id"]}#{parent}', target=id, type='화면요소→화면요소'))
    where = f'`{raw}` ' + (f'줄 1, `{pointer or "/ (root)"}`' if pointer is not None else f'줄 {lines[0]}–{lines[1]}')
    display = shown.replace('|', '&#124;').replace('\n', '<br>')
    screen['_doc'].append(f'| {number} / `{id}` ({label}) | <code>{display}</code> | {where} | 읽기 / 위 공통 조건 |')

def finish(screen):
    screen['_doc'] += ['', f'### {screen["id"]} 화면 정책', '',
        '정책 없음. 이 원문은 해당 실행에서 보인 값·문구의 증거이며, 다른 입력·환경에서도 적용되는 표시 규칙을 입증하지 않는다. 정책 노드와 화면→정책 연결은 생성하지 않는다.', '']
    sections.append('\n'.join(screen.pop('_doc')))

def mapping(obs, out, meta, body, headers=None, classification='product-http'):
    m = json.loads(read(meta))
    url = m.get('url')
    path = None
    if url:
        path = '/' + url.split('/', 3)[3] if len(url.split('/', 3)) > 3 else '/'
    row = dict(observationId=obs, outputId=out, method=m.get('method'), url=url,
               path=path, status=m.get('httpStatus'), rawBody=body, rawMeta=meta,
               classification=classification)
    if headers:
        row['rawHeaders'] = headers
    if m.get('requestBodyFile'):
        row['requestBody'] = 'requests/' + m['requestBodyFile'].replace('\\', '/').split('/')[-1]
    if 'exitCode' in m:
        row['clientExitCode'] = m['exitCode']
    if 'clientStatus' in m:
        row['clientStatus'] = m['clientStatus']
    mappings.append(row)

s = add_screen('BRG-01', '격리 브리지 시작: 그래프 경고와 listening', 'OBS-03', 'product-startup',
    'http://127.0.0.1:18331', '프로세스가 graph NOT FOUND 안내 후 listening 주소를 출력함. 전체 제품 준비 완료 의미는 아님.',
    ['raw/bridge-isolated.stdout.txt', 'raw/bridge-isolated.stderr.txt', 'raw/bridge-isolated.meta.json'],
    'A-1의 완전 패키지를 별도 포트·빈 데이터로 실행한 직후 받는 콘솔 출력이다.',
    'Node 실행 → stdout 첫 줄의 game graph 안내 읽기 → 둘째 줄의 listening 주소 읽기. A-1은 이후 별도 health 요청을 보냈다. 자동 화면 이동은 관측되지 않았다.',
    'A-1에 기록된 격리 실행 1회. stderr는 빈 파일이다.')
for i, line in enumerate(read('raw/bridge-isolated.stdout.txt').splitlines(), 1):
    elem(s, str(i), '게임 그래프 안내' if i == 1 else '대기 주소 안내', line, 'raw/bridge-isolated.stdout.txt', lines=[i, i])
finish(s)


http_states = [
 ('BRG-02', 'health: 격리 환경의 준비되지 않은 값', 'OBS-04', 'isolated-health',
  '격리 환경에서 HTTP 200, ok:false, busy:[]를 반환함.',
  '브리지 응답을 확인하려고 GET /v1/health를 요청한 결과다. A-1에 따르면 members 빈 설정으로 로그인·할당량 preflight가 비활성인 빈 환경이다. 이 값으로 운영 계정·사용자 설치의 장애나 인증 실패를 주장할 수 없다.',
  'GET 요청 → HTTP 200 헤더 확인 → ok·members·busy 읽기. HTTP 200은 응답 전송 성공이며 AI 사용 준비 완료를 뜻하지 않는다.'),
 ('BRG-03', '없는 경로: not found', 'OBS-05', 'isolated-not-found',
  'GET / 요청에 HTTP 404와 not found를 반환함.',
  'A-1이 루트 경로를 요청하여 받은 오류 출력이다. 다른 미관측 경로의 결과는 일반화하지 않는다.',
  'GET / 요청 → HTTP 404 확인 → ok:false와 error 문구 읽기. 응답 안의 후속 조작은 관측되지 않았다.'),
 ('BRG-04', '알 수 없는 멤버: unknown member', 'OBS-06', 'isolated-unknown-member',
  '기록된 unknown-member 입력에 HTTP 400을 반환함.',
  'A-1의 requests/unknown-member.json을 POST /v1/plan으로 보낸 결과다. 입력 원문 내용은 이 설계서에서 추측하지 않고 요청 메타의 파일 참조를 보존한다.',
  '기록된 POST 요청 → HTTP 400 확인 → error와 detail의 멤버 이름 읽기. 안내 이름을 정상 호출해 확인한 기록은 없다.'),
 ('BRG-05', 'JSON 구문 오류: bridge', 'OBS-07', 'isolated-malformed',
  '단일 { 입력에 HTTP 500과 JSON parse detail을 반환함.',
  'A-1이 requests/malformed.json의 단일 {를 POST /v1/plan으로 보낸 결과다.',
  '기록된 POST 요청 → HTTP 500 확인 → error와 detail의 위치 설명 읽기. 일반 JSON 오류 전부의 상태 코드를 확정하지 않는다.')
]
for id, name, obs, stem, state, overview, flow in http_states:
    body = f'raw/{stem}.body.txt'
    headers = f'raw/{stem}.headers.txt'
    meta = f'raw/{stem}.meta.json'
    m = json.loads(read(meta))
    s = add_screen(id, name, obs, 'product-http', m['url'], state, [body, headers, meta], overview, flow,
                   f'{obs}에 기록된 {m["method"]} {m["url"]} 1회 응답. 비스트리밍 JSON이며 스트림 이벤트 관측 없음.')
    header_lines = read(headers).splitlines()
    elem(s, '1', 'HTTP 응답 헤더', '\n'.join(header_lines[:6]), headers, lines=[1, 6])
    payload = json.loads(read(body))
    for n, (key, value) in enumerate(payload.items(), 2):
        num = str(n)
        elem(s, num, key, value, body, pointer=f'/{key}')
        if key == 'members':
            for j, (member, fields) in enumerate(value.items(), 1):
                member_num = f'{num}-{j}'
                elem(s, member_num, member, fields, body, pointer=f'/members/{member}', parent=num)
                for k, (field, field_value) in enumerate(fields.items(), 1):
                    elem(s, f'{member_num}-{k}', f'{member}.{field}', field_value, body,
                         pointer=f'/members/{member}/{field}', parent=member_num)
    finish(s)
    mapping(obs, id, meta, body, headers)

s = add_screen('ENV-01', '클라이언트 연결 실패: curl exit 7', 'OBS-01', 'environment-client',
    'http://127.0.0.1:8331/v1/health', 'HTTP 응답 없음. clientStatus 000, httpStatus null, curl exitCode 7.',
    ['raw/health-02.stderr.txt', 'raw/health-02.meta.json'],
    '기본 주소로 접속을 시도했으나 curl이 연결 실패를 출력한 환경 관측이다. 제품 HTTP 오류 화면이 아니다.',
    'GET 시도 → curl stderr 읽기 → 메타에서 HTTP 응답 없음 확인. A-1의 환경 조사와 패키지 실행은 별도 조작이다.',
    'OBS-01 재수집 요청 health-02에서 관측. health-01과 합쳐 하나의 HTTP 응답으로 취급하지 않는다.')
elem(s, '1', 'curl 연결 실패 문구', read('raw/health-02.stderr.txt').strip(), 'raw/health-02.stderr.txt', lines=[1, 1])
finish(s)



s = add_screen('ENV-02', '설치 패키지 시작 실패: ERR_MODULE_NOT_FOUND', 'OBS-02', 'environment-installation',
    None, 'Node 시작 실패. result 파일에 httpServiceStarted:false, processRunning:false.',
    ['raw/bridge-start.stderr.txt', 'raw/bridge-start.meta.json', 'raw/bridge-start.result.json'],
    '최초 전달 패키지를 실행했을 때 누락 모듈 때문에 받은 Node 런타임 출력이다. 제품 HTTP 응답이나 실제 게임 행동 출력이 아니다. 오류 문자열 속 B 경로는 출력 문구로만 보존했으며 해당 파일을 열람하지 않았다.',
    '최초 Node 실행 → 누락 모듈 오류와 스택 읽기 → Node 버전 읽기. A-1은 이후 완전 패키지를 별도로 실행했다.',
    'OBS-02의 최초 패키지 실행 1회. 일반 제품 시작 화면과 분리한다.')
error_lines = read('raw/bridge-start.stderr.txt').splitlines()
for num, label, first, last in [('1','런타임 오류 위치',1,3), ('2','누락 모듈 오류',5,5), ('3','런타임 스택',6,15), ('4','오류 code·url',16,18), ('5','Node 버전',20,20)]:
    elem(s, num, label, '\n'.join(error_lines[first-1:last]), 'raw/bridge-start.stderr.txt', lines=[first,last])
finish(s)




intro = '''# A-2 실제 관측 출력설계서

입력은 A/A1-exploration.md와 그 문서에 명시된 새 raw 원문이다. 제품 소스·B 결과·기존 기획·A-3는 읽지 않았다. CLI/API이므로 그림 대신 원문 파일과 JSON pointer 또는 1부터 시작하는 줄 위치를 남긴다. 모든 경로는 A 기준 상대경로다.

제품 출력은 BRG-01~BRG-05의 5종이다. ENV-01~ENV-02는 제품 정상 화면과 분리한 환경·설치 관측 2종으로, 설계서 형식만 재사용하며 제품 화면·화면요소 노드에서는 제외하고 environmentObservations 배열로 보존한다. requestMappings는 실제 HTTP 응답 4개만 담는다. clientStatus 000은 HTTP 상태 코드가 아니다. 프로세스 시작 OBS-02·OBS-03은 HTTP 요청이 아니므로 requestMappings에서 제외한다. BRG-01 화면 URL은 stdout에 표시된 listening 주소다.

각 요소의 보이는 조건은 해당 관측 1회에 한정한다. 화면요소 표의 문자열은 원문 문구를 유지하며 줄바꿈만 표 안에서 표시한다. JSON 값은 구조·값을 보존한 compact 표기로 적었다. 부모 객체 요소와 하위 필드 요소는 같은 출력의 포함 관계다. 헤더의 날짜·연결 값도 그때의 관측값일 뿐 고정 표시 정책이 아니다. 정책은 전 화면에서 없음이며 JSON에 정책 노드·정책 연결도 없다.

실행 조건은 A-1의 별도 포트 18331·빈 AI 데이터·members 빈 설정·preflight 비활성·fake chat 미설정이다. 정상 plan 결과, 로그인된 계정, 실제 게임과 운영 설치판은 미관측이다. 사용자 후속 지시에 따라 실게임 화면·컴퓨터유즈 검증은 후속으로 유예되었으며 이 문서는 현재 HTTP·시작 출력만 다룬다. 제품 화면 전체 검증 완료를 뜻하지 않는다. 화면 흐름은 관측 조작의 서술이며 화면 간 navigate 연결을 만들지 않는다. 이 문서는 기능·코드 노드와 추측 연결을 생성하지 않는다.

## 출력 목록

| ID | 관측 | 분류 | 상태 |
|---|---|---|---|
| BRG-01 | OBS-03 | 제품 시작 콘솔 | graph 경고 뒤 listening |
| BRG-02 | OBS-04 | 제품 HTTP | 200, ok:false, 빈 격리 환경 |
| BRG-03 | OBS-05 | 제품 HTTP | 404, not found |
| BRG-04 | OBS-06 | 제품 HTTP | 400, unknown member |
| BRG-05 | OBS-07 | 제품 HTTP | 500, bridge / parse detail |
| ENV-01 | OBS-01 | 클라이언트·환경 | curl exit 7, HTTP 응답 없음 |
| ENV-02 | OBS-02 | 설치·런타임 | 모듈 누락, HTTP 서비스 미시작 |

'''
environment_nodes = [n for n in nodes if n['id'].startswith('ENV-')]
environment_observations = []
for n in environment_nodes:
    if n['type'] != '화면':
        continue
    item = {k:v for k,v in n.items() if k not in ('type','elements','policyIds')}
    item['status'] = None
    item['elements'] = [{k:v for k,v in e.items() if k != 'type'} for e in environment_nodes if e['id'].startswith(n['id']+'#')]
    if n['id'] == 'ENV-01':
        item.update(method='GET', url='http://127.0.0.1:8331/v1/health', path='/v1/health', clientStatus='000', clientExitCode=7, rawBody=None, rawMeta='raw/health-02.meta.json', rawStderr='raw/health-02.stderr.txt')
    else:
        item.update(method=None, path=None, rawBody=None, rawMeta='raw/bridge-start.meta.json', rawStderr='raw/bridge-start.stderr.txt', rawResult='raw/bridge-start.result.json')
    environment_observations.append(item)
nodes = [n for n in nodes if not n['id'].startswith('ENV-')]
edges = [e for e in edges if not e['source'].startswith('ENV-')]
graph = dict(nodes=nodes, edges=edges, requestMappings=mappings, environmentObservations=environment_observations)
output = intro + '\n'.join(sections) + '\n## 파싱용 그래프\n\n```json\n' + json.dumps(graph, ensure_ascii=False, indent=2) + '\n```\n'
HERE.mkdir(parents=True, exist_ok=True)
(HERE / 'screens.md').write_text(output, encoding='utf-8')

# Check the generated document, including source sections and exact observed values.
parsed = json.loads(re.search(r'```json\n(.*?)\n```', output, re.S).group(1))
ids = {n['id'] for n in parsed['nodes']}
assert len(ids) == len(parsed['nodes'])
for n in parsed['nodes']:
    assert n['source']['document'] == DOC
    assert n['source']['section'] in output
    if n['type'] != '화면요소':
        continue
    loc = n['location']
    if 'jsonPointer' in loc:
        value = json.loads(read(loc['rawFile']))
        for part in loc['jsonPointer'].split('/')[1:]:
            value = value[part.replace('~1', '/').replace('~0', '~')]
        assert value == n['visibleValue']
    else:
        first, last = loc['lines']
        assert '\n'.join(read(loc['rawFile']).splitlines()[first-1:last]) == n['visibleValue']
for e in parsed['edges']:
    assert e['source'] in ids and e['target'] in ids
assert len(parsed['requestMappings']) == 4
assert all(m['outputId'] in ids for m in parsed['requestMappings'])
assert all(n['type'] in ('화면','화면요소') for n in parsed['nodes'])
print(json.dumps(dict(document=DOC, screens=sum(n['type']=='화면' for n in nodes),
    elements=sum(n['type']=='화면요소' for n in nodes), policies=0, edges=len(edges),
    requestMappings=len(mappings), validation='passed'), ensure_ascii=False))



