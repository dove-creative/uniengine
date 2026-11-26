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

공간 구현과 무관한 소비자는 같은 line을 제네릭 계약으로 받는다. 위치 조회에는 TPosition만 필요하며 회전까지 조회할 때 TRotation을 추가한다.

```csharp
IReadOnlySegment<Vector2> positions = line;
positions.TryGetPosition(new Location(0.5f, DistanceType.Rate), out var position);

ILinearSegment<Vector2, float> linear = line;
if (linear.TryGetTransform(new Location(1f), out var point, out var rotation))
{
    // point is Vector2; rotation is a float angle in degrees.
}
```

`Node<TPosition>`와 `Segment<TPosition, TRotation>`는 공통 기본 구현이다. 자체 공간 계산을 추가할 때는 Segment의 TryGetTransform과 범위 TryGetLocation을 구현하거나 구간 인터페이스를 직접 구현한다. 직접 구현자는 TryGetLocation의 두 오버로드를 모두 제공한다. `ILinearSegment<TPosition, TRotation>`와 `IPolySegment<TPosition, TRotation>`는 기본 구현 클래스 상속을 요구하지 않는다.

실제 좌표에서 구간 내 거리를 찾을 때는 TryGetLocation을 사용한다. 다음 예시는 길이가 10인 직선에서 From으로부터 2만큼 떨어진 좌표를 조회한다.

```csharp
using var origin = new Node(Vector2.Zero);
using var destination = new Node(new Vector2(10f, 0f));
using var segment = new LinearSegment(origin, destination);
var target = new Vector2(2f, 0f);

segment.TryGetLocation(target, out var forward);
// forward = Length 2, IsForward true.
segment.TryGetLocation(target, out var backwardRate,
    isForward: false, distanceType: DistanceType.Rate);
// backwardRate = Rate 0.8, IsForward false; both results identify the same position.

segment.TryGetLocation(new Vector2(1.875f, 0f), 2f, 8f,
    out var bounded, out var errorSquared, epsilon: 0.125f);
// bounded = Length 2; errorSquared = 0.015625.
```

검색 범위는 항상 구간 From 기준 실제 거리이다. 시작·끝을 뒤집으면 같은 오차의 후보를 선택하는 검색 순서가 바뀐다. 결과의 isForward만 바꾸는 것은 후보 선택에 영향을 주지 않는다. Clip·Route에서도 해당 객체 자체의 범위와 Location을 사용한다.

PolySegment의 형상을 편집할 때는 수치 설정과 절대 위치 Handle을 사용한다. 다음 예시는 이차 베지어를 만든 뒤 제어점을 이동하고 꺾은선으로 바꾼다.

```csharp
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Geometry.Planar;

using var from = new Node(Vector2.Zero);
using var to = new Node(new Vector2(6f, 0f));
using var handle = new ControlHandle(new Vector2(3f, 4f));
using var curve = new PolySegment(from, to, new QuadraticBezierShaper());
curve.Values[ShaperValues.TargetSegmentLength] = 0.25f;
curve.Handles.Add(handle);

handle.Position = new Vector2(3f, 2f);
// Length, spatial queries, and dependent pins are already updated here.
curve.TryGetTransform(new Location(0.5f, DistanceType.Rate), out var pose);

curve.Shaper = new PolylineShaper();
// The same inputs now describe From -> handle -> To, with each leg subdivided.
float length = curve.Length;
bool hasEnd = curve.TryGetPosition(new Location(1f, DistanceType.Rate), out var endPosition);
// hasEnd is true and endPosition matches to.Position for this valid curve.
```

인자 없는 PolySegment도 PolylineShaper를 기본으로 사용한다. CubicBezierShaper는 Handle 2개를 순서대로 받는다. 필요한 Handle 수가 맞지 않으면 Length는 0이고 Try 조회가 실패하며, 입력을 갖추면 즉시 회복한다. 내부 분할 목록은 공개하지 않으며 Length와 Try 조회로 결과를 사용한다. Handle의 소유권은 호출자에게 있으므로 여러 구간이 공유할 수 있다. 노드 이동은 형상의 끝점을 갱신하고 중간 Handle의 절대 위치는 유지한다. Linear·Clip·Route의 Values·Handles는 빈 읽기 전용 컬렉션이다.

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

