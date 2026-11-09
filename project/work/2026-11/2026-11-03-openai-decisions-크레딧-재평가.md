# OpenAI Decisions 크레딧 조건 재평가

사용자는 OpenAI 무료 크레딧을 활용할 수 있다는 조건을 추가했다. 기존 [모델 조사](2026-11-03-jev대체모델-조사.md)의 유료 비용 중심 우선순위와 구분한다.

기존 동일 입력 평가에서 GPT-6 Luna Decisions 지수75.95는 Jev1.14의79.29보다3.34점, Flash1.2의81.94보다5.99점 낮다. 중앙값215ms·p95 582ms는 Jev136/175ms보다 느렸다. 다만 OpenRouter 경유·시점별 비교이며 Core Keeper 임무 성과나 OpenAI 직접 호출 지연을 확정하지 않는다. 평균 차이를 게임 오판율로 환산하지 않는다.

[OpenAI 공식 Decisions 문서](https://developers.openai.com/api/docs/guides/decisions)를 재확인했다. 현재 gpt-6-luna를 사용하며 구조화 상태와 고정 후보 선택 용도에 맞는다. [구독 API 크레딧 공식 안내](https://developers.openai.com/api/docs/guides/chatgpt-api-credits)는 Decisions API를 적용 대상으로 명시한다. 혜택은 연결된 API 조직에 적용되며 해당 조직의 프로젝트 키를 사용해야 한다. 사용자의 실제 잔액·만료일·계정 자격은 확인하지 않았다.

판단: 무료 크레딧을 실제 사용할 수 있다면 OpenAI Decisions를 첫 구현·시험 후보로 올리는 것이 합리적이다. 일반 평가 열세는 인정하되 제외할 정도의 게임 성능 부족이 입증된 것은 아니다. 평상시2~3초 잠정 주기보다 전투의 늦은 응답과 잘못된 행동 선택을 확인하는 것이 중요하다. 같은 실행기·후보를 유지해 Jev/Flash와 비교할 수 있게 두되, 아직 모델 채택이나 코드 변경을 결정한 것은 아니다.
