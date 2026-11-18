# USM 전이 정책

전이기는 부모 Work가 Running을 반환했을 때 자식을 선택하고, 자식의 결과를 부모 노드의 실행 결과로 바꾼다. 자식을 고르는 기준과 부모의 완료 시점은 전이기 종류에 따라 달라진다.

## 목차

- ID로 선택하는 전이
    - MatrixTransitioner
    - DirectTransitioner

- 조건으로 선택하는 전이
    - Selector와 Sequence
    - 회차 반복

- 여러 자식을 진행하는 Parallel

## ID로 선택하는 전이

ExplicitTransitioner 계열은 한 시점에 자식 하나를 ID로 선택한다. 다른 ID로 전환할 때 실행 중인 이전 자식은 Preempted로 정지한다. 선택된 자식의 AbortPolicies에 Self가 있으면 갱신 전에 조건을 검사하며, 조건이 거짓이면 그 자식의 결과는 HaltedBySelf이다.

### MatrixTransitioner

결정표는 시작 위치와 자식을 실행한 뒤의 목적지를 정한다. DecisionMatrixBuilder의 FromEntry는 진입 규칙이며, With로 부모 입력별 시작점을 지정할 수 있다. From은 출발 자식 ID, When은 자식의 상세 상태나 이에 대한 조건 함수, With는 결과값, GoTo는 목적지이다. Build로 완성한 표는 공유할 수 있고, 이전에 받아 둔 하위 빌더로도 변경할 수 없다. 표에 쓴 출발·목적지 ID는 모두 해당 부모의 자식이어야 한다.

한 자식에 여러 규칙이 있으면 다음 순서로 목적지를 찾는다.

1. 상세 상태가 정확히 일치하는 규칙에서 With의 결과값 일치를 먼저 찾고, 이어서 그 상태의 기본 목적지를 찾는다.
2. 찾지 못하면 When의 조건 함수를 등록 순서대로 평가한다. 참인 규칙에서도 결과값 일치가 기본 목적지보다 앞선다. 같은 대리자로 등록한 규칙들은 결과값별 목적지와 기본 목적지를 공유한다.
3. 같은 조건을 다시 등록하면 그 목적지를 교체한다. With(null)은 결과값 null과 일치하고, GoTo(null)은 부모의 명시적 완료를 뜻한다.

| 조회 결과 | 이번 전이기 갱신 |
| --- | --- |
| 적용할 진입 규칙 없음 | 자식을 시작하지 않고 Running을 반환한다. |
| 자식을 갱신했으나 적용할 후속 규칙 없음 | 그 자식의 상태와 출력을 부모에 전달한다. |
| 다른 자식 ID | 이번 갱신에서 전환하고 다음 자식을 갱신한다. |
| 현재 자식 ID | Running을 반환하고 다음 전이기 갱신에서 같은 자식을 다시 갱신한다. |
| 명시적 목적지 null | ChildCompleted와 현재 출력을 반환한다. |

DecisionMatrix.GetNext의 반환값은 규칙을 찾았는지를, out 인자는 목적지를 나타낸다. 출발 ID null은 진입 규칙 조회이다. 실패나 HaltedBySelf도 결정표의 입력이므로 복구 경로를 둘 수 있다. 자식에 StopOnFailure가 있어도 결정표 규칙을 평가한다.

### DirectTransitioner

호스트나 콜백이 SetNext로 다음 자식을 요청한다. 요청은 다음 **전이기 갱신**에서 적용하며, 적용 전에 여러 번 요청하면 마지막 값이 남는다. 자식 콜백에서 새 목적지를 요청한 경우에는 자식이 이미 종료했더라도 부모가 Running을 반환해 다음 갱신에서 요청을 처리한다.

선택한 자식도 요청도 없으면 Running이다. 자식이 선택되어 있고 새 요청이 없으면 그 자식을 갱신해 결과를 전달한다. SetNext(null)은 진행 중인 자식을 정리하고 부모를 ChildCompleted로 끝낸다. 초기화 전에 지정한 ID는 초기화할 때, 초기화가 끝난 뒤 요청한 ID는 요청할 때 확인한다.

## 조건으로 선택하는 전이

