# USM 사용 예제

Node와 Work·Transitioner를 직접 조립한다. 실행 계약은 [설계 문서](Usm.md)를 따른다.

출력을 비교하며 실행하려면 [USM 콘솔 예시](../../../../samples/03-Services/StateMachines/Usm/README.md)를 사용한다.

## 작업 정의

```csharp
using BlackThunder.UniEngine.StateMachines.Usm;
using BlackThunder.UniEngine.StateMachines.Usm.Core;

public class AppendWork : Work
{
    private readonly string _suffix;

    public AppendWork(string suffix) => _suffix = suffix;

    public override OperationInfo Update(object input) =>
        new OperationInfo(DetailedOperationStatus.Success, input + _suffix);
}
```

## Sequence 구성

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
// ChildCompleted, Result and Payload: "input-a-b"
```

실행 중인 Sequence는 현재 자식 위치를 유지한다. 성공한 회차를 반복하려면 LoopType.Conditional을 생성자에 전달한다.

## Matrix 전이

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
// ChildCompleted, "job-prepared-finished"
```

자식이 Sequence·Selector·Parallel이면 성공 상세 상태는 ChildCompleted이다. 여러 상태를 같은 규칙으로 처리하려면 When에 조건 함수를 전달한다.

## Direct 전이와 Reframe

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
root.Update(new FrameContext());
// Select move, then update the root once more.

direct.SetNext(null);
root.Update(new FrameContext());
```

ReframeOption은 소비되는 요청이다. 작업 콜백에서 새 요청을 지정하면 추가 재실행할 수 있으며 전체 프레임 예산을 공유한다. CheckConditionThenUpdate는 직전 출력으로 조건을 검사한 뒤 재실행한다.

## Parallel 결과

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

각 자식은 같은 입력을 받는다. AnySuccess는 첫 성공 시 나머지 활성 자식을 정지한다. 결과 목록에는 미시작 자식의 NeverRun과 정지한 자식의 HaltedByParent도 포함된다.

## 실패 처리와 정지

Selector에서 실패한 자식 다음 후보를 평가하려면 그 자식에 `abortPolicies: AbortPolicies.StopOnFailure`를 지정한다. 정책이 없으면 다음 평가에서 첫 후보부터 선택한다. Self·LowerPriority는 비트 조합으로 지정할 수 있다.

`root.Stop(new FrameContext())`으로 정지하면 활성 자손이 먼저 종료된다. Work·Transitioner·Node를 다른 트리와 공유하지 않으며 초기화 실패 시 새 객체로 재구성한다.
