# USM 사용 예제

가장 작은 USM은 입력을 받아 결과 하나를 돌려주는 작업 노드이다. 여기에 전이기와 자식을 붙이면 같은 Update 호출로 순차 실행, 상태 전이, 우선순위 선택, 병렬 진행을 구성할 수 있다.

## 목차

- 작업 정의
- 순차 실행과 결과 읽기
- 결정표로 상태 전환하기
- 실패한 후보에서 대체 행동으로 넘어가기
- 외부 전환과 같은 프레임의 재실행
- 병렬 결과 읽기

## 작업 정의

아래 AppendWork를 별도 파일에 둔다. Update 한 번으로 성공하고 입력 뒤에 문자열을 붙여 다음 작업에 넘기는 Work이다.

```csharp
using BlackThunder.UniEngine.StateMachines.Usm;

public class AppendWork : Work
{
    private readonly string _suffix;

    public AppendWork(string suffix) => _suffix = suffix;

    public override OperationInfo Update(object input) =>
        new OperationInfo(DetailedOperationStatus.Success, input + _suffix);
}
```

이후 각 예제는 독립적인 트리이다. 실행 코드를 작성하는 파일 상단에는 다음 두 네임스페이스를 둔다.

```csharp
using BlackThunder.UniEngine.StateMachines.Usm;
using BlackThunder.UniEngine.StateMachines.Usm.Core;
```

## 순차 실행과 결과 읽기

```csharp
var root = new Node(
    "root",
    transitioner: new Sequence(),
    children: new[]
    {
        new Node("first", new AppendWork("-a")),
        new Node("second", new AppendWork("-b"))
    });

root.Initialize();
var frame = new FrameContext("input");
var result = root.Update(frame);
```

| 조회 | 위 갱신에서 확인할 값 |
| --- | --- |
| result.DetailedOperationStatus | ChildCompleted. 두 자식이 모두 성공해 Sequence 한 회차가 끝났다. |
| result.Result와 frame.Payload | 둘 다 'input-a-b'. first가 돌려준 'input-a'가 second의 입력이 되었다. |
| frame.Nexts | first, second 순서의 두 컨텍스트. 각 자식의 Reports에서 작업 수명을 확인할 수 있다. |
| frame.UpdateCount | 루트 한 번과 자식 두 번을 합한 3이다. |

실행 중인 자식이 있으면 Sequence는 그 위치를 기억한다. 성공한 회차를 다음 전이기 갱신에서 반복하려면 `new Sequence(LoopType.Conditional)`로 구성한다.

## 결정표로 상태 전환하기

```csharp
var matrix = new DecisionMatrixBuilder()
    .FromEntry().GoTo("prepare")
    .From("prepare").When(DetailedOperationStatus.Success).GoTo("finish")
    .From("finish").When(DetailedOperationStatus.Success).GoTo(null)
    .Build();

var machine = new Node(
    "machine",
    transitioner: new MatrixTransitioner(matrix),
    children: new[]
    {
        new Node("prepare", new AppendWork("-prepared")),
        new Node("finish", new AppendWork("-finished"))
    });

machine.Initialize();
var result = machine.Update(new FrameContext("job"));
```

prepare와 finish가 한 번의 외부 갱신에서 실행되고, GoTo(null)에서 부모가 ChildCompleted와 'job-prepared-finished'를 반환한다. 규칙의 When은 **자식의 상세 상태**를 검사한다. 예를 들어 prepare의 내부를 Sequence로 교체했다면 정상 완료 조건도 Success에서 ChildCompleted로 바꾼다. 두 상태를 같은 규칙에서 처리할 때는 When에 조건 함수를 전달한다.

## 실패한 후보에서 대체 행동으로 넘어가기

문을 열 수 있으면 통과하고, 잠겼으면 우회로를 택하는 Selector이다. 다음 작업 형식을 AppendWork와 같은 네임스페이스에 정의한다.

```csharp
using BlackThunder.UniEngine.StateMachines.Usm;

public class OpenDoorWork : Work
{
    public override OperationInfo Update(object input)
    {
        if (Equals(input, "locked"))
            return new OperationInfo(DetailedOperationStatus.Failure, input);

        return new OperationInfo(DetailedOperationStatus.Success, "door open");
    }
}
```

```csharp
var route = new Node(
    "route",
    transitioner: new Selector(),
    children: new[]
    {
        new Node("door", new OpenDoorWork(), abortPolicies: AbortPolicies.StopOnFailure),
        new Node("detour", new AppendWork("-detour"))
    });

route.Initialize();
var result = route.Update(new FrameContext("locked"));
```

door가 Failure를 반환하면 그 자식의 StopOnFailure 정책에 따라 detour가 즉시 실행된다. 부모 결과는 ChildCompleted와 'locked-detour'이다. door에서 정책을 생략하면 이 호출의 결과는 Running이고, 다음 평가에서 다시 door부터 시도한다.

## 외부 전환과 같은 프레임의 재실행

```csharp
var direct = new DirectTransitioner();
var root = new Node(
    "root",
    transitioner: direct,
    children: new[] { new Node("idle"), new Node("move") });

root.Initialize();
direct.SetNext("idle");
root.Update(new FrameContext());

direct.SetNext("move");
root.ReframeOption = ReframeOption.Update;
var frame = new FrameContext();
var moving = root.Update(frame);

direct.SetNext(null);
var done = root.Update(new FrameContext());
```

idle과 move는 기본 Work를 사용하므로 선택되면 Running을 유지한다. 두 번째 외부 Update에서는 idle을 Preempted로 정지하고 move를 시작한다. Reframe이 루트와 move를 한 번 더 갱신하므로 moving은 Running이고 frame.UpdateCount는 4이다. 마지막 SetNext(null)은 다음 갱신에서 부모를 ChildCompleted로 끝낸다.

호스트가 활성 트리를 외부 사유로 중단할 때는 `root.Stop(new FrameContext())`을 호출한다. 활성 자손은 HaltedByParent, 루트는 기본값 HaltedByExternal로 정지한다. 같은 프레임에서 조건을 다시 검사하는 방법과 재실행 예산은 [USM 실행](Execution.md)에 있다.

## 병렬 결과 읽기

```csharp
var root = new Node(
    "parallel",
    transitioner: new BlackThunder.UniEngine.StateMachines.Usm.Parallel(
        ParallelCompletionPolicy.AllSuccess),
    children: new[]
    {
        new Node("left", new AppendWork("-left")),
        new Node("right", new AppendWork("-right"))
    });

root.Initialize();
var result = root.Update(new FrameContext("input"));
if (result.TryGetResult<System.Collections.Generic.IReadOnlyList<OperationInfo>>(out var children))
{
    var left = children[0];   // Success, "input-left"
    var right = children[1];  // Success, "input-right"
}
```

각 자식은 같은 'input'을 받고 부모는 두 자식이 모두 성공했을 때 ChildCompleted를 반환한다. AnySuccess로 바꾸면 첫 성공에서 종료하므로 이 예제의 right는 NeverRun으로 남는다. 스냅샷에는 조기 완료 때문에 정지된 자식의 상태도 남는다.

같은 정책을 여러 갱신에 걸쳐 진행하며 콘솔 출력으로 비교하려면 저장소 루트에서 다음 명령을 실행한다.

```powershell
dotnet run --project samples/UniEngine.Samples.csproj -- usm
```

[USM 콘솔 예시와 기대 출력](../../../../../samples/03-Services/StateMachines/Usm/README.md) · [USM 전이 정책](Transitions.md) · [USM](README.md)
