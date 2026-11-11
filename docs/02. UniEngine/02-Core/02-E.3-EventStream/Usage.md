# EventStream 사용 예제

## 구독과 필터

```csharp
using System;
using Blackthunder.UniEngine.Core;

using var stream = new EventStream<UnitEvent>();
Func<UnitEvent, bool> death = unitEvent => unitEvent.Kind == EventKind.Death;

using var score = stream.Where(death).Subscribe(_ => AddScore());
using var sound = stream.Where(death).Subscribe(_ => PlaySound());
stream.OnNext(new UnitEvent(EventKind.Death));
```

UnitEvent는 Kind 속성을 가진 이벤트 형식이다. 두 구독이 같은 death 객체를 사용하므로 조건은 한 번 평가된다.

## 값이 같은 경로

```csharp
using var events = new EventStream<string>();
using var score = events.When("death").Subscribe(_ => AddScore());
using var sound = events.When(new string(new[] { 'd', 'e', 'a', 't', 'h' }))
    .Subscribe(_ => PlaySound());
using var missing = events.When(null).Subscribe(_ => Console.WriteLine("missing"));

events.OnNext("death");
events.OnNext(null);
```

두 문자열은 서로 다른 객체여도 EqualityComparer&lt;string&gt;.Default로 같으므로 같은 When 항목을 공유한다. 첫 값은 score와 sound에, null 값은 missing에 전달된다. Nullable&lt;T&gt;의 null target도 같은 규칙을 따른다. 조건 함수를 넘길 때는 Where를 사용한다.

같은 target이라도 부모 조건이나 순서 그룹이 다르면 항목을 공유하지 않는다. target 객체의 Equals·GetHashCode 결과는 등록 후 바꾸지 않아야 한다. 발행 중 추가하거나 제거 후 다시 등록한 항목은 다음 값부터 받는다. 비교가 발행 중 예외를 던지면 같은 위치의 모든 When target 경로가 해제되고 형제 경로는 계속 진행한다.

`EventStream<ulong>`에서 `When(3UL)`은 값 3에만 반응한다. 아래 ForFlags 예제의 마스크 비트 1은 값 1과 3 모두에 반응하므로 두 연산의 의미가 다르며 같은 부모 아래 함께 사용할 수 있다. When의 성능과 Unity Mono·IL2CPP 할당은 아직 검증하지 않았다.

## 서로 다른 enum의 비트 조건

```csharp
using System;
using Blackthunder.UniEngine.Core;

using var events = new EventStream<ulong>();
using var shapes = events.ForFlags(GeoEvents.Shape).Subscribe(_ => Console.WriteLine("shape"));
using var locations = events.ForFlags(RouteEvents.Location).Subscribe(_ => Console.WriteLine("location"));

events.OnNext(GeoEvents.Shape.ToBits().Combine(RouteEvents.Location.ToBits()));

[Flags]
enum GeoEvents
{
    Shape = 1
}

[Flags]
enum RouteEvents
{
    Location = 2
}
```

하나의 ulong 스트림에 서로 다른 enum의 비트를 함께 발행한다. 값에 두 마스크의 비트가 모두 있으므로 shape, location 순서로 출력한다. 마스크에 여러 비트를 넣어도 하나만 겹치면 통과한다. 값 0은 어떤 ForFlags 구독에도 전달하지 않는다.

enum을 비트로 발행할 때는 ulong 캐스트 대신 ToBits를 쓴다. 기반 형식은 강제하지 않으며, ToBits는 부호 비트를 포함해 선언된 크기만큼의 비트를 보존하고 나머지를 0으로 채운다. 같은 부모 아래 마스크 비트가 같으면 enum 형식이 달라도 경로를 공유하므로, 서로 다른 분류로 구분할 비트는 호출자가 배정해야 한다. `[Flags]`가 없는 enum이나 비트가 0인 마스크는 ForFlags 호출 시 ArgumentException이 발생한다.

## 횟수 제한

```csharp
using var stream = new EventStream<int>();
var firstTwo = stream.Take(2);
using var early = firstTwo.Subscribe(value => Console.WriteLine($"early {value}"));
stream.OnNext(1);
using var late = firstTwo.Subscribe(value => Console.WriteLine($"late {value}"));
stream.OnNext(2);
stream.OnNext(3);
```

출력은 early 1, early 2, late 2이다. Take는 새 스트림을 반환하고, 그 스트림을 구독한 쪽끼리 남은 횟수를 공유한다. 횟수를 따로 세려면 Take를 다시 호출한다.

## 독립 분기

```csharp
using var parent = new EventStream<int>();
using var branch = parent.Where(value => value > 0).Branch();
using var handle = branch.Subscribe(value => Console.WriteLine(value));
parent.OnNext(-1);
parent.OnNext(2);
branch.OnNext(-3);
```

출력은 2와 -3이다. 부모가 발행한 값에는 Positive 조건이 적용되고, 분기에 직접 넣은 값은 그 분기 안에서만 전파된다.

## 순서 그룹과 Merge

```csharp
using var updates = new EventStream<int>();
using var later = updates.ForOrder(Phase.After).Subscribe(_ => Render());
using var earlier = updates.ForOrder(Phase.Before).Subscribe(_ => Simulate());
updates.OnNext(1);

using var left = new EventStream<int>();
using var right = new EventStream<int>();
using var merged = left.Merge(right);
using var values = merged.Subscribe(value => Console.WriteLine(value));
left.OnCompleted();
right.OnNext(7);
right.OnCompleted();
```

Phase는 Before와 After를 가진 열거형이다. 등록 순서와 관계없이 Simulate가 Render보다 먼저 호출된다. Merge 예제는 7을 전달한 뒤 두 입력이 모두 완료했을 때 종료한다.
