# Event Stream

EventStream은 값을 받아 경로 그래프를 따라 구독자에게 나눠 주는 기본 단위이다. 예를 들어 64명이 16종의 이벤트를 기다릴 때, 같은 종류를 기다리는 4명이 Filter 하나를 공유하면 그 조건은 값마다 한 번만 평가된다.

| 구분 | 내용 |
| --- | --- |
| 개요 | 구독과 경로로 구성하는 이벤트 전달 구조이다. |
| 목적 | 구독자가 많아도 값이 필요한 구독자에게만 적은 비용으로 전달한다. |
| 구현 | 스트림마다 상태 없는 노드 그래프를 두고 같은 Filter 객체를 쓰는 경로를 합친다. 상태는 연산자가 만든 새 스트림이 가진다. |

## 구성

사용자가 다루는 형식은 EventStream, EventPath, Filter이다.

- EventStream은 값을 받는 입구와 그래프를 가진다. 어떤 스트림이든 OnNext로 값을 직접 받을 수 있다.
- EventPath는 Where와 Debug로 만든 경로이다. 경로를 만드는 동안 그래프에 구독이 생기지 않는다.
- Filter는 Where 노드의 공유 기준이다. 같은 부모 아래 같은 Filter 객체를 쓴 Where만 합쳐진다.

Take·TakeWhile·Skip·SkipWhile·Branch·Merge는 경로 노드가 아니라 새 EventStream을 시작한다. 상위 스트림의 그래프에는 새 스트림으로 가는 연결 하나만 남고, 횟수 같은 상태는 새 스트림이 가진다. 그래서 그래프에는 상태가 없다.

## 경로와 수명

1. EventStream을 만들거나 외부 `IObservable<T>`를 생성자에 전달한다. 외부 공급원의 수명은 호출자에게 있다.
2. Where, Debug로 경로를 만들거나 Take, Skip, Branch, Merge로 새 스트림을 시작한다.
3. Subscribe가 그 지점에 구독을 등록하고 개별 Handle을 반환한다. 새 스트림은 첫 구독 때 상위에 연결된다.
4. 값은 각 노드의 구독자를 등록 순서로 방문한 뒤 자식을 깊이 우선으로 방문한다. 조건이 거짓이면 그 아래의 구독과 조건을 모두 건너뛴다.
5. 마지막 구독을 해제하면 빈 노드를 루트 방향으로 제거한다. 사용자가 만든 EventStream은 다시 구독할 수 있고, 연산자·Branch·Merge가 만든 스트림은 함께 폐기된다.

완료와 오류는 조건을 평가하지 않고 모든 활성 구독에 전달한다. Dispose는 통지 없이 연결을 해제한다. 값 전파·구독·해제는 같은 스레드에서 수행하는 동기식 작업이다.

## 모듈 안내

| 필요한 작업 | 문서 |
| --- | --- |
| 입력 방식, 전파 중 변경, 실패와 종료 규칙 확인 | [EventStream](EventStream.md) |
| 외부 Observable 연결과 발행 권한 제한 | [외부 입력](ExternalInput.md) |
| 독립 분기를 만들거나 여러 스트림을 합치기 | [Branch와 Merge](BranchAndMerge.md) |
| 사건 종류를 골라 전달하고 횟수나 조건으로 중단하기 | [경로 연산자](EventStreamOperators.md) |
| 구독 순서를 여러 단계로 나누기 | [순서 그룹](OrderGroups.md) |
| 비트마스크 필터, 독립 분기, 그룹별 구독 작성 | [사용 예제](Usage.md) |

교체 당시의 측정 근거는 [프로토타입 평가](Issues/EventStreamPrototype.md)와 [본 구현 보고서](Issues/EventStreamImplementation.md)에, 이후의 구조 결정은 [승인 기록](Issues/Approvals.md)에 있다.
