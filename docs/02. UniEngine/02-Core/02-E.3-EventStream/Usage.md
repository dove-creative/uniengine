# EventStream 사용 예제

구독과 해제, 필터 조합, 여러 입력의 병합, 순서 그룹 호출, 대상 라우팅을 현재 EventStream API로 구성하는 예제이다.

각 C# 블록은 독립된 콘솔 프로그램이다. Golden Hind를 참조하는 프로젝트에서 하나씩 실행하며, 문법은 C# 9를 기준으로 한다. 모든 출력은 동기 호출 순서를 그대로 보여준다. 설계와 공통 수명 규칙은 [모듈 개요](README.md)를 따른다.

## 목차

1. 구독과 해제
2. 필터 조합
3. 여러 입력의 병합
4. 순서 그룹 호출
5. 대상 라우팅

## 구독과 해제

Subscribe가 반환한 핸들을 폐기하면 그 구독만 해제된다. 기본 EventStream의 종료 정책은 Complete이므로, 마지막 구독을 해제해도 스트림은 남고 다시 구독할 수 있다. 스트림 자체를 Dispose하면 남은 구독이 정리되지만 완료 콜백은 호출되지 않는다.

```csharp
using System;
using System.Collections.Generic;
using Blackthunder.UniEngine.Core;

public static class Program
{
    public static void Main()
    {
        using var stream = new EventStream<int>();
        var values = new List<int>();
        using var subscription = stream.Subscribe(values.Add);

        stream.OnNext(1);
        subscription.Dispose();
        stream.OnNext(2);
        Console.WriteLine($"After unsubscribe: disposed={stream.IsDisposed}");

        var completed = false;
        using var next = stream.Subscribe(values.Add, () => completed = true, null);
        stream.OnNext(3);
        stream.Dispose();

        Console.WriteLine($"Values: {string.Join(", ", values)}");
        Console.WriteLine($"After dispose: completed={completed}, disposed={stream.IsDisposed}");
    }
}
```

값 2는 첫 구독을 해제한 뒤 발행했으므로 전달되지 않는다.

```text
After unsubscribe: disposed=False
Values: 1, 3
After dispose: completed=False, disposed=True
```

같은 콜백을 여러 번 구독해도 각 핸들은 자기 구독만 해제한다. 완료를 알리면서 끝내려면 Dispose 대신 OnCompleted를 호출한다. 통지와 폐기의 차이는 [EventStream](EventStream.md)과 [TerminationMode](TerminationMode.md)에서 설명한다.

## 필터 조합

Where와 Take는 각각 새 하위 스트림을 반환한다. 아래 경로는 짝수만 고른 뒤 그중 두 개를 전달하고 완료한다. 원본에 따로 등록한 구독은 모든 입력을 계속 받는다.

```csharp
using System;
using System.Collections.Generic;
using Blackthunder.UniEngine.Core;

public static class Program
{
    public static void Main()
    {
        using var source = new EventStream<int>();
        using var filtered = source.Where(value => value % 2 == 0);
        using var firstTwo = filtered.Take(2);
        var originalValues = new List<int>();
        var selectedValues = new List<int>();
        var completed = false;

        using var original = source.Subscribe(originalValues.Add);
        using var selected = firstTwo.Subscribe(selectedValues.Add, () => completed = true, null);
        for (var value = 1; value <= 5; value++) source.OnNext(value);

        Console.WriteLine($"Original: {string.Join(", ", originalValues)}");
        Console.WriteLine($"Selected: {string.Join(", ", selectedValues)}");
        Console.WriteLine($"Completed: {completed}");
        Console.WriteLine($"Disposed: take={firstTwo.IsDisposed}, where={filtered.IsDisposed}, source={source.IsDisposed}");
    }
}
```

```text
Original: 1, 2, 3, 4, 5
Selected: 2, 4
Completed: True
Disposed: take=True, where=True, source=False
```

Take가 값 4를 전달하면서 완료하고 폐기되면, Where에 대한 구독도 해제된다. Where는 기본 정책이 NoSubscribers이고 다른 구독이 없으므로 함께 폐기되어 소스와의 연결을 끊는다. 소스는 Complete이므로 남고, 원본 구독도 그대로 값 5를 받는다.

조건 연산자는 값을 받는 조건 대신 `() => isReady`처럼 외부 상태만 보는 조건도 받는다. SkipWhile은 조건이 처음 false가 된 뒤 검사를 멈추고, Where는 매 입력을 검사하며, TakeWhile은 처음 false인 값에서 완료한다. 자세한 차이는 [EventStream 연산자](EventStreamOperators.md)에서 설명한다.

## 여러 입력의 병합

Merge는 여러 상위의 값을 하나의 구독 경로로 전달한다. All은 모든 상위가 완료될 때까지 기다리고, Any는 첫 상위가 완료되면 끝난다. 아래 예제는 같은 두 소스에 두 정책을 각각 연결한다.

