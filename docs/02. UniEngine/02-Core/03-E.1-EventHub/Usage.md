# EventHub 사용 예제

객체 연결과 폐기 알림, 직접 구독, 수면과 깨우기, 여러 허브의 일괄 수면을 현재 EventHub API로 구성하는 예제이다.

각 C# 블록은 독립된 콘솔 프로그램이다. UniEngine을 참조하는 프로젝트에서 하나씩 실행하며, 문법은 C# 9를 기준으로 한다. 모든 출력은 동기 호출 순서를 그대로 보여준다. 설계와 공통 수명 규칙은 [모듈 구성](README.md)을 따른다.

## 목차

- 객체 연결과 폐기 알림
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
                .OnUpdated(0, () => Console.WriteLine("HealthBar: defeated"))
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

OnUpdated에 대상 값을 넘기면 그 값과 같은 갱신에서만 실행하고, 조건 함수를 넘기면 조건이 true인 갱신에서만 실행한다. Unit을 폐기하면 HealthBar의 OnDisposed가 실행되므로 폐기된 Unit의 참조가 남지 않는다. 폐기 알림의 단계는 [EventHub](EventHub.md), 콜백 구성 규칙은 [EventHubBuilder](EventHubBuilder.md)에서 설명한다.

## 직접 구독

수신자 객체 없이 콜백만 등록하려면 Updated·Disposing·Disposed 이벤트를 직접 구독한다. 직접 구독은 연결 수에 포함하지 않으며, 해제할 때는 등록한 대리자를 그대로 넘긴다.

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

log를 먼저 구독했지만 Listener가 먼저 호출된다. 허브는 구독 순서와 관계없이 연결 수신자를 직접 구독자보다 먼저 호출한다.

```text
Connections: 1
Listener: opened
Log: opened
Listener: closed
Log: door disposed
```

직접 구독은 수신자 객체를 따로 만들지 않아도 되는 가벼운 경로이다. 대신 해제 시점은 구독한 쪽이 관리해야 한다. 여러 콜백을 한 번에 해제하거나 폐기 단계별 반응을 함께 구성하려면 Connect를 사용한다.

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
