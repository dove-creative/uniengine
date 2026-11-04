# EventStream 사용 예제

## 공급원과 비트마스크 구독

```csharp
using Blackthunder.UniEngine.Core;

using var source = new EventSource<uint>();
using var stream = new EventStream<uint>(source);
var death = new Filter<uint>("Death", flags => (flags & 1u) != 0);

using var score = stream.Where(death).Subscribe(_ => AddScore());
using var sound = stream.Where(death).Subscribe(_ => PlaySound());
source.OnNext(1u);
```

두 구독이 같은 death 객체를 사용하므로 조건은 한 번 평가된다. 상태 있는 경로를 쓰려면 Where(death).Take(2)처럼 연결한다. 해당 경로를 다시 구독하면 두 번째 구독은 자신의 첫 사망 이벤트부터 센다.

## 독립 분기

```csharp
using var parent = new EventStream<int>();
using var branch = parent.Where(new Filter<int>("Positive", value => value > 0)).Branch();
using var handle = branch.Subscribe(value => Console.WriteLine(value));
parent.OnNext(-1);
parent.OnNext(2);
branch.OnNext(-3);
```

출력은 2와 -3이다. 부모가 발행한 값에는 Positive 조건이 적용되고, 분기에 직접 발행한 값은 그 분기 안에서만 전파된다.

## 순서 그룹과 Merge

```csharp
using var updates = new EventStream<int, Phase>(Phase.Before, Phase.Before, Phase.After);
using var later = updates.Subscribe(Phase.After, _ => Render());
using var earlier = updates.Subscribe(Phase.Before, _ => Simulate());
updates.OnNext(1);

using var left = new EventSource<int>();
using var right = new EventSource<int>();
using var merged = EventStream<int>.Merge(left, right);
using var values = merged.Subscribe(value => Console.WriteLine(value));
left.OnCompleted();
right.OnNext(7);
right.OnCompleted();
```

Phase는 Before와 After를 가진 열거형이다. 등록 순서와 관계없이 Simulate가 Render보다 먼저 호출된다. Merge 예제는 7을 전달한 뒤 두 상위가 모두 완료했을 때 종료한다.
