# 경로 탐색

경로 탐색은 현재 그래프와 통행 옵션을 읽고, 여러 목표 중 하나까지의 이동 순서를 반환한다.

## 목차

- 입력과 결과
- 알고리즘
- Geometry와 휴리스틱
- 실행과 실패

## 입력과 결과

Navigation.IPathfinder의 기본 계약은 다음과 같다.

```csharp
bool TryPathfind(
    IReadOnlyNavNode from,
    IEnumerable<IReadOnlyNavNode> targets,
    out Path path);
```

목표 집합이 비거나 도달할 목표가 없으면 false와 null을 반환한다. 출발점이 목표에 포함되면 true이며, From만 있고 Arrows 길이가 0인 Path를 반환한다. Arrows는 항상 non-null이고 출발점만 표현하는 Arrow는 만들지 않는다. 노드와 목표는 참조 동일성으로 구분하고, 호출자가 전달한 목표 열거는 호출당 한 번 수행하며 중복을 제거한다.

| 결과 클래스 | 읽기 전용 프로퍼티 |
| --- | --- |
| Navigation.Path | `IReadOnlyNavNode From`, `IReadOnlyList<Arrow> Arrows` |
| Navigation.Arrow | `IReadOnlyNavSegment Segment`, `IReadOnlyNavNode To` |
| Geometry.Path&lt;P&gt; | 타입을 좁힌 `IReadOnlyNode<P> From`, `IReadOnlyList<Arrow<P>> Arrows` |
| Geometry.Arrow&lt;P&gt; | 타입을 좁힌 `IReadOnlySegment<P> Segment`, `IReadOnlyNode<P> To` |

Arrow는 실제 구간 이동 한 번을 나타낸다. Segment와 To는 필수이며 To는 완전한 구간의 한 끝점이어야 한다. Path의 첫 이동은 From에서 시작하고 이후 각 이동은 이전 To에서 이어진다. 공개 생성자는 null·끝점·연속성·생존 상태를 검사한다. 수동 결과 구성은 통행 옵션을 검사하지 않는다. Path는 입력 목록을 복사하고 변경할 수 없는 목록으로 노출한다.

결과는 탐색 당시의 객체 참조와 순서를 보존한다. 원본의 위치·연결·옵션이 이후 변경되거나 원본이 폐기되어도 결과 목록은 바뀌지 않는다. 결과가 형상이나 길이까지 복제하는 것은 아니므로 참조를 통한 Length 등의 조회는 원본 상태를 따른다. 결과는 원본을 구독·소유·폐기하지 않고 자동 재탐색하지 않는다.

## 알고리즘

| 구현 | 선택 기준 | 비용 |
| --- | --- | --- |
| Navigation.Pathfinders.Bfs | 구간 수가 최소인 경로 | 구간마다 1 |
| Navigation.Pathfinders.Dfs | 인접 열거 순서상 처음 발견한 경로 | 비용 비교 없음 |
| Navigation.Pathfinders.Dijkstra | Length 합이 최소인 경로 | 구간 Length |
| Geometry.Pathfinders.AStar&lt;P&gt; | 허용적 휴리스틱 아래 최소 비용 경로 | 구간 Length + 남은 비용 추정 |

BFS는 큐를 사용하고 DFS는 반복형 깊이 우선 탐색으로 인접 열거 순서를 유지한다. Dijkstra와 A*는 double로 비용을 누적하고 .NET Standard 2.1 호환 내부 최소 힙을 사용한다. 우선순위가 같으면 삽입 순서로 처리하고 같은 비용으로 다시 발견한 경로는 기존 경로를 교체하지 않는다.

Length는 가중 탐색에서 음수·NaN·무한대가 아니어야 하며 길이 0은 허용한다. BFS·DFS는 Length를 비용으로 읽지 않는다. 모든 알고리즘은 현재 노드가 구간의 From인지 To인지 확인하고 해당 방향의 TraversalOptions를 검사한다. 옵션 변경 후 새 호출은 변경된 값을 사용한다.

## Geometry와 휴리스틱

Geometry.IPathfinder&lt;P&gt;는 Navigation.IPathfinder를 확장한다. 타입이 지정된 TryPathfind는 IReadOnlyNode&lt;P&gt; 입력과 Path&lt;P&gt; 출력을 사용한다. Navigation 인터페이스를 통한 호출도 위치 타입 검사 후 같은 탐색에 전달된다. 호환되지 않는 출발점·목표 위치 타입은 ArgumentException이다. Path&lt;P&gt;와 Arrow&lt;P&gt;는 대응하는 Navigation 클래스를 상속하며 기반 타입으로 조회해도 같은 참조를 제공한다.

AStar&lt;P&gt;의 생성자에는 `Func<P, P, float>` 휴리스틱이 필수이다. 여러 목표에 대한 추정값 중 최솟값을 사용하며, 목표 노드의 추정값은 콜백을 호출하지 않고 0으로 처리한다. 휴리스틱은 순수한 비용 추정 함수로 사용하며 음수·NaN·무한대를 반환해서는 안 된다.

최단 경로 보장을 위해 휴리스틱은 실제 남은 최소 비용을 과대평가하지 않아야 한다. 평면 직선의 Length가 유클리드 거리일 때 Vector2.Distance를 사용할 수 있다. 임의로 짧은 비용을 부여한 구간에서는 같은 거리 함수가 조건을 만족하지 않을 수 있다. 0을 반환하면 Dijkstra와 같은 비용 기준으로 탐색한다. A*는 더 낮은 비용을 발견하면 이미 처리한 노드도 다시 탐색하므로 일관되지 않은 허용적 휴리스틱을 지원한다.

## 실행과 실패

| 조건 | 결과 |
| --- | --- |
| null 출발점·목표 열거 | ArgumentNullException |
| 폐기된 출발점 | ObjectDisposedException |
| null·폐기된 목표 원소 | ArgumentException |
| 탐색 도중 발견한 끝점 누락·폐기 구간·폐기 끝점 | 해당 이동을 건너뜀 |
| 가중 탐색이 읽은 음수·비유한 Length | InvalidOperationException |
| A*가 받은 음수·비유한 추정값 | InvalidOperationException |
| A* 그래프의 이동 대상 위치 타입 불일치 | InvalidOperationException |

목표 목록 전체를 검사하므로 출발점이 목표 중 하나이더라도 잘못된 다른 목표 원소가 있으면 인자 오류이다. 가중치 검사는 실제 확장하는 통행 가능한 구간에 적용하며, 그래프 전체를 사전 검증하는 API는 아니다.

탐색은 동기 방식이며 실행 중 그래프 변경은 지원하지 않는다. 탐색기는 연결·통행 옵션·외부 속성·이벤트 구독을 변경하지 않고 호출별 자료구조에 방문·비용·이전 경로를 보관한다. [Route 끝점 편집](RouteSystem.md)은 명시적인 연결 요청에서 탐색 결과를 검증하여 View 목록으로 반영한다. 원본 변경에 따른 자동 재탐색은 제공하지 않는다.

[사용 예시](Usage.md) · [Navigation](Navigation.md) · [Geometry](Geometry.md) · [상위 구성](README.md)
