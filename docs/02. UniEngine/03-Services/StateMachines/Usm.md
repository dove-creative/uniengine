# Usm

USM은 작업과 자식의 실행 흐름을 분리하여 FSM·BT·병렬 진행을 같은 노드 계층에서 조합하는 상태 기계 서비스이다.

## 목차

- 소개
- 구성과 소유
- 상태와 데이터 전달
- 실행 수명과 Reframe
- 전이 정책
- 실행 제한과 범위

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | Node가 Work·Transitioner·자식을 소유하고 실행 수명을 관리한다. |
| 목적 | 작업 내용과 다음 자식의 선택 규칙을 독립적으로 조합한다. |
| 구현 | 객체를 직접 생성하고 루트를 초기화한 뒤 외부에서 Update·Stop을 호출한다. |

EventStream에 의존하거나 갱신을 자동 구독하지 않는다. 호출자가 실행 시점과 입력을 제공하고 결과와 계층별 실행 기록을 관찰한다.

## 구성과 소유

공개 구현은 `BlackThunder.UniEngine.StateMachines.Usm`에 둔다. INode·IWork·ITransitioner와 FrameContext는 하위 Core에 둔다.

| 구성 요소 | 책임 |
| --- | --- |
| Node / INode | ID, Children, Work, Transitioner, AbortPolicies, ReframeOption과 실행 상태를 제공한다. |
| Work / IWork | 조건 검사·시작·갱신·정지를 정의한다. 기본 Work는 입력을 그대로 반환하며 Running을 유지한다. |
| Transitioner / ITransitioner | Node가 소유한 자식의 선택과 갱신을 정의한다. |
| ExplicitTransitioner | MatrixTransitioner·DirectTransitioner의 공통 기반이다. |
| EvaluationTransitioner | 조건·우선순위·LoopType을 사용하는 Selector·Sequence의 공통 기반이다. |
| Parallel | 여러 자식을 진행하고 생성 시 선택한 완료 정책으로 결과를 합산한다. |
| DecisionMatrixBuilder / DecisionMatrix | FSM 규칙을 조립하고 완성된 결정표를 조회한다. |
| OperationInfo | 상세 상태와 Result를 담는 불변 값이다. |
| FrameContext / IFrameContext | Payload, 계층별 실행 기록, 프레임 전체 실행 예산을 전달한다. |

자식 목록은 생성 시 복사하며 이후 변경할 수 없다. Work 생략 시 기본 Work를 사용하고, 자식이 있으면 Transitioner가 필요하다. NodeBuilder와 별도 Content 구성 객체는 사용하지 않는다.

초기화 전에 형제 ID 중복·순환·자식 공유·Work 공유·Transitioner 공유를 검사한다. 형제 ID는 대소문자를 구분하며 다른 가지에서는 같은 ID를 사용할 수 있다. 별도 트리 사이에서도 실행 객체를 공유하지 않는다. 완성된 DecisionMatrix는 공유할 수 있다.

루트 Initialize 한 번으로 전체 트리에 같은 Owner·Blackboard를 전달한다. 초기화 완료·실패 이후 재초기화할 수 없다. 구조 검증 실패도 해당 루트의 초기화 실패이다.

StateMachineNames.GetName은 문자열, Type.Name, 그 밖의 객체의 ToString 결과를 이름으로 사용한다. USM 상태·전이 정책은 SCP 공통 계약에 포함하지 않는다.

## 상태와 데이터 전달

OperationInfo가 저장하는 상태는 DetailedOperationStatus 하나이며 OperationStatus는 계산한다. Result는 작업 출력이다. TryGetResult<T>는 Result가 T에 해당하면 값을 반환하고, null이나 형식 불일치는 false이다.

| DetailedOperationStatus | OperationStatus |
| --- | --- |
| NeverRun | NeverRun |
| Running | Running |
| Success, ChildCompleted | Success |
| Failure, ChildFailed | Failure |
| HaltedBySelf, Preempted, HaltedByParent, HaltedByExternal | Halted |

