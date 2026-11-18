# StateMachines.Usm 예시

Node·Work·Transitioner를 직접 조립하여 FSM·BT·재실행·병렬 진행을 비교하는 실행 예시이다.

## 실행

저장소 최상위에서 한 번 실행한다.

```powershell
dotnet run --project samples/UniEngine.Samples.csproj -- usm
```

대화형 프롬프트에서도 같은 명령을 사용할 수 있다.

```powershell
dotnet run --project samples/UniEngine.Samples.csproj
```

```text
sample> usm
sample> exit
```

all 명령에도 포함된다. 별도 인자는 받지 않는다.

## 내용

| 구간 | 보여 주는 동작 |
| --- | --- |
| Matrix + Sequence | Sequence의 ChildCompleted 결과와 Payload로 다음 자식을 결정하고 같은 갱신에서 완료한다. |
| Selector + StopOnFailure | 정책이 없으면 다음 평가에서 첫 후보를 다시 선택하고, 있으면 실패 직후 다음 후보를 실행한다. |
| Direct + child-first Stop | SetNext로 실행 중인 자식을 전환하고 자식부터 부모 순서로 정지한다. |
| LoopType + Reframe | Conditional은 다음 전이기 갱신에서 반복하고, Reframe 요청은 같은 프레임에서 소비·재등록한다. |
| Parallel | AllSuccess·AnySuccess의 완료 기준과 자식별 상태·결과 스냅샷을 비교한다. |

각 트리는 자기 Work·Transitioner·자식 인스턴스를 소유한다. 예시마다 루트를 한 번 초기화하고 새 FrameContext로 갱신한다. 실행 후에도 Running인 Selector·반복 트리는 명시적으로 정지한다.

CountingWork는 시작 횟수와 누적 갱신 횟수를 보여 주는 작업이다. 지정한 횟수에 도달하면 성공 또는 실패한다. 작업을 확장하면서 기본 Work의 콜백도 사용하려고 Start·Update에서 base 메서드를 호출한다.

## 출력

```text
[Matrix + Sequence]
Result: ChildCompleted, payload: input-a-b-end
Visited: prepare -> finish
[Selector + StopOnFailure]
None: Running, attempt updates: 1, fallback updates: 0
Next update: attempt updates: 2, fallback updates: 0
StopOnFailure: ChildCompleted, attempt updates: 1, fallback updates: 1
[Direct + child-first Stop]
idle started
idle stopped: Preempted
move started
move stopped: HaltedByParent
root stopped: HaltedByExternal
[LoopType + Reframe]
Conditional tick 1: Running, step updates: 1, frame updates: 2
Conditional tick 2: Running, step updates: 2, frame updates: 2
Reframe: Success, starts: 1, work updates: 3, frame updates: 3, pending: None
[Parallel]
AllSuccess tick 1: Running [Running(input:slow), Success(input:fast)]
AllSuccess tick 2: ChildCompleted [Success(input:slow), Success(input:fast)]
Saved tick 1 snapshot: Running [Running(input:slow), Success(input:fast)]
Work updates: slow=2, fast=1
AnySuccess: ChildCompleted [HaltedByParent(input:slow), Success(input:fast), NeverRun(null)]
```

Parallel의 두 자식은 같은 input을 받는다. 완료된 fast는 두 번째 갱신에서 실행하지 않는다. 보관한 첫 번째 결과 목록은 두 번째 갱신 후에도 바뀌지 않는다. AnySuccess에서는 느린 자식이 정지하고 뒤의 자식은 시작하지 않는다.

결과 파일은 생성하지 않는다. 공통 SampleEnvironment 초기화에 따른 Configuration 경로는 [예시 목록](../../../README.md)의 실행 환경·출력 절을 따른다. USM 자체에는 Bootstrapper·EventStream 의존이 없다.

## 관련 문서

- [USM 설계](../../../../docs/02.%20UniEngine/03-Services/StateMachines/Usm/README.md)
- [USM 사용 예제](../../../../docs/02.%20UniEngine/03-Services/StateMachines/Usm/Usage.md)
- [예시 코드](UsmSample.cs)
- [예시 목록](../../../README.md)