통행 방향과 목표를 지정하여 탐색할 수 있다. 다음 예시의 직선 Length는 유클리드 거리이므로 A* 휴리스틱으로 Vector2.Distance를 사용한다.

```csharp
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Geometry.Planar;
using BlackThunder.GeoSystem.Navigation;

using var origin = new Node(Vector2.Zero);
using var destination = new Node(new Vector2(10f, 0f));
using var road = new LinearSegment(origin, destination);
road.TraversalOptions = TraversalOptions.Forward;
origin.Property.Add("name", "Origin");

var search = new AStarPathfinder<Vector2>(Vector2.Distance);
if (search.TryPathfind(origin, new[] { destination }, out var path))
{
    var current = path.From;
    foreach (var arrow in path.Arrows)
    {
        // Each arrow refers to the traversed segment and its destination node.
        current = arrow.To;
    }
    // With origin among the targets, Arrows is empty and current remains origin.
}

IPathfinder cheapest = new DijkstraPathfinder();
bool reverse = cheapest.TryPathfind(destination, new[] { origin }, out var reversePath);
// reverse is false and reversePath is null under Forward-only traversal.
road.TraversalOptions = TraversalOptions.Both;
reverse = cheapest.TryPathfind(destination, new[] { origin }, out reversePath);
// The new search now succeeds. Existing path results keep their references and order.

origin.Dispose();
string name = origin.Property.Get<string>("name");
origin.Property.Remove("name");
// Dispose does not clear Property or dispose its values; the caller manages cleanup.
```

구간 수가 적은 경로에는 BfsPathfinder, 열거 순서의 첫 경로에는 DfsPathfinder를 사용한다. 임의 비용 그래프에서는 거리 휴리스틱이 실제 비용을 과대평가하지 않는지 확인하고, 추정 근거가 없으면 0을 반환하거나 DijkstraPathfinder를 사용한다. 상세 계약은 [경로 탐색](Pathfinding.md)을 참고한다.

Route의 끝점을 연결하면 경로를 찾아 Views로 반영한다. 경로 교체가 실패하면 기존 결과를 유지한다.

```csharp
using System.Numerics;
using BlackThunder.GeoSystem.Geometry.Planar;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.Geometry;
using Route = BlackThunder.GeoSystem.RouteSystem.Planar.Route;

using var a = new Node(Vector2.Zero);
using var b = new Node(Vector2.UnitX);
using var c = new Node(new Vector2(2f, 0f));
using var isolated = new Node(new Vector2(5f, 0f));
using var ab = new LinearSegment(a, b);
using var bc = new LinearSegment(b, c);
using var journey = new Route(); // Planar defaults to A* with Vector2.Distance.
journey.ConnectNode(ConnectionSide.From, a); // No search until both endpoints exist.
bool connected = journey.TryConnectNode(ConnectionSide.To, c);
// connected = true, From = a, To = c, Views = [ab, bc].

bool replaced = journey.TryConnectNode(ConnectionSide.To, isolated);
// replaced = false; the previous a-to-c route remains intact.
journey.DisconnectNode(ConnectionSide.To);
// From = a, To = null, Views is empty.
journey.ConnectNode(ConnectionSide.To, b);
// The entire a-to-b route is searched again.

using var custom = new Route(new PathfinderAdapter<Vector2>(new DijkstraPathfinder()));
using var common = new BlackThunder.GeoSystem.RouteSystem.Route(); // Defaults to DijkstraPathfinder.
```

원본 노드의 Segments에는 journey 자체를 추가하지 않는다. 수동 Append·Remove로 View 목록을 편집할 수도 있으며, 기존의 자동 형상 갱신과 Property·TraversalOptions 계약은 그대로 적용된다.

## Tracer로 Route에서 연결 구간으로 이동

