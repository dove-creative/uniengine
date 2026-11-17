# RouteSystem

RouteSystem은 원본 그래프의 특정 위치와 부분 구간을 참조하고, 방향이 있는 구간 목록을 하나의 경로로 조회한다.

## 목차

- Pin과 Hold
- Clip
- SegmentView와 Route
- Route 끝점 편집
- 통신과 수명

## Pin과 Hold

NodePin은 원본 노드의 위치를, SegmentPin은 원본 구간의 Location 위치를 따른다. 둘 다 노드이므로 별도 구간을 연결할 수 있다. Pin을 원본에 붙이는 Connect·Disconnect는 그래프 끝점의 ConnectNode·DisconnectNode와 구분한다.

Hold는 자신이 소유한 Pin을 통해 위치를 따른다. 노드와 구간 사이를 옮길 때 필요한 Pin으로 교체하며 이전 Pin은 폐기한다. 같은 종류의 앵커 이동에서는 Pin을 재사용한다. Hold는 Pin을 소유하지만 Pin이 가리키는 원본은 소유하지 않는다.

IsValid는 현재 앵커에서 유효한 위치를 얻을 수 있는지를 나타낸다. 앵커가 없거나 Location이 범위를 벗어나면 유효하지 않으며, Position에는 마지막으로 계산한 값이 남는다. 소비자는 IsValid를 함께 확인해야 한다. 앵커 또는 형상이 다시 유효해지면 갱신 알림으로 위치를 회복한다.

## Clip

Clip은 두 SegmentPin 사이를 하나의 구간으로 제공한다. 양끝 Pin이 같은 원본 구간을 가리킬 때 SegmentSource가 결정된다. Clip에 원본을 별도로 연결하는 API는 없다. 두 핀의 실제 From 기준 거리로 Clip.IsForward와 길이를 계산한다. 핀의 IsForward 값이 같거나 다른지만으로 Clip 방향을 결정하지 않는다.

Clip을 정방향으로 조회하면 자신의 From Pin에서 To Pin을 향하고, 역방향이면 To Pin에서 From Pin을 향한다. 원본에 대한 Clip 방향과 조회 방향을 조합하여 원본 위치와 회전을 구한다. 원본 불일치, 유효하지 않은 위치 또는 같은 위치의 핀은 유효하지 않은 Clip을 만든다. 핀을 옮기거나 재연결하여 회복할 수 있다. Clear는 끝점 연결을 비우며 핀과 원본을 폐기하지 않는다.

## SegmentView와 Route

SegmentView는 Source와 IsForward를 보유하는 불변 값이다. IsForward는 Source의 방향을 따를지를 나타낸다. EventHub나 별도 수명이 없으며 경로 편집 기능도 없다.

공통 RouteSystem.Route는 Geometry의 Segment 계약을 구현하고 Views를 읽기 전용 목록으로 제공한다. View 관리·끝점 편집·구독·수명·Property·TraversalOptions를 담당하며 Planar.Route가 이를 상속하여 평면 타입 검사와 공간 조회를 추가한다. 공통 Route는 Geometry 노드·구간을, Planar.Route는 Planar 노드·구간을 받는다.

Append는 앞이나 뒤에 View를 추가하고 입력의 Source·IsForward를 불변 스냅샷으로 저장한다. Remove는 지정한 Source가 경로의 맨 앞 또는 맨 뒤에 있을 때 한 View를 제거한다. 양쪽 모두 같은 Source이면 앞쪽을 먼저 제거한다. 경로 길이는 각 View의 길이를 합산하고, 같은 Source가 반복되어도 하나의 구독을 유지하여 한 번의 원본 갱신으로 다시 계산한다.

Planar의 Route와 Clip은 `ISegment<Vector2, float>`로 조회할 수 있다. NodePin·SegmentPin·Hold도 `INode<Vector2>`로 사용할 수 있으므로 소비자가 구체 클래스에 의존하지 않고 위치와 연결을 조회할 수 있다.

Views가 있으면 From·To는 첫 View와 마지막 View의 방향에서 결정된다. Nodes는 원본 구간들의 노드를 참조 기준으로 중복 없이 열거한다. 수동 View 편집 후에는 이 목록에서 끝점을 다시 유도하며 마지막 View를 제거하면 양끝도 비운다. 목록 순서와 연결 가능성은 호출자가 구성하며 Append는 인접 View의 연속성을 강제하지 않는다. 정확한 구간 경계에서는 현재 조회 방향으로 먼저 도달한 View의 회전을 반환한다.

Clip과 Route는 TraversalOptions를 기본값 Both로 독립 보관한다. 자신의 From→To가 Forward이며 원본 옵션이나 SegmentView·Clip의 원본 방향에서 자동 환산하지 않는다. 원본이 None이어도 Clip·Route의 공간 조회는 가능하고, Clip을 통한 탐색은 Clip 자신의 옵션을 따른다. Route는 원본 노드의 Segments에 자동 등록하지 않으므로 Views에 넣는 것만으로 탐색 가능한 새 간선이 생기지는 않는다.

## Route 끝점 편집

IRoute는 `bool TryConnectNode(ConnectionSide side, INavNode node)`를 제공한다. ConnectNode는 같은 연결을 수행하되 도달할 경로가 없으면 InvalidOperationException을 던진다. 두 API 모두 끝점 교체 시 반대쪽 끝점을 유지하고 From→To 전체를 다시 탐색한다. From을 바꾸면 새 From에서 기존 To로 탐색한다.

