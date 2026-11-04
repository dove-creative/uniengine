# Event Stream

EventSource는 값을 발행하고, EventStream은 공급원 하나를 구독하여 값을 그래프의 경로에 따라 나눠 준다. 예를 들어 64명이 16종의 이벤트를 기다릴 때, 같은 종류를 기다리는 4명이 Filter 하나를 공유하면 그 조건은 값마다 한 번만 평가된다.

## 경로와 수명

1. EventStream을 만들거나 외부 공급원을 생성자에 전달한다. 외부 공급원의 수명과 발행 권한은 호출자에게 있다.
2. Where, Skip, Take 등의 연산으로 EventPath를 만든다. 경로를 만드는 동안 그래프에 구독이 생기지 않는다.
3. Subscribe가 해당 경로에 구독을 등록하고 개별 Handle을 반환한다. 같은 부모 아래 같은 Filter 객체를 사용한 Where만 공유된다.
4. 값은 각 노드의 구독자를 등록 순서로 방문한 뒤 자식을 깊이 우선으로 방문한다. 조건이 거짓이면 그 아래의 구독과 조건을 모두 건너뛴다.
5. 마지막 구독을 해제하면 빈 노드를 루트 방향으로 제거한다. 사용자가 만든 EventStream은 다시 구독할 수 있다.

완료와 오류는 조건을 평가하지 않고 모든 활성 구독에 전달한다. Dispose는 통지 없이 연결을 해제한다. 두 경우 모두 소유한 공급원은 함께 폐기한다. 값 전파·구독·해제는 같은 스레드에서 수행하는 동기식 작업이다.

## 모듈 안내

| 필요한 작업 | 문서 |
| --- | --- |
| 발행 권한, 전파 중 변경, 실패와 종료 규칙 확인 | [EventStream](EventStream.md) |
| 공급원 구현 또는 외부 Observable 연결 | [공급원과 관찰자 계약](EventStreamInterfaces.md) |
| 경로를 독립적으로 발행하거나 여러 공급원을 합치기 | [경로 연결과 병합](EventStreamExtensions.md) |
| 사건 종류를 골라 전달하고 횟수나 조건으로 중단하기 | [경로 연산자](EventStreamOperators.md) |
| 구독 순서를 여러 단계로 나누기 | [순서 그룹](OrderedEventStream.md) |
| 마지막 구독을 해제한 뒤 공급원의 수명 확인 | [TerminationMode](TerminationMode.md) |
| 비트마스크 필터, 독립 분기, 그룹별 구독 작성 | [사용 예제](Usage.md) |

교체 당시의 측정 근거는 [프로토타입 평가](Issues/EventStreamPrototype.md), 항목별 결정과 실제 측정 범위는 [본 구현 보고서](Issues/EventStreamImplementation.md)에 기록했다.