Work의 출력 Result가 전이기와 자식의 입력이다. 자식 출력은 부모 Payload와 OperationInfo.Result로 돌아온다. 순차 전이는 이전 자식의 출력을 다음 자식에게 전달한다. Parallel은 같은 부모 출력을 각 자식에게 전달하고 결과 목록을 반환한다.

각 외부 Update에는 새 FrameContext를 사용한다. 자식 컨텍스트는 방문 순서와 계층을 보존하고 재사용 시 입력을 갱신한다. 실행에는 FrameContext를 사용하며 IFrameContext는 관찰 계약이다.

## 실행 수명과 Reframe

첫 Update에서 시작하고 Running 동안 시작 콜백을 반복하지 않는다. 종료 이후 Update는 새 실행이다. Work가 종료하면 해당 실행의 전이기 갱신을 생략한다. 루트 진입 조건은 호출자가 CheckCondition으로 확인한다.

Stop은 활성 자손 → Transitioner → 부모 Work 순서로 처리한다. 부모 정지로 끝나는 자손은 HaltedByParent가 된다. 비활성 노드의 반복 Stop은 종료 콜백을 반복하지 않는다. 콜백 중 명시적 Stop은 남은 실행과 Reframe 요청을 취소한다.

| ReframeOption | 추가 실행 |
| --- | --- |
| None | 요청 없음 |
| Update | 자신을 다시 갱신 |
| CheckConditionThenUpdate | 최신 출력으로 조건을 검사하고 참이면 갱신, 거짓이면 HaltedBySelf |

ReframeOption은 소비되는 요청 플래그이다. Running이면 수명을 이어가고 정상 종료했다면 새 실행을 시작한다. 재실행 후 소비한 요청을 초기화하되 재실행 중 새로 지정한 요청은 보존한다. 같은 옵션을 다시 지정해도 새 요청이므로 한 프레임에 여러 번 재실행할 수 있다.

## 전이 정책

### ExplicitTransitioner

MatrixTransitioner는 결정표로 다음 자식을 정한다. FromEntry는 초기 자식, From은 출발 자식, When은 상세 상태 또는 그 조건 함수, With는 Result 일치 조건, GoTo는 목적지이다. FromEntry의 With는 WithEntryBuilder를 통해 초기 입력 규칙을 구성한다. Build 이후 Builder와 보관한 하위 Builder로 결정표를 변경할 수 없다.

- 명시 상태 규칙을 조건 함수보다 먼저 평가하고, 각 규칙에서 Result 일치를 기본 목적지보다 우선한다.
- 조건 함수는 등록 순서대로 평가한다. 같은 대리자는 Result 조건과 기본 목적지를 공유하며 동일 조건 재등록은 목적지를 교체한다.
- Result의 null은 비교값이고 목적지 null은 명시 종료이다. GetNext는 검색 성공 여부와 목적지를 구분한다. 출발 ID null은 초기 규칙 조회이다.
- 초기 규칙이 없으면 Running, 후속 규칙이 없으면 자식 결과를 전달한다. 없는 자식 참조는 초기화 시 거부한다.
- 다른 자식 전이는 같은 갱신에서 진행하고 자기 전이는 해당 갱신을 마친다. Reframe 요청은 별도로 처리한다.
- Self 조건 실패는 HaltedBySelf이며 결정표의 복구 규칙을 적용할 수 있다. StopOnFailure는 실패를 결정표보다 먼저 전파하는 정책이 아니다.

DirectTransitioner는 SetNext로 다음 자식을 지정한다. 다음 전이기 갱신에서 적용하고 적용 전 여러 요청은 마지막 요청을 사용한다. 자식 콜백의 요청도 다음 전이기 갱신까지 대기한다. null은 명시 종료이다. 요청이 없으면 현재 자식을 실행하고 선택한 자식이 없으면 Running이다. 자식 종료 시 추가 요청이 없으면 그 결과를 전달한다. 초기화 후 없는 ID 요청은 즉시 거부한다.

