# EventHub 사용 예제

객체 연결과 폐기 알림, 비트 조건, 조건별 등록과 재사용, 직접 구독, 수면과 깨우기, 여러 허브의 일괄 수면을 현재 EventHub API로 구성하는 예제이다.

각 C# 블록은 독립된 콘솔 프로그램이다. UniEngine을 참조하는 프로젝트에서 하나씩 실행하며, 문법은 C# 9를 기준으로 한다. 모든 출력은 동기 호출 순서를 그대로 보여준다. 설계와 공통 수명 규칙은 [모듈 구성](README.md)을 따른다.

## 목차

- 객체 연결과 폐기 알림
- 비트 조건으로 연결하기
- 조건별 등록과 재사용
- null 인자 구분
- 직접 구독
- 수면과 깨우기
- 여러 허브 함께 재우기

## 객체 연결과 폐기 알림

도메인 객체는 허브를 소유하고 IEventSubject로 노출한다. 수신자는 자신을 키로 Connect한 뒤 반환된 Builder로 콜백을 이어 등록한다. 아래 예제에서 HealthBar는 모든 체력 변화와 체력이 0이 된 순간을 각각 받고, Unit이 폐기되면 보관한 참조를 스스로 비운다.

```csharp
using System;
using Blackthunder.UniEngine.Core;

public static class Program
{
    private class Unit : IEventSubject<int>
    {
        public bool IsDisposed => _hub.IsDisposed;
        public IEventHub<int> EventHub => _hub;

        private readonly EventHub<int> _hub;
        private int _health;

        public Unit(int health)
        {
            _health = health;
            _hub = new EventHub<int>(this);
        }

        public void Damage(int amount)
        {
            _health = Math.Max(0, _health - amount);
            _hub.Update(_health);
        }

        public void Dispose() => _hub.Dispose();
    }

    private class HealthBar
    {
        public bool HasTarget => _target != null;

        private Unit _target;

        public void Watch(Unit unit)
        {
            _target = unit;
            unit.EventHub.Connect(this)
                .OnUpdated(health => Console.WriteLine($"HealthBar: {health}"))
                .OnUpdatedWhen(0, () => Console.WriteLine("HealthBar: defeated"))
                .OnDisposed(() => _target = null);
        }
    }

    public static void Main()
    {
        var unit = new Unit(10);
        var healthBar = new HealthBar();
        var log = new object();
        healthBar.Watch(unit);
        unit.EventHub.Connect(log).OnUpdated(health => Console.WriteLine($"Log: {health}"));

        unit.Damage(4);
        unit.EventHub.Disconnect(log);
        unit.Damage(7);
        unit.Dispose();

        Console.WriteLine($"HealthBar has target: {healthBar.HasTarget}");
    }
}
```

log 연결은 첫 피해 뒤 Disconnect했으므로 두 번째 갱신을 받지 않는다. 연결은 수신자 객체 단위이므로 Disconnect 한 번으로 그 수신자의 콜백이 모두 해제된다.

```text
HealthBar: 6
Log: 6
HealthBar: 0
HealthBar: defeated
HealthBar has target: False
```

OnUpdatedWhen(0, callback)은 값이 0과 같을 때만 콜백을 실행한다. 조건 대리자를 쓰려면 OnUpdatedWhere(health => health <= 0, callback)처럼 조건과 콜백을 함께 넘긴다. 값을 쓰지 않는 콜백은 인자 없는 Action으로 등록한다. 모든 등록은 같은 Builder를 반환하고 각 등록의 조건은 다음 등록에 영향을 주지 않는다. Unit을 폐기하면 조건과 관계없이 HealthBar의 OnDisposed가 실행되어 참조를 비운다. 폐기 알림의 단계는 [EventHub](EventHub.md), 콜백 구성 규칙은 [EventHubBuilder](EventHubBuilder.md)에서 설명한다.

## 비트 조건으로 연결하기

여러 변화 종류를 비트로 발행하는 EventHub&lt;ulong&gt;에서는 OnUpdatedForFlags에 마스크와 콜백을 함께 넘긴다. 마스크에 지정한 비트 중 하나라도 포함한 갱신을 받으며, 값 사용 콜백에는 마스크로 자르지 않은 원래 ulong 값이 전달된다.

