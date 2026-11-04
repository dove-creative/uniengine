# EventStream 승인 기록

EventStream 본 구현에서 GPT가 정했거나 검토 중에 제안된 결정의 승인 상태를 기록한다. 항목은 하나씩 확정한다. 결정의 배경은 [작업 목록](EventStreamTodo.md), 측정 근거는 [본 구현 보고서](EventStreamImplementation.md)에 있다.

## 목차

1. 승인 현황
2. 결정 내용
3. 적용 기록

## 승인 현황

| ID    | 대상              | 결정                                                                     | 상태      |
| ----- | --------------- | ---------------------------------------------------------------------- | ------- |
| AP-01 | ES-02 상태 단위     | Take·TakeWhile·Skip·SkipWhile은 공급원 없는 새 EventStream을 시작하고, 상태는 그 스트림이 가진다. | 승인·적용   |
| AP-02 | ES-03 종료 통지     | 별도 규칙을 두지 않고, 스트림이 끝날 때 활성 구독에 완료를 통지하는 일반 규칙을 따른다.                    | 승인·적용   |
| AP-03 | ES-04 경로 크기     | 형제가 8개 이상인 부모는 Filter 색인을 쓰고, 4개 미만이 되면 색인을 회수한다. 경로 깊이는 256단계까지 허용한다. | 승인      |
| AP-04 | ES-05 Branch 연결 | Branch는 첫 구독 때 부모에 연결한다.                                               | 승인      |
| AP-05 | ES-08 공개 단위     | 기본 단위는 EventStream이다. 별도의 공급원 계약은 두지 않고 외부 입력은 `IObservable<T>`로 받는다. | 승인·적용   |
| AP-06 | ES-09 Append    | 독립 분기는 Append 대신 Branch로 만든다.                                          | 승인      |
| AP-07 | ES-10 Merge     | Merge는 EventStream을 입력으로 받는 공급원 없는 릴레이션이다. 경로는 Branch로 넘기고, 그래프 안에 합류 노드는 두지 않는다. | 승인·적용   |
| AP-08 | ES-14 다이어그램     | GPT가 바꾼 상자 이름을 되돌리고, 다이어그램은 사용자가 직접 정리한다.                              | 승인·적용   |
| AP-09 | 측정 자료           | 측정 자료 zip을 저장소에서 제거한다.                                                 | 승인·적용   |
| AP-10 | EventSource 예외  | 처리기가 없을 때 일반 구독자가 실패해도 남은 구독에 값을 전달한 뒤 첫 예외를 다시 던진다.                   | 승인·적용   |
| AP-11 | 문서 이름           | 내용이 바뀐 문서의 파일 이름을 고치고 README의 개요 표를 복원한다.                               | 승인·적용   |
| AP-12 | 릴레이션 구조         | Take 계열, Merge, Branch는 공급원 없는 릴레이션이다.                                  | 승인·적용   |
| AP-13 | 직접 입력           | 모든 EventStream의 루트는 OnNext로 값을 직접 받는다. 넣은 값은 위쪽에서 온 값과 같게 처리한다.      | 승인·적용   |
| AP-14 | 공급원 삭제          | EventSource 클래스와 IEventSource를 모두 없앤다.                                    | 승인·적용   |
| AP-15 | 관찰자 계약          | EventStream은 `IObserver<T>`를 구현하여 다른 공급원의 수신자가 될 수 있다.                  | 승인·적용   |

## 결정 내용

### AP-01·02 상태 있는 연산자

Take·TakeWhile·Skip·SkipWhile은 경로 노드가 아니라 공급원 없는 새 EventStream을 시작한다. 그래프에는 상태를 두지 않고, 상태는 연산자를 호출할 때 만들어진 스트림이 가진다.

- 같은 스트림을 구독한 쪽끼리 횟수와 진행 상태를 공유한다. 연산자를 다시 호출하면 새 상태로 시작한다.
- 스트림은 Branch처럼 첫 구독 때 부모에 연결되고, 그때부터 값을 센다. 나중에 구독한 쪽은 남은 상태를 이어받는다.
- 연산자 스트림이 끝나면 일반 종료 규칙에 따라 활성 구독에 완료를 통지하고 폐기된다. 부모에서는 그 스트림으로 가는 연결만 빠진다.
- 끝난 스트림에 구독하면 ObjectDisposedException이 발생한다.
- 연산자 스트림에 직접 넣은 값도 위쪽에서 온 값과 같게 세어 남은 횟수와 진행 상태에 반영한다.

