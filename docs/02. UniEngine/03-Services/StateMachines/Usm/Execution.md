# USM 실행

USM에서 노드는 여러 갱신에 걸쳐 작업의 수명을 유지하고, FrameContext는 그중 한 번의 외부 호출에서 실행한 내용을 기록한다.

## 목차

- 작업 수명
- 입력과 결과
- 프레임 기록
- 같은 프레임에서 다시 실행하기
- 정지와 예외

## 작업 수명

| 시점 | 노드가 지키는 동작 |
| --- | --- |
| 첫 Update | Work를 시작하고 갱신한다. Work가 Running이고 전이기가 있으면 전이기도 시작하고 갱신한다. |
| Running 중 Update | 활성 Work와 전이기의 수명을 이어 간다. 전이기는 이번 Work의 결과를 입력으로 받는다. |
| Work 또는 전이기 종료 | 해당 결과가 노드의 LastResult가 된다. 이번 Work가 먼저 끝났다면 전이기 갱신을 건너뛰고 이미 활성인 자손도 정리한다. |
| 종료 후 Update | 같은 노드에서 Work의 새 수명을 시작한다. 초기화 때 받은 Owner와 Blackboard는 그대로 사용한다. |

Work를 확장할 때 CheckCondition은 주어진 입력으로 실행 가능 여부를 판단하고, Update는 작업의 진행 상태와 출력을 반환한다. 한 실행에 필요한 상태는 Start에서 준비하고 Stop에서 정리한다. 기본 Work의 OnStart·OnUpdate·OnStopped로 등록한 콜백도 사용할 때는 재정의한 메서드에서 해당 base 메서드를 호출한다.

## 입력과 결과

Blackboard는 트리를 초기화할 때 전달한 공유 객체이다. 반면 Payload는 호출자가 이번 FrameContext에 담은 입력에서 시작해 작업을 거치며 바뀐다. OperationInfo는 상세 상태와 Result를 함께 담는 불변 값이다. `TryGetResult<T>`는 Result가 T에 해당하면 값을 반환하고 null이거나 형식이 다르면 false를 반환한다.

| OperationStatus | DetailedOperationStatus | 판단 기준 |
| --- | --- | --- |
| NeverRun | NeverRun | 아직 실행 결과가 없다. Work와 전이기의 Update는 Running 또는 종료 상태를 반환해야 한다. |
| Running | Running | 다음 갱신에서도 같은 실행을 이어 간다. |
| Success | Success, ChildCompleted | 작업이 성공했거나 전이기가 자식 흐름의 완료를 보고했다. |
| Failure | Failure, ChildFailed | 작업이 실패했거나 전이기가 자식 흐름의 실패를 보고했다. |
| Halted | HaltedBySelf, Preempted, HaltedByParent, HaltedByExternal | 각각 자신의 조건 거부, 다른 자식의 선점, 부모의 정지, 외부 정지를 나타낸다. |

예를 들어 Sequence의 첫 자식이 `Success`와 경로를 반환하면 같은 갱신에서 다음 자식이 그 경로를 입력으로 받는다. 마지막 자식도 성공하면 부모는 `ChildCompleted`와 마지막 출력을 반환한다. 같은 경로를 다음 **외부** 갱신의 입력으로도 쓰려면 호스트가 새 FrameContext에 그 값을 넣는다. Parallel에서는 각 자식이 같은 부모 입력을 받고 부모가 자식별 결과 목록을 반환한다.

## 프레임 기록

Update가 반환하는 OperationInfo는 노드의 최종 결과이다. 그 결과에 이른 경로는 호출할 때 넘긴 FrameContext에 남는다.

| 조회 대상 | 읽을 수 있는 정보 |
| --- | --- |
| Id, Payload | 해당 노드의 ID와 그 위치에서 마지막으로 전달된 값 |
| Reports | 이 노드에서 기록한 Node·Work·Transitioner의 시작·갱신·정지와 진입·종료 단계. 결과가 기록된 항목에서는 OperationInfo도 읽을 수 있다. |
| Nexts | 방문 순서대로 모은 자식 컨텍스트. 같은 부모에서 마지막으로 방문한 자식을 연속 방문하면 같은 컨텍스트에 기록을 이어 쓰고 입력을 갱신한다. |
| UpdateCount, MaxNodeUpdates | 이번 호출에서 트리 전체가 사용한 노드 갱신 횟수와 허용 한도 |

실행에는 구체 형식 FrameContext를 사용한다. IFrameContext는 기록을 읽기 위한 계약이다. 매 외부 Update에 새 컨텍스트를 주면 이전 호출의 기록과 예산을 독립적으로 보관할 수 있다.

## 같은 프레임에서 다시 실행하기

ReframeOption은 노드가 이번 갱신을 마친 직후에 처리하는 일회성 요청이다. 전이기의 LoopType이 다음 전이기 갱신에서 회차를 시작하는 것과 실행 시점이 다르다.

| ReframeOption | 동작 |
| --- | --- |
| None | 현재 갱신의 결과를 반환한다. |
| Update | 직전 출력을 입력으로 한 번 더 갱신한다. Running이면 수명을 이어 가고, 이미 끝났다면 새 수명을 시작한다. |
| CheckConditionThenUpdate | 직전 출력으로 Work의 조건을 검사한다. 참이면 다시 갱신하고 거짓이면 HaltedBySelf로 끝낸다. |

재실행 중 콜백이 같은 옵션을 다시 지정해도 새 요청으로 처리한다. 루트·자식·재실행은 기본 1,024회의 갱신 예산을 함께 쓴다. Self나 Reframe의 조건 거부처럼 노드 갱신을 거부 결과로 대체한 시도도 1회로 계산한다. FrameContext 생성 시 양의 maxNodeUpdates로 한도를 바꿀 수 있다. 이 한도는 노드 갱신 횟수에 적용되며 동기 콜백의 소요 시간은 제한하지 않는다.

## 정지와 예외

Stop은 활성 자손부터 정지한 뒤 해당 노드의 전이기, Work 순서로 정리한다. 외부 Stop의 기본 사유는 HaltedByExternal이고, 부모의 정리로 멈추는 자손은 HaltedByParent를 받는다. 자식이 낸 마지막 Result는 정지 후에도 남는다. 유효한 종료 사유로 비활성 노드를 다시 정지해도 종료 콜백은 한 번만 호출된다.

실행 콜백에서 명시적으로 Stop하면 그 실행의 남은 단계와 Reframe 요청을 취소한다. 같은 노드의 Update 재진입, 정지 중인 노드의 재실행, 비활성 조상 아래에서의 자식 실행은 거부한다. 트리는 한 호스트가 동기적으로 갱신한다.

Work·전이기를 실행하다 예외가 발생하거나 갱신 한도를 넘으면 Failure를 기록하고 활성 계층을 정리한 뒤 예외를 호출자에게 전달한다. 정리 콜백이 실패해도 나머지 정리를 시도하며, 실행 오류와 정리 오류가 함께 발생하면 AggregateException으로 둘 다 전달한다.

[USM](README.md) · [사용 예제](Usage.md)