```csharp
using System;
using Blackthunder.UniEngine.Core;

public static class Program
{
    [Flags]
    private enum ItemChanges
    {
        Count = 1,
        Selection = 2
    }

    public static void Main()
    {
        using var hub = new EventHub<ulong>("Inventory");
        IEventHub<ulong> events = hub;
        var listener = new object();

        events.Connect(listener)
            .OnUpdatedForFlags(ItemChanges.Count | ItemChanges.Selection,
                value => Console.WriteLine($"Changed bits: {value}"))
            .OnDisposed(() => Console.WriteLine("Inventory disposed"));

        hub.Update(ItemChanges.Count.ToBits());
        hub.Update((ItemChanges.Count | ItemChanges.Selection).ToBits());
        hub.Update(0);
    }
}
```

```text
Changed bits: 1
Changed bits: 3
Inventory disposed
```

값 0은 어떤 비트 조건도 통과하지 않는다. 폐기 알림은 마스크 조건과 관계없이 실행된다. Disconnect(listener)는 같은 연결에서 구성한 모든 갱신과 생애주기 콜백을 함께 해제한다.

OnUpdatedForFlags도 같은 Builder를 반환하므로 뒤에 OnUpdated를 붙여 별도의 루트 등록을 계속할 수 있다. 뒤의 OnUpdated는 앞의 마스크와 관계없이 모든 갱신을 받는다. 비트 판정과 다른 조건을 함께 검사하려면 OnUpdatedWhere의 조건 함수 안에서 조합한다. 같은 부모 위치의 마스크 비트가 같으면 리스너나 enum 형식이 달라도 경로를 공유한다. 서로 다른 분류를 구분할 비트는 호출자가 배정한다.

마스크는 `[Flags]` enum이어야 하며 비트가 0이면 OnUpdatedForFlags 호출 시 ArgumentException이다. enum의 기반 형식은 자유롭다. Update에 넘길 값은 ToBits로 변환하면 부호 비트를 포함한 선언 비트 폭을 보존한다. 형식 없는 IEventHubBuilder에는 OnUpdatedForFlags를 제공하지 않으므로 `IEventHub<ulong>` 계약을 유지한다.

OnUpdatedWhen(ItemChanges.Count.ToBits(), callback)은 값 전체가 Count 비트 값과 같을 때만 실행한다. Count와 Selection이 함께 들어온 값 3은 OnUpdatedForFlags(ItemChanges.Count, callback)을 통과하지만 Count의 값 1을 target으로 등록한 OnUpdatedWhen은 통과하지 않는다.

## 조건별 등록과 재사용

조건 함수는 OnUpdatedWhere, 값 전체의 동등성은 OnUpdatedWhen으로 구분한다. 공통 조건 함수는 대리자를 보관하여 재사용할 수 있고, 같은 target은 리스너가 달라도 같은 비교 경로를 공유한다.

```csharp
using System;
using Blackthunder.UniEngine.Core;

public static class Program
{
    private static readonly Func<int, bool> Positive = value => value > 0;

    public static void Main()
    {
        using var hub = new EventHub<int>("Selection");
        hub.Connect(new object())
            .OnUpdatedWhere(Positive, value => Console.WriteLine($"Positive: {value}"))
            .OnUpdatedWhen(3, () => Console.WriteLine("Selected: 3"));
        hub.Connect(new object()).OnUpdatedWhen(3, value => Console.WriteLine($"Mirror: {value}"));

        hub.Update(-1);
        hub.Update(2);
        hub.Update(3);
    }
}
```

```text
Positive: 2
Positive: 3
Selected: 3
Mirror: 3
```

Positive 조건과 target 3은 독립된 등록이다. OnUpdatedWhen은 앞의 OnUpdatedWhere 조건을 이어받지 않는다. target 3을 등록한 두 리스너는 같은 WhenNode의 행을 공유한다. 같은 연결에서 동일한 콜백을 같은 종류·값의 조건으로 다시 등록하면 InvalidOperationException이다. 조건 함수의 동등성은 원본 대리자의 Equals로 판정하므로, 본문이 같다는 이유만으로 같은 조건으로 취급하지 않는다.

