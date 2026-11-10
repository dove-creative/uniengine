# Event Stream

EventStream은 값을 받아 경로 그래프를 따라 구독자에게 나눠 주는 기본 단위이다. 예를 들어 64명이 16종의 이벤트를 기다릴 때, 같은 종류를 기다리는 4명이 조건 함수 하나를 공유하면 그 조건은 값마다 한 번만 평가된다.

| 구분 | 내용 |
| --- | --- |
| 개요 | 구독과 경로로 구성하는 이벤트 전달 구조이다. |
| 목적 | 구독자가 많아도 값이 필요한 구독자에게만 적은 비용으로 전달한다. |
| 구현 | 스트림마다 노드 그래프를 두고 같은 조건 대리자나 마스크 비트를 쓰는 경로를 합친다. 횟수 같은 상태는 연산자가 만든 새 스트림의 입구 노드가 가진다. |

## 구성

사용자가 다루는 형식은 EventStream과 EventPath이고, 일반 조건은 `Func<T, bool>` 대리자로, ulong 스트림의 비트 조건은 flags enum 마스크로 넘긴다.

- EventStream은 값을 받는 입구와 그래프를 가진다. 어떤 스트림이든 OnNext로 값을 직접 받을 수 있다.
- EventPath는 Where, ForFlags, Debug, ForOrder로 만든 경로이다. 경로를 만드는 동안 그래프에 구독이 생기지 않는다.
- 조건 대리자는 Where 노드의 공유 기준이다. 같은 부모 아래 메서드와 대상이 같은 대리자를 쓴 Where만 합쳐진다.
- ForFlags는 값과 마스크에 공통 비트가 하나라도 있는 경로만 통과시킨다. 같은 부모 아래 마스크 비트가 같으면 enum 형식과 관계없이 항목을 공유한다.

Take·TakeWhile·Skip·SkipWhile·Branch·Merge는 경로 노드가 아니라 새 EventStream을 시작한다. 상위 스트림의 그래프에는 새 스트림으로 가는 연결 하나만 남고, 횟수 같은 상태는 새 스트림의 입구 노드가 가진다. 그래서 공유되는 경로 노드에는 상태가 없다.

## 경로와 수명

1. EventStream을 만들거나 외부 `IObservable<T>`를 생성자에 전달한다. 외부 공급원의 수명은 호출자에게 있다.
2. Where, ForFlags, Debug, ForOrder로 경로를 만들거나 Take, Skip, Branch, Merge로 새 스트림을 시작한다.
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
| 필터, 독립 분기, 그룹별 구독 작성 | [사용 예제](Usage.md) |
| 개별 순회와 파이프라인의 비용 비교 | [벤치마크](Benchmark.md) |
| 지금 구조를 고른 이유와 대가, 보류 항목 확인 | [설계 결정](Design%20Decisions.md) |

설계 기준은 [EventStream](EventStream.md)에, 테스트 결과는 [검증 결과](../../../../tests/02-Core/02-E.3-EventStream/docs/03.%20Results.md)에 있다.

EventStream은 매 프레임 많은 구독자에게 값을 나누는 구조라 전파와 구독 비용이 설계의 중요한 기준이다. 그래서 구조를 바꿀 때는 기능 테스트와 함께 벤치마크로 이전 버전과 비용을 비교한다. 측정 도구는 테스트 폴더의 [벤치마크 도구](../../../../tests/02-Core/02-E.3-EventStream/Benchmarks/README.md)에 있고, 측정 결과와 해석은 [벤치마크](Benchmark.md)에 정리한다.