EvaluationTransitioner 계열은 자식의 등록 순서와 Work.CheckCondition을 이용한다. 자식이 Running을 반환하면 현재 자식을 기억하여 다음 갱신에서 이어 간다.

### Selector와 Sequence

| 상황 | Selector | Sequence |
| --- | --- | --- |
| 첫 선택에서 조건이 거짓 | 다음 후보를 평가한다. | 해당 회차가 실패한다. |
| 자식이 성공 | 해당 회차가 성공한다. | 출력을 다음 자식에게 전달한다. 마지막 자식까지 성공하면 회차가 성공한다. |
| 자식이 Failure 또는 ChildFailed | 그 자식에 StopOnFailure가 있으면 같은 갱신에서 다음 후보로 넘어간다. 없으면 Running을 반환하고 다음 평가를 첫 후보부터 시작한다. | 해당 회차가 실패한다. |
| 실행 중인 자식의 Self 조건이 거짓 | 자식을 HaltedBySelf로 정지하고 다음 후보를 평가한다. | 자식을 HaltedBySelf로 정지하고 회차가 실패한다. |

Selector가 모든 후보를 소진하면 회차 실패이다. StopOnFailure 없이 실패한 자식을 다시 평가하는 동안에는 회차가 아직 끝나지 않았다. 외부 정지는 StopOnFailure가 처리하는 실패에 해당하지 않는다.

Self와 LowerPriority는 자식에 설정하는 플래그이며 함께 쓸 수 있다. Self는 **현재 자식**의 조건을 다시 검사한다. LowerPriority는 **현재 자식보다 앞선 자식**이 자신의 조건을 다시 검사받도록 한다. 예를 들어 '회피, 순찰' 순서에서 회피에 LowerPriority를 주면, 순찰 중 회피 조건이 참이 된 갱신에서 순찰을 Preempted로 정지하고 회피로 전환한다.

### 회차 반복

| LoopType | 성공한 회차 | 실패한 회차 |
| --- | --- | --- |
| None | ChildCompleted로 끝난다. | ChildFailed로 끝난다. |
| Conditional | Running을 반환한다. | ChildFailed로 끝난다. |
| Forced | Running을 반환한다. | Running을 반환한다. |

반복하는 경우 다음 전이기 갱신에서 첫 자식부터 새 회차를 시작한다. LoopType 자체는 같은 프레임의 추가 갱신을 요청하지 않는다. 빈 Selector와 빈 Sequence는 성공한 회차로 처리하며 같은 반복 정책을 적용한다.

## 여러 자식을 진행하는 Parallel

Parallel은 각 갱신에서 미완료 자식을 등록 순서대로 동기 실행한다. 모든 자식이 같은 부모 입력을 받으므로 한 자식의 출력이 다음 자식의 입력을 덮어쓰지 않는다. 이미 끝난 자식은 이번 실행이 끝날 때까지 그 결과를 유지한다.

| ParallelCompletionPolicy | ChildCompleted | ChildFailed | 빈 목록 |
| --- | --- | --- | --- |
| AllSuccess(기본값) | 모든 자식이 성공 | 한 자식이라도 실패 | ChildCompleted |
| AnySuccess | 한 자식이라도 성공 | 모든 자식이 실패 | ChildFailed |

처음 실행하는 자식의 조건을 확인하고, 그 뒤에는 Self가 지정된 자식의 조건을 다시 확인한다. 조건 거부·Self 중단을 포함해 성공 이외의 자식 종료는 실패로 집계한다. LowerPriority·StopOnFailure·LoopType은 이 전이기의 정책에 포함되지 않는다.

완료 기준에 도달하면 남은 활성 자식을 HaltedByParent로 정지한다. 매 갱신의 Result는 등록 순서대로 각 자식의 OperationInfo를 담은 읽기 전용 스냅샷이다. 예를 들어 자식이 셋인 AnySuccess에서 첫 자식이 Running을 반환하고 둘째가 바로 성공하면, 첫째는 정지하고 셋째는 시작할 차례가 오지 않는다. 결과 목록에는 순서대로 HaltedByParent, Success, NeverRun이 남는다. 다음 갱신은 이미 반환한 목록을 바꾸지 않는다.

[USM](README.md) · [사용 예제](Usage.md)
