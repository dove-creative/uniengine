# 그룹 컨테이너

그룹은 노드·구간 또는 Hold·Route를 등록하고 하위 그룹으로 묶는 단위이다. 등록 관계는 그래프 연결과 독립적이며 그룹은 원소의 수명을 소유하지 않는다.

## 목차

- 조회와 편집 계약
- 등록과 중첩
- 수명과 정리
- 재귀 조회

## 조회와 편집 계약

| 계층 | 조회 계약 | 편집 계약과 구현체 |
| --- | --- | --- |
| Navigation | IReadOnlyNavGroup | INavGroup·NavGroup |
| Geometry | `IReadOnlyGeoGroup<P>`·`IReadOnlyGeoGroup<P, R>` | `IGeoGroup<P, R>`·`GeoGroup<P, R>` |
| RouteSystem | `IReadOnlyRouteGroup<P, R>` | `IRouteGroup<P, R>`·`RouteGroup<P, R>` |

`IReadOnlyGeoGroup<P>`는 IReadOnlyNavGroup을, 위치·회전 그룹은 위치 전용 그룹을 확장한다. Route 그룹은 위치·회전 Geometry 조회 계약을 확장한다. Geometry의 편집 계약은 INavGroup을 상속하지 않고 Route의 편집 계약도 IGeoGroup을 상속하지 않으므로, 기반 타입을 통한 호환되지 않는 원소의 추가 경로가 없다.

Nodes·Segments·NestedGroups는 직접 등록한 원소를 등록 순서대로 보여 주는 IReadOnlyList이다. NavGroup과 GeoGroup의 편집 계약은 가변 노드·구간 인터페이스로 원소를 제공하고, 조회 계약은 읽기 전용 원소 인터페이스를 제공한다. 캐스팅으로 목록 자체를 편집할 수 없으며, 인터페이스를 바꿔도 같은 목록과 원소를 조회한다.

RouteGroup의 Holds·Routes는 `Hold<P, R>`·`Route<P, R>` 구현체를 직접 제공한다. Geometry의 Nodes·Segments에서는 동일한 목록을 각각 노드·구간으로 조회한다. 원본 그래프의 노드와 Route 내부의 Source를 자동으로 펼쳐 등록하지 않는다. 읽기 전용 Route 그룹에서 꺼낸 Hold·Route의 편집은 가능하다.

NestedGroups는 해당 단계의 읽기 전용 그룹을 받는다. Geometry 그룹에는 같은 P·R의 Route 그룹을 넣을 수도 있다. 조회 인터페이스를 직접 구현하는 사용자는 동일한 원소를 반환하는 상위 조회 계약과 ILifecycleSubject 계약을 지킨다.

## 등록과 중첩

AddNode·AddSegment·AddHold·AddRoute·AddGroup과 대응 Remove 메서드는 실제 등록 상태가 바뀌면 true를 반환한다. 동일 객체의 중복 추가와 미등록 또는 null 원소 제거는 false이다. 비교는 Equals가 아닌 참조 동일성을 사용한다. null 추가는 ArgumentNullException, 이미 폐기된 원소 추가와 순환 중첩은 ArgumentException이며 기존 등록 상태를 보존한다.

한 원소와 한 하위 그룹을 여러 그룹에 등록할 수 있다. 부모 역참조나 단일 소유자는 두지 않는다. 자신·조상을 하위 그룹으로 넣거나 이미 순환하는 외부 그룹을 넣는 요청은 거부한다. 직접 구현한 그룹도 이후의 편집에서 순환 금지를 지켜야 한다.

등록은 연결된 끝점·원본·앵커를 자동 등록하지 않는다. Remove·Clear는 실제 그래프의 Connect·Disconnect를 호출하지 않으며 그룹 밖 원소와 연결할 수 있다. 탐색기는 기존 연결을 따라 이동하고 그룹의 경계를 자동 탐색 제한으로 사용하지 않는다.

## 수명과 정리

모든 그룹은 ILifecycleSubject와 LifecycleHub를 제공한다. 등록할 때 원소의 폐기 완료를 구독하고, 원소 또는 하위 그룹이 외부에서 폐기되면 해당 등록을 자동 제거한다. 여러 그룹에 공유된 원소의 폐기는 모든 등록에 반영된다. 구독 등록이 실패하면 등록을 남기지 않는다.

Remove·Clear·그룹 Dispose는 구독과 등록만 정리한다. 원소·하위 그룹을 Dispose하지 않으며 원소의 Property와 그래프 연결도 그대로 둔다. 그룹 Dispose는 목록을 비운 뒤 폐기 완료를 알리므로 부모 그룹에서도 자동 제거된다. 중복 Dispose는 아무 변화가 없다. 폐기 후 조회는 빈 목록을 반환하고 Add·Remove·Clear는 ObjectDisposedException이다.

추가·제거 또는 원소의 형상 변경을 알리는 그룹 EventHub는 제공하지 않는다. 동시 편집과 열거 중 변경은 지원하지 않는다.

## 재귀 조회

EnumerateNodes·EnumerateSegments와 Route 그룹의 EnumerateHolds·EnumerateRoutes는 현재 그룹과 모든 하위 그룹의 원소를 열거한다. EnumerateDescendants는 현재 그룹을 제외한 하위 그룹을 열거한다. 직접 등록 순서와 하위 그룹 등록 순서에 따른 깊이 우선 순서를 사용하며, 공유 그룹과 공유 원소는 참조 기준으로 한 번만 반환한다.

탐색은 지연 열거이며 호출 때의 스냅샷을 만들지 않는다. 별도의 스냅샷이 필요하면 호출자가 ToArray 등으로 보관한다.

[사용 예시](Usage.md) · [상위 구성](README.md)
