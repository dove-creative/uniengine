# GeoSystem 사용 예시

노드 이동, Pin·Clip 조절과 Route 위 이동을 직접 확인하려면 [GeoSystem 시각화 예제](../../../../samples/03-Services/GeoSystem/README.md)를 실행한다.

평면 직선을 만든 뒤 일부 구간을 Clip으로 참조하고 Route에 넣는 예시이다.

```csharp
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Geometry.Planar;
using BlackThunder.GeoSystem.RouteSystem.Planar;

using var from = new Node(Vector2.Zero);
using var to = new Node(new Vector2(10f, 0f));
using var line = new LinearSegment(from, to);
using var start = new SegmentPin(line, new Location(0.2f, DistanceType.Rate));
using var end = new SegmentPin(line, new Location(0.8f, DistanceType.Rate));
using var clip = new Clip(start, end);
using var route = new Route();
route.Append(new SegmentView(clip));

// Starts at the clip's To pin and travels toward its From pin.
if (route.TryGetTransform(new Location(1f, isForward: false), out var pose))
{
    // Position = (7, 0), Rotation = 180 degrees.
}

to.Position = new Vector2(20f, 0f);
// The clip and route now have length 12.
route.ClearNodes();
// The line and its endpoints remain connected.
```

객체의 결과를 관찰할 때는 해당 객체의 EventHub에 리스너를 등록한다. GeoEvents는 복합 값이므로 마스크로 확인한다.

```csharp
var listener = new object();
route.EventHub.Connect(listener).OnUpdatedWhere(
    flags => (flags & (int)BlackThunder.GeoSystem.Navigation.GeoEvents.Shape) != 0,
    () => ObserveLength(route.Length));

// Release the subscription when the consumer no longer needs it.
route.EventHub.Disconnect(listener);
```

ObserveLength는 소비자가 제공하는 콜백이다. 전체 의존 관계의 갱신 완료를 원본 Node의 알림에서 추정하지 말고, 사용할 Route·Clip 등의 결과 알림을 구독한다.

[상위 구성](README.md)
