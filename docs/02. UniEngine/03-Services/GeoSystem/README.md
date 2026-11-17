# GeoSystem

GeoSystem은 노드와 구간의 연결을 모델링하고, 그 위에서 위치와 경로를 조회하는 서비스이다.

## 목차

- 소개
- 구성
- 협력 흐름
- 구현 범위

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 그래프의 연결과 공간 계산, 경로 참조를 계층별로 구성한다. |
| 목적 | 원본 그래프를 편집하면서 Pin·Clip·Route가 원본의 변화를 따라가도록 한다. |
| 구현 | Navigation의 연결 관리, 공통 Geometry·RouteSystem 계약, Planar의 2차원 계산으로 나눈다. |

## 구성

| 계층 | 책임 | 문서 |
| --- | --- | --- |
| Navigation | 노드·구간 연결, 통행 옵션, BFS·DFS·Dijkstra 탐색 | [Navigation](Navigation.md) · [경로 탐색](Pathfinding.md) |
| Geometry | 제네릭 노드·구간 계약, 구간 내 Location, AStar&lt;P&gt; 탐색 | [Geometry](Geometry.md) |
| Geometry.Planar | Vector2 위치와 도 단위 Transform2D, 직선·PolySegment와 Shaper 계산 | [Geometry](Geometry.md) |
| RouteSystem·RouteSystem.Planar | 앵커 위치, 부분 구간, Route 끝점 편집과 경로·공간 조회 | [RouteSystem](RouteSystem.md) |

공통 Geometry는 위치 타입 P와 회전 타입 R을 받는 제네릭 계약을 제공한다. 위치만 필요한 계약은 P만 받고, 변환 조회 계약에서 R을 추가한다. `IPositioned<P>`는 위치 조회 계약이며, 연결 변경은 Navigation의 가변 계약에 둔다. Planar는 Vector2 위치와 float 회전으로 이 계약을 구현한다. 제네릭 없는 기존 공통 계약은 좌표 형식과 무관한 연결 참조에 사용한다. 각 이름 앞에는 `BlackThunder.GeoSystem` 네임스페이스가 붙는다.

소스는 `src/03-Services/GeoSystem` 아래에서 좌표계 독립 코드인 Common과 평면 구현인 Planar로 나눈다. Common에는 인터페이스뿐 아니라 NavNode·NavSegment 같은 기본 구현과 Location 같은 값 타입도 포함한다. Common은 폴더 분류에만 사용하며 공개 네임스페이스에는 넣지 않는다.

| 폴더 | 네임스페이스 |
| --- | --- |
| `Common/Navigation` | `BlackThunder.GeoSystem.Navigation` |
| `Common/Geometry` | `BlackThunder.GeoSystem.Geometry` |
| `Common/RouteSystem` | `BlackThunder.GeoSystem.RouteSystem` |
| `Planar/Geometry` | `BlackThunder.GeoSystem.Geometry.Planar` |
| `Planar/RouteSystem` | `BlackThunder.GeoSystem.RouteSystem.Planar` |

네임스페이스는 기능을 기준으로 묶고 폴더는 공통 코드와 공간별 구현을 기준으로 묶는다. 본체 어셈블리와 Core의 EventHub·Infrastructure의 Transform2D 의존성은 유지한다.

## 협력 흐름

객체는 자신이 의존하는 원본을 참조하고 원본의 EventHub를 구독한다. Node의 위치가 바뀌면 LinearSegment·PolySegment가 형상을 갱신하고, SegmentPin·Clip·Route가 그 변화를 전달받는다. PolySegment의 Values·Handles·Shaper 편집도 같은 Shape 갱신 흐름을 따른다. 연결 요청은 직접 메서드로 수행하고, 알림은 변경된 상태를 관찰하는 용도로 사용한다.

연결 양쪽의 상태는 연결 알림 전에 일치해야 한다. 공간 계산 객체도 자신의 상태를 먼저 갱신한 뒤 알린다. 모든 통신은 동기 방식이며, 최상위 원본의 알림 도중에 의존 그래프 전체의 갱신이 이미 끝났다고 보장하지 않는다. 소비자는 필요한 계산 결과를 소유한 객체의 알림을 구독한다. 알림 중 동일 객체를 다시 변경하는 흐름과 다중 스레드 편집은 지원 범위가 아니다.

## 구현 범위

Project-Index의 연결·앵커·경로 모델을 기준으로 Navigation, 평면 직선·PolySegment, NodePin·SegmentPin·Hold·Clip·SegmentView·Route를 제공한다. PolySegment의 FreeHandle·Bezier2·Bezier3 Shaper는 입력을 Hinge 점열로 바꾸며, 조회는 점열의 직선 조각을 사용한다. Unity에 의존하지 않으며 System.Numerics.Vector2와 Infrastructure의 Transform2D를 사용한다.

ILinearSegment와 IPolySegment는 제네릭과 Planar 계약을 제공한다. 공통 추상 클래스는 연결·편집·갱신을 관리하고 Planar는 거리·각도·보간을 계산한다. 경로 탐색은 Navigation의 BFS·DFS·Dijkstra와 Geometry의 AStar&lt;P&gt;를 제공한다. 공통 Route는 Dijkstra를 기본으로 끝점 사이의 경로를 구성하고, Planar.Route는 이를 상속하면서 기본 탐색기를 A*로 지정한다. 노드·구간은 외부 속성 저장소 IPropertySubject를 지원한다. Circular·Offset Shaper, 3차원 계산과 원본 변경에 따른 자동 재탐색은 후속 범위이다.

[사용 예시](Usage.md) · [TTC](Geo%20System%20-%20TTC.drawio) · [Services](../README.md)