```csharp
using System;
using System.Collections.Generic;
using Blackthunder.UniEngine.Core;
using Blackthunder.UniEngine.Core.EventStreamCore;

public static class Program
{
    public static void Main()
    {
        using var left = new EventStream<int>();
        using var right = new EventStream<int>();
        using var all = new Merge<int>(Merge<int>.CompletionMode.All, left, right);
        using var any = new Merge<int>(Merge<int>.CompletionMode.Any, left, right);
        var allEvents = new List<string>();
        var anyEvents = new List<string>();

        using var allSubscription = all.Subscribe(value => allEvents.Add(value.ToString()), () => allEvents.Add("done"), null);
        using var anySubscription = any.Subscribe(value => anyEvents.Add(value.ToString()), () => anyEvents.Add("done"), null);

        left.OnNext(1);
        right.OnNext(2);
        left.OnCompleted();
        Console.WriteLine($"After left completed: all={all.IsDisposed}, any={any.IsDisposed}");

        right.OnNext(3);
        right.OnCompleted();
        Console.WriteLine($"All: {string.Join(", ", allEvents)}");
        Console.WriteLine($"Any: {string.Join(", ", anyEvents)}");
    }
}
```

```text
After left completed: all=False, any=True
All: 1, 2, 3, done
Any: 1, 2, done
```

Any가 끝나면 아직 완료하지 않은 right와의 연결도 해제한다. 그러나 right 자체를 폐기하지는 않으므로 right는 All에 계속 값을 전달한다. 어느 입력에서든 OnError가 오면 완료 정책과 관계없이 오류를 알리고 종료한다.

All이면 충분한 경우에는 EventStreamExtensions.Merge(left, right)나 left.Merge(right)로 간단히 만들 수 있다. 둘 다 새 하위 스트림을 반환한다.

## 순서 그룹 호출

구독한 순서와 실행 단계의 순서를 분리한다. 아래 예제는 PostUpdate를 가장 먼저 구독해도 PreUpdate → Update → PostUpdate 순서로 실행한다. 순서 인자를 생략한 구독은 기본 그룹인 Update에 들어간다.

```csharp
using System;
using System.Collections.Generic;
using Blackthunder.UniEngine.Core;

public static class Program
{
    private enum UpdateOrder
    {
        PreUpdate,
        Update,
        PostUpdate,
    }

    public static void Main()
    {
        using var stream = new OrderedEventStream<int, UpdateOrder>(
            UpdateOrder.Update, new[] { UpdateOrder.PreUpdate, UpdateOrder.Update, UpdateOrder.PostUpdate });
        var calls = new List<string>();
        using var post = stream.Subscribe(UpdateOrder.PostUpdate, _ => calls.Add("PostUpdate"));
        using var first = stream.Subscribe(_ => calls.Add("Update 1"));
        using var pre = stream.Subscribe(UpdateOrder.PreUpdate, _ => calls.Add("PreUpdate"));
        using var second = stream.Subscribe(_ => calls.Add("Update 2"));

        stream.OnNext(0);
        Console.WriteLine(string.Join(", ", calls));

        first.Dispose();
        calls.Clear();
        stream.OnNext(0);
        Console.WriteLine(string.Join(", ", calls));
    }
}
```

```text
PreUpdate, Update 1, Update 2, PostUpdate
PreUpdate, Update 2, PostUpdate
```

순서 목록을 생략하면 Enum.GetValues의 결과, 즉 값의 크기순을 사용한다. 이 예제처럼 값이 0, 1, 2로 매겨져 있으면 목록을 생략해도 같은 순서가 된다. 전파 중 새 구독을 추가하면 아직 실행하지 않은 뒤쪽 그룹이어도 다음 전파부터 참여한다. 자세한 규칙은 [OrderedEventStream](OrderedEventStream.md)에서 설명한다.

## 대상 라우팅

EventRouter는 값을 비트 마스크로 바꾸어, 구독 마스크와 겹치는 수신자에게만 전달한다. 아래 예제는 바뀐 항목을 [Flags] 열거형으로 알리고, 각 화면 요소가 자신과 관련된 변경만 받게 한다. 대상 없이 구독한 Log는 모든 값을 받는다.

```csharp
using System;
using Blackthunder.UniEngine.Core;

public static class Program
{
    [Flags]
    private enum Changes
    {
        Health = 1,
        Mana = 2,
        Inventory = 4,
    }

    public static void Main()
    {
        using var router = new EventRouter<Changes>(changes => (uint)changes);
        using var statusBar = router.Subscribe(
            new[] { Changes.Health, Changes.Mana }, changes => Console.WriteLine($"StatusBar: {changes}"));
        using var inventoryPanel = router.Subscribe(
            Changes.Inventory, changes => Console.WriteLine($"InventoryPanel: {changes}"));
        using var log = router.Subscribe(changes => Console.WriteLine($"Log: {changes}"));

        router.OnNext(Changes.Health);
        router.OnNext(Changes.Inventory);
        router.OnNext(Changes.Health | Changes.Inventory);
    }
}
```

```text
StatusBar: Health
Log: Health
InventoryPanel: Inventory
Log: Inventory
StatusBar: Health, Inventory
InventoryPanel: Health, Inventory
Log: Health, Inventory
```

StatusBar의 마스크는 Health와 Mana를 합친 3이다. Inventory(4)와는 겹치는 비트가 없으므로 두 번째 값은 받지 않고, Health와 Inventory를 합친 세 번째 값(5)은 Health 비트가 겹치므로 받는다. 마스크 판정 규칙은 [EventRouter](EventRouter.md)에서 설명한다.

[모듈 개요](README.md)