### AP-05·07 공개 단위와 Merge

사용자가 다루는 기본 단위는 EventStream이다. 별도의 공급원 계약은 두지 않는다.

- 공개 형식은 EventStream, EventPath, Filter이다. EventSource, IEventSource, 공급원 형식이던 `Merge<T>`는 없앤다.
- 외부 입력은 `IObservable<T>`를 EventStream 생성자에 전달한다.
- Merge는 EventStream을 입력으로 받고 s2.Merge(s1)처럼 호출한다. 호출한 스트림도 입력에 포함한다. 경로는 Branch로 넘기며, 양쪽 입력을 모두 통과한 값은 두 번 전달한다.
- 이에 따라 Chain·Connect, IPassiveObserver, TerminationMode 지정, EventSource의 파생 지점은 공개 API에서 사라진다.

### AP-12 릴레이션 구조

모든 릴레이션은 입력을 받는 루트와 그 아래 그래프로 구성한다. 릴레이션의 차이는 입력과 루트가 하는 일이다.

| 릴레이션 | 입력 | 루트가 하는 일 |
| --- | --- | --- |
| 사용자가 만든 EventStream | OnNext, 외부 공급원 | 그래프를 시작한다. |
| Take·TakeWhile·Skip·SkipWhile | 상위 하나 | 상태를 갖는다. Take와 TakeWhile은 끝나면 종료한다. |
| Merge | 상위 여러 개 | 입력을 모아 전달한다. |
| Branch | 상위 하나 | 값을 그대로 전달한다. |

- 상위 연결은 호출한 지점에서 시작한다. s1.Take(2)는 s1의 루트에서, s1.Where(f).Take(2)는 그 Where 노드에서 값을 받는다.
- 서로 다른 릴레이션의 Where는 같은 Filter를 써도 공유하지 않는다.
- Branch의 Enabled는 릴레이션 루트의 값으로 제어한다. Branch를 폐기하면 그 아래의 구독이 모두 해제된다.
- 값을 직접 넣는 데 공급원은 필요하지 않다. 외부 입력을 받는 스트림을 포함한 모든 EventStream의 루트는 OnNext로 값을 받고, 그 값은 해당 릴레이션 아래로만 흐른다. 값 주입을 막으려면 EventPath나 `IObservable<T>`로 넘긴다.

### AP-14 공급원 삭제

모든 EventStream이 루트로 값을 직접 받으므로 별도의 공급원 객체와 계약이 필요 없다. AP-12 그림의 Event Source는 코드에서 스트림의 루트 입구에 해당한다.

- 외부 입력은 `IObservable<T>`를 생성자에서 바로 구독한다. 상위 경로 입력은 내부 형식 Upstream으로 보관했다가 첫 구독 때 연결한다.
- 구독 목록을 가진 EventSource 클래스를 없앤다. 스트림의 예외 규칙은 그래프 하나로 통일되어 AP-10의 동작을 모든 구독에 적용한다.
- 구독마다 래퍼를 만들던 경로가 사라져 AdaptiveCollection의 AddPrepared를 되돌린다.

## 적용 기록

- AP-01·02·05·07·12·13·14: EventStream, EventPath, 그래프와 연산자 코드를 고치고 EventSource, IEventSource, `Merge<T>`, TerminationMode, IPassiveObserver를 제거했다. 전체 테스트가 통과했다. 측정 결과는 [검증 결과](../../../../../tests/02-Core/02-E.3-EventStream/UniTest/03-Results.md)에 있다.
- AP-15: 이전 공개 API와 비교하다 빠진 것을 확인하고 복원했다. 수신자로 넘긴 스트림은 일반 구독으로 동작한다.
- AP-08: 다이어그램을 마지막 커밋과 같은 내용으로 되돌렸다.
- AP-09: zip을 저장소 밖으로 옮기고, 본 구현 보고서에서 zip을 가리키던 문장을 고쳤다.
- AP-10: EventSource를 없애면서 같은 규칙이 그래프의 순회로 일원화되었다.
- AP-11: 문서 이름을 BranchAndMerge, ExternalInput, OrderGroups로 바꾸고 TerminationMode 문서를 없앴다. README에 개요 표를 복원했다.