여러 판정을 함께 수행하려면 `OnUpdatedWhere(value => value > 0 && value % 2 == 0, callback)`처럼 하나의 함수로 표현한다. 이 표현은 하나의 Where 조건이다. 단계별 공통 경로 공유나 Debug·ForOrder가 필요한 구성은 [EventStream 연산자](../02-E.3-EventStream/EventStreamOperators.md)를 사용한다.

참조 형식과 Nullable&lt;T&gt;의 null target도 OnUpdatedWhen으로 비교할 수 있다. target 객체의 Equals·GetHashCode 결과는 등록 뒤 바꾸지 않아야 한다. ulong target은 전체 값의 동등성을 비교하므로 OnUpdatedForFlags의 공통 비트 판정과 다르다. When의 성능은 측정하지 않았고 Unity Mono·IL2CPP에서의 할당도 검증하지 않았다.

## null 인자 구분

참조 형식의 null target은 유효하다. 예를 들어 문자열 허브에서 `builder.OnUpdatedWhen(null, callback)`은 null 갱신만 받는다. `builder.OnUpdatedWhere(null, callback)`은 null 조건 대리자이므로 ArgumentNullException이다. 메서드 이름이 다르므로 target과 조건을 구분하기 위한 형변환은 필요하지 않다. 콜백 자체에 null을 넘길 때는 Action과 Action&lt;T&gt; 중 원하는 형식을 명시한다.

Builder는 연결 수명을 먼저 검사하고 조건 인자를 검사한 다음 null 콜백을 무시한다. 잘못된 조건의 등록 시도는 기존 연결과 등록을 유지한다. null 콜백을 넘기더라도 null 조건 대리자와 유효하지 않은 flags 마스크는 거부한다.

## 직접 구독

수신자 객체 없이 콜백만 등록하려면 Updated·Disposing·Disposed 이벤트를 직접 구독한다. 직접 구독은 원본 대리자를 키로 하는 연결이며 연결 수에 포함한다. 해제할 때는 등록한 대리자를 그대로 넘긴다.

Updated·Disposing·Disposed는 콜백을 하나씩 등록하는 방식을 기본으로 한다. 같은 이벤트의 동일한 등록은 InvalidOperationException으로 거부한다. 멀티캐스트를 한 번에 등록했다면 같은 묶음으로 해제한다. -=는 해당 이벤트만, Disconnect(callback)는 같은 키의 모든 등록을 해제한다. typed/object Updated는 별도로 등록·해제한다.

```csharp
using System;
using Blackthunder.UniEngine.Core;

public static class Program
{
    public static void Main()
    {
        var door = new EventHub<string>("Door");
        var listener = new object();
        Action<string> log = state => Console.WriteLine($"Log: {state}");

        door.Updated += log;
        door.Connect(listener).OnUpdated(state => Console.WriteLine($"Listener: {state}"));
        door.Disposed += () => Console.WriteLine("Log: door disposed");
        Console.WriteLine($"Connections: {door.ConnectionCount}");

        door.Update("opened");
        door.Updated -= log;
        door.Update("closed");
        door.Dispose();
    }
}
```

log를 먼저 구독했으므로 Log가 먼저 호출된다. 직접 Updated와 OnUpdated는 같은 루트에 등록되어 등록 순서대로 섞여 호출된다. 조건의 자식 노드는 루트 구독을 모두 호출한 뒤 노드 생성 순서대로 방문한다. 같은 위치의 Where 조건들은 WhereNode 하나의 행으로 모여 그 노드의 차례에 등록 순서대로 방문한다.

```text
Connections: 3
Log: opened
Listener: opened
Listener: closed
Log: door disposed
```

직접 구독은 수신자 객체를 따로 만들지 않고 대리자를 연결 키로 사용하는 경로이다. 마지막 등록을 해제하면 그 연결도 사라지며, 해제 시점은 구독한 쪽이 관리한다. 여러 콜백을 한 번에 해제하거나 폐기 단계별 반응을 함께 구성하려면 Connect를 사용한다.

## 수면과 깨우기