[Tracer와 Wheel 시각화 예제](../../../../samples/03-Services/GeoSystem/README.md#tracer와-wheel)에서 두 위치 기준을 비교하고 분기 예약을 직접 실행할 수 있다.

앞 예제의 Planar Route와 제네릭 RouteSystem 타입을 함께 사용한다. Absolute는 Route가 정리되어도 현재 말단을 보관한다. 분기 예약 1은 다음 분기의 두 번째 출구를 뜻한다.

```csharp
var tracer = new BlackThunder.GeoSystem.RouteSystem.PolyTracer<Vector2, float>();
var type = BlackThunder.GeoSystem.RouteSystem.LocationType.Absolute;
if (tracer.PlaceOnRoute(journey, new Location(0f), type))
{
    tracer.Wheel.ReservedRoutes.Enqueue(1);
    tracer.DetachRoute();
    bool moved = tracer.Move(5f, out var endState, out var remaining);
    if (tracer.TryGetTransform(out var position, out var rotation))
        System.Console.WriteLine($"{moved} {endState} {position} {rotation} {remaining}");
}
```

[상위 구성](README.md)

## 제네릭 Route와 그룹

평면 Geometry도 공통 제네릭 Route·Hold와 그룹에 그대로 등록할 수 있다. 원소는 호출자가 소유하고 그룹은 등록만 관리한다.

```csharp
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.RouteSystem;
using Planar = BlackThunder.GeoSystem.Geometry.Planar;

using var from = new Planar.Node(Vector2.Zero);
using var to = new Planar.Node(new Vector2(10f, 0f));
using var line = new Planar.LinearSegment(from, to);
using var hold = new Hold<Vector2, float>();
using var route = new Route<Vector2, float>();
hold.MoveTo(line, new Location(3f));
route.Append(new SegmentView(line));

using var geometry = new GeoGroup<Vector2, float>();
using var services = new RouteGroup<Vector2, float>();
geometry.AddNode(from);
geometry.AddNode(to);
geometry.AddSegment(line);
services.AddHold(hold);
services.AddRoute(route);
geometry.AddGroup(services);

// Direct geometry membership contains two nodes; recursive enumeration includes the hold.
foreach (var node in geometry.EnumerateNodes())
{
    Vector2 position = node.Position;
}

IReadOnlyGeoGroup<Vector2, float> routeView = services;
// routeView.Nodes and routeView.Segments expose the registered hold and route.
route.Dispose();
// services.Routes is now empty; line and its endpoints remain alive.
```

조회·편집 인터페이스와 공유·중첩·수명 규칙은 [그룹 컨테이너](Groups.md)를 따른다.

## Linear Network Creator

외부 입력에서 현재 감지 결과를 갱신하고, Begin으로 생성을 시작한다. Update는 상태만 갱신하고 TryConfirm이 노드·구간을 확정한다. 다음 예시는 Geometry 네임스페이스를 사용하며 평면 타입에 별칭을 지정한 경우이다.

```csharp
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using PlanarNode = BlackThunder.GeoSystem.Geometry.Planar.Node;
using PlanarSegment = BlackThunder.GeoSystem.Geometry.Planar.LinearSegment;

var detection = new Detection<Vector2>(Vector2.Zero);
using var group = new GeoGroup<Vector2, float>();
using var creator = new LinearGeometricNetworkCreator<Vector2>(
    () => detection,
    (Detection<Vector2> hit, out INode<Vector2> node) =>
    {
        node = new PlanarNode(hit.Position);
        return true;
    },
    (INode<Vector2> from, INode<Vector2> to, out ISegment<Vector2> segment) =>
    {
        segment = new PlanarSegment(
            (BlackThunder.GeoSystem.Geometry.Planar.INode)from,
            (BlackThunder.GeoSystem.Geometry.Planar.INode)to);
        return true;
    });

creator.Begin();
Confirm();
detection = new Detection<Vector2>(new Vector2(5, 0));
creator.Update();
Confirm();
creator.Finish();

void Confirm()
{
    if (!creator.TryConfirm(out var result)) return;
    if (result.IsNewNode) group.AddNode(result.Node);
    if (result.Segment != null) group.AddSegment((ISegment<Vector2, float>)result.Segment);
}

// The caller owns confirmed objects; the creator and group do not dispose them.
foreach (var segment in group.Segments.ToArray()) segment.Dispose();
foreach (var node in group.Nodes.ToArray()) node.Dispose();
```

기존 노드를 선택하려면 Detection의 node 인자에 전달한다. 구간 위의 Pin·Hold를 만드는 Factory는 Detection.Segment.TryGetLocation으로 위치를 해석한다. 구간 감지 자체가 원본 구간을 분할하지는 않는다. 취소와 실패·소유권 규칙은 [Network Creator](NetworkCreators.md)를 따른다.