### EvaluationTransitioner

Selector의 한 회차는 첫 성공 또는 모든 후보 소진까지이다. Sequence는 전체 성공 또는 중간 실패까지이다. Running 자식과 Sequence의 진행 위치는 다음 갱신까지 유지한다.

| LoopType | 회차 성공 | 회차 실패 |
| --- | --- | --- |
| None | ChildCompleted | ChildFailed |
| Conditional | 반복 | ChildFailed |
| Forced | 반복 | 반복 |

반복은 Running을 반환하고 다음 전이기 갱신에서 첫 자식부터 평가하는 것이다. LoopType 자체는 즉시 재실행을 요청하지 않는다. 빈 목록은 성공한 회차로 처리하여 같은 정책을 적용한다.

초기 선택 시 조건을 검사한다. 실행 중 Self 조건 실패는 해당 자식을 HaltedBySelf로 정지한다. Selector는 다음 후보를 평가하고 Sequence는 회차 실패이다. 앞선 자식의 LowerPriority 조건이 참이면 현재 자식을 Preempted로 정지하고 앞선 자식을 선택한다.

StopOnFailure는 Selector에서 실패한 자식을 지나 다음 후보를 평가하게 하는 자식 정책이다. 정책이 없는 자식이 실패하면 Running을 반환하고 다음 평가에서 첫 후보부터 선택한다. 이때 회차가 종료된 것이 아니므로 LoopType을 적용하지 않는다. 외부 정지는 실패로 취급하지 않는다.

### Parallel

Parallel은 스레드를 생성하지 않고 자식 순서대로 미완료 자식을 갱신한다. 완료된 자식은 같은 실행에서 재시작하지 않는다. LoopType은 사용하지 않는다.

| ParallelCompletionPolicy | 성공 | 실패 | 빈 목록 |
| --- | --- | --- | --- |
| AllSuccess, 기본값 | 전부 성공 | 하나라도 실패 | 성공 |
| AnySuccess | 하나라도 성공 | 전부 실패 | 실패 |

초기 조건 거부·Self 중단·성공 이외의 자식 종료를 실패로 집계한다. LowerPriority와 StopOnFailure는 적용하지 않는다. 완료 기준에 도달하면 나머지 활성 자식을 HaltedByParent로 정지하며 미시작 자식은 NeverRun이다.

Running을 포함한 각 Update의 Result는 자식 순서의 IReadOnlyList<OperationInfo> 스냅샷이다. 조기 완료 시 정지한 자식의 최종 상태와 결과도 담는다. 이후 갱신은 이전 스냅샷을 변경하지 않는다.

## 실행 제한과 범위

같은 노드의 Update 재진입과 비활성 조상 아래의 자식 실행을 거부한다. 동시 실행은 지원하지 않는다. 실행 예외는 실패 상태를 기록하고 활성 계층을 정리한 뒤 전파한다. 정리 예외가 발생해도 나머지 정리를 시도하며 원래 예외와 함께 보존한다.

FrameContext의 기본 갱신 한도는 루트를 포함하여 1,024회이다. maxNodeUpdates로 양의 한도를 지정한다. 모든 자식·Reframe이 예산을 공유하며 실제 갱신 대신 처리한 조건 거부 시도도 포함한다. 한도 초과 시 예외와 계층 정리를 발생시킨다. 동기 콜백의 실행시간은 제한하지 않는다.

netstandard2.1·C# 9를 사용한다. EventStream 연동, Injection, SCP, 기존 프로젝트 소비 코드 변경, Unity 실행 검증은 포함하지 않는다.

[사용 예제](Usm%20Usage.md) · [검증 계획](../../../../tests/03-Services/StateMachines/Usm/docs/02.%20Plans.md) · [상위 구성](../README.md)