Sleep은 반환한 Token이 폐기될 때까지 Update를 생략한다. 생략한 갱신은 보류하지 않으므로, 깨어난 뒤 한 번 발행할 갱신을 onAwake로 예약한다. 아래 예제의 AddItems는 항목마다 Update를 호출하지만 수면 범위 안에서 호출하므로, 범위가 끝날 때 한 번만 알린다.

```csharp
using System;
using System.Collections.Generic;
using Blackthunder.UniEngine.Core;

public static class Program
{
    public static void Main()
    {
        using var inventory = new EventHub<int>("Inventory");
        var items = new List<string>();
        inventory.Updated += count => Console.WriteLine($"Updated: {count} items");

        AddItems(inventory, items, "Sword", "Shield");

        using (inventory.Sleep(() => inventory.Update(items.Count)))
        {
            AddItems(inventory, items, "Potion");
            AddItems(inventory, items, "Arrow", "Bow");
            Console.WriteLine($"Awake inside batch: {inventory.IsAwake}");
        }

        try
        {
            using (inventory.Sleep())
            {
                inventory.Sleep();
            }
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine($"Nested Sleep rejected, awake: {inventory.IsAwake}");
        }
    }

    private static void AddItems(EventHub<int> inventory, List<string> items, params string[] added)
    {
        using (inventory.TrySleep(() => inventory.Update(items.Count)))
        {
            foreach (var item in added)
            {
                items.Add(item);
                inventory.Update(items.Count);
            }
        }
    }
}
```

AddItems는 TrySleep을 사용하므로 허브가 이미 수면 중인지 알 필요가 없다. 혼자 호출하면 허브를 직접 재우고 깨운다. 바깥에서 이미 재운 경우에는 수면 상태를 바꾸지 않으며, 범위가 끝날 때 실행하는 onAwake의 Update도 허브가 아직 수면 중이므로 생략된다. 따라서 갱신은 가장 바깥 범위의 onAwake가 한 번 발행한다.

```text
Updated: 2 items
Awake inside batch: False
Updated: 5 items
Nested Sleep rejected, awake: True
```

이미 수면 중인 허브에 Sleep을 다시 호출하면 거부한다. 예외가 using 범위를 빠져나가면서 첫 Token이 폐기되므로 허브는 다시 깨어난다. 중첩 수면을 거부하는 이유는 [EventHub](EventHub.md)에서 설명한다.

## 여러 허브 함께 재우기

외부에서 만든 Token을 Sleep에 넘기면 여러 허브를 같은 Token에 묶을 수 있다. 아래 예제는 체력과 마나를 함께 바꾸는 동안 두 허브를 재우고, Token 하나를 폐기하여 함께 깨운다.

```csharp
using System;
using Blackthunder.UniEngine.Core;

public static class Program
{
    public static void Main()
    {
        using var health = new EventHub<int>("Health");
        using var mana = new EventHub<int>("Mana");
        var healthValue = 10;
        var manaValue = 5;
        health.Updated += value => Console.WriteLine($"Health: {value}");
        mana.Updated += value => Console.WriteLine($"Mana: {value}");

        using (var token = new Token())
        {
            health.Sleep(token, () => health.Update(healthValue));
            mana.Sleep(token, () => mana.Update(manaValue));

            healthValue -= 3;
            health.Update(healthValue);
            manaValue -= 2;
            mana.Update(manaValue);
            healthValue += 1;
            health.Update(healthValue);
            Console.WriteLine($"During batch: health={health.IsAwake}, mana={mana.IsAwake}");
        }

        Console.WriteLine($"After batch: health={health.IsAwake}, mana={mana.IsAwake}");
    }
}
```

각 허브는 깨어날 때 onAwake로 마지막 값을 한 번 발행한다. 수면 중 갱신한 중간 값 7은 발행하지 않는다.

```text
During batch: health=False, mana=False
Health: 8
Mana: 3
After batch: health=True, mana=True
```

Sleep(Token)은 받은 Token을 그대로 반환하므로, 어느 허브의 반환값을 폐기해도 같은 Token에 묶인 허브가 모두 깨어난다. 이미 폐기된 Token은 연결할 수 없다. Token의 수명 규칙은 [Token](../02-E.1-HandleToken/Token.md)에서 설명한다.

[모듈 구성](README.md)