| 생성자 | 탐색기 선택 |
| --- | --- |
| RouteSystem.Route() | Dijkstra |
| RouteSystem.Planar.Route() | AStar&lt;Vector2&gt;(Vector2.Distance) |
| 각 Route(IPathfinder) | 전달한 탐색기를 그대로 사용, null 거부 |

기본 제공자는 생성자에서 선택하며 노드 타입을 보고 실행 중 탐색기를 바꾸지 않는다. Planar 기본 A*의 최단 경로 보장에는 거리 휴리스틱이 실제 남은 비용을 과대평가하지 않아야 한다는 조건이 있다. 거리보다 작은 비용의 사용자 구간을 사용하면 조건에 맞는 탐색기나 Dijkstra를 명시적으로 전달한다.

| 상태와 요청 | 결과 |
| --- | --- |
| 빈 Route에 첫 끝점 연결 | 지정한 끝점만 보관, 탐색 없음 |
| 반대쪽 끝점 연결 | 양끝 사이의 경로 탐색 |
| 점유된 끝점을 다른 노드로 연결 | 전체 경로 재탐색 후 교체 |
| 현재 끝점에 같은 노드 연결 | 성공, 탐색·알림 없음 |
| 출발점=도착점 | 양끝이 같은 노드, 빈 Views, Length=0 |
| 한쪽 DisconnectNode | Views를 비우고 반대쪽 끝점만 유지 |
| Clear·ClearNodes | Views·양쪽 끝점·구독을 모두 비움 |

Views가 없는 상태에서도 저장된 끝점을 Nodes에 포함한다. IsIsolated는 Views와 양끝이 모두 없을 때만 true이다. 길이 0 상태의 Planar 공간 조회는 기존 규칙대로 false를 반환한다. DisconnectNode(node)는 해당 노드와 일치하는 끝점을 해제하고 양끝이 같은 노드이면 둘 다 제거한다. null·무관한 노드나 이미 빈 쪽의 해제는 아무 변화가 없다.

TryConnectNode가 false를 반환하거나 탐색·결과 검증이 실패하면 기존 끝점·Views·길이·구독을 보존한다. 탐색 결과는 요청한 양끝·연속성·구간 타입·통행 방향·의존 순환을 검사한 뒤 반영한다. null 노드·미정의 side·폐기 또는 호환되지 않는 노드는 인자 오류이며, 폐기된 Route의 끝점 편집은 ObjectDisposedException이다. 입력이나 탐색기 오류를 도달 불가 false로 숨기지 않는다.

연결 성공은 상태와 구독을 완성한 뒤 Connect|Shape를, 기존 끝점·경로 교체는 Connect|Disconnect|Shape를 한 번 발행한다. 해제는 Disconnect|Shape이며 무변경 요청은 알리지 않는다. 관찰자 예외는 호출자에게 전달하지만 이미 완료된 경로는 유지한다. 원본 형상은 계속 따라가되 원본 연결·통행 옵션 변경에 따른 자동 재탐색은 수행하지 않는다. 탐색·편집 도중 그래프 변경과 재진입 편집은 지원하지 않는다.

기존 IRoute 직접 구현자는 TryConnectNode 계약을 추가해야 한다. 공통과 Planar 네임스페이스를 함께 가져오는 소비자는 Route 타입을 별칭이나 전체 이름으로 구분한다.

## 통신과 수명

각 객체는 자신이 참조하는 원본의 EventHub를 구독한다. RouteEvents는 Navigation 플래그와 같은 연결·형상·Traversal = 16 비트를 사용하며 Location = 8을 유지한다. All은 이 비트를 모두 포함한다. SegmentPin의 Location 편집은 Location과 Shape를 함께 알린다. Route·SegmentPin의 형상 갱신 구독은 연결·형상 변경만 받으므로 원본 Traversal 변경은 파생 Shape 알림으로 전달되지 않는다. 재연결 시 이전 원본 구독을 해제하며, 이후 이전 원본의 갱신이나 폐기는 영향을 주지 않는다.

원본의 폐기는 그 원본을 참조하는 Pin·Clip·Route와 Pin을 소유한 Hold의 수명에 전파된다. 반대로 참조 객체를 폐기해도 원본은 폐기하지 않는다. 원본 노드와 구간의 단순 연결 해제는 원본 폐기와 다르며, Pin·Clip은 살아 있는 상태에서 유효성을 잃었다가 회복할 수 있다.

Route는 사용 중인 Source 구간을 구독하고, Views 없이 끝점만 보관하는 동안에는 해당 노드의 폐기를 구독한다. 끝점을 비우거나 View 경로로 교체하면 이전 단독 끝점 구독을 해제한다. 의존 순환 검사는 공통 Geometry에서 수행하며 View 없는 Route의 저장된 끝점도 포함한다.

Pin·Hold·Clip은 기반 클래스의 객체별 Property를 사용하고 Route도 독립 저장소를 지연 생성한다. 원본과 공유하지 않으며 Property 편집은 자동 알림을 발행하지 않는다. 원본 폐기로 연쇄 Dispose되더라도 각 Property의 내용과 페이로드는 유지된다. 폐기 후 저장소 접근과 정리는 사용자 책임이다.

[상위 구성](README.md)
