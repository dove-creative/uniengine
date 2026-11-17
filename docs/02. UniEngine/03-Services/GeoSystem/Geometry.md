# Geometry

Geometry는 Navigation 연결을 공간 구간으로 확장하고, Location을 실제 위치와 방향으로 해석한다.

## 목차

- 공통 계약과 Planar
- 편집 데이터와 Shaper
- Hinge와 PolySegment
- Location
- 형상과 조회 실패
- 위치 기반 탐색

## 공통 계약과 Planar

공통 IReadOnlyNode·INode와 IReadOnlySegment·ISegment는 Navigation 계약을 확장하며 서로의 참조 형식을 좁힌다. 그 위에 위치 타입 P와 회전 타입 R을 지정하는 계약을 둔다. 위치만 필요한 소비자는 회전 타입에 의존하지 않는다.

| 계약 | 책임과 관계 |
| --- | --- |
| `IPositioned<P>` | P 형식의 Position 조회 |
| `IReadOnlyNode<P>` | 위치와 연결된 읽기 전용 구간 조회 |
| `INode<P>` | 읽기 전용 노드 계약과 Navigation의 연결 편집 계약 결합 |
| `IReadOnlySegment<P>` | 같은 P를 사용하는 끝점·노드 조회와 TryGetPosition |
| `ISegment<P>` | 위치 조회와 Navigation의 연결 편집 계약 결합 |
| `IReadOnlySegment<P, R>` | 위치 조회 계약에 TryGetTransform 추가 |
| `ISegment<P, R>` | 위치·변환·연결 편집 계약과 Values·Handles |
| `ILinearSegment<P, R>` | 직선 구간 분류 |
| `IPolySegment<P, R>` | Shaper 교체와 읽기 전용 Hinges 조회 |
| `IShaper<P>` | 양끝점·수치 설정·제어점으로 위치열 생성 |

제네릭 TryGetTransform은 P 위치와 R 회전을 별도의 out 인자로 반환한다. 위치와 회전 형식에는 특정 벡터·행렬 라이브러리 제약을 두지 않으며, 인터페이스를 직접 구현할 수도 있다. 제네릭 없는 계약으로 참조해도 원래 노드·구간 객체와 연결을 유지한다.

`Node<P>`는 위치 저장·변경 알림과 연결 관리를 제공한다. 동일한 위치를 다시 설정하면 알리지 않으며, 좌표 형식별 유효성 검사는 ValidatePosition을 재정의하여 추가한다. `Segment<P, R>`는 연결·허용 오차를 관리한다. 추상 클래스 `LinearSegment<P, R>`는 끝점 형상 변경 구독을, `PolySegment<P, R>`는 입력 편집·Shaper 호출·Hinge 갱신을 담당한다. 공간별 구현이 길이·회전·위치 보간을 계산한다. 서로 다른 위치 타입의 연결과 순환 의존은 변경 전에 거부한다.

Planar.IPositioned는 `IPositioned<Vector2>`를, Planar의 노드·구간 인터페이스는 대응하는 Vector2·float 제네릭 계약을 확장한다. Planar.Node는 위치를 편집할 수 있고, Planar.LinearSegment는 양끝 노드를 연결하여 직선 길이·위치·방향을 계산한다. Planar에는 Transform2D를 반환하는 TryGetTransform도 유지하며, 제네릭 조회는 같은 결과를 위치와 회전으로 나누어 반환한다. 회전은 도 단위이며 +X 방향이 0도이고 +Y 방향이 90도이다.

Planar 객체는 기존과 같이 Planar 노드·구간끼리 연결한다. 공통 제네릭 계약으로 조회할 수 있다는 사실이 서로 다른 공간 구현 사이의 연결 호환성을 보장하지는 않는다.

Planar.LinearSegment와 Planar.PolySegment는 각각 공통 제네릭 추상 클래스를 상속한다. Planar.LinearSegment는 더 이상 Planar.Segment의 파생형이 아니므로 두 구간을 함께 받는 소비자는 Planar.ISegment를 사용한다. Planar.Segment는 Clip과 사용자 파생형을 위한 기반 클래스로 유지한다. 기존 `ISegment<P, R>` 직접 구현자는 Values·Handles를 추가해야 한다. Linear·Clip·Route와 공통 Segment 기본 구현은 읽기 전용 빈 컬렉션을 제공하며 편집 시 NotSupportedException이다.

## 편집 데이터와 Shaper

Values는 `IDictionary<string, float>`, Handles는 `IList<SegmentHandle<P>>`이다. PolySegment는 사전 항목 추가·설정·제거·전체 해제, Handle 추가·삽입·교체·제거와 Position 변경을 감지한다. 입력·끝점·Shaper 변경은 동기 재계산을 수행하며, Length와 Hinges를 완성한 뒤 Shape를 알린다. 같은 값·같은 객체 재설정과 실제 변경이 없는 제거·비우기는 알리지 않는다. Shaper를 교체해도 기존 입력은 보존한다.

`SegmentHandle<P>`는 Position과 EventHub를 가진 객체이며 그래프의 Node가 아니다. Planar.SegmentHandle은 유한한 Vector2 절대 위치를 보유한다. 끝점 노드가 움직여도 중간 Handle은 이동하지 않는다. 한 Handle을 여러 구간이나 같은 구간의 여러 위치에서 공유할 수 있으며, 각 구간은 한 번만 구독한다. 마지막 참조 제거 또는 구간 폐기 시 구독을 해제하고 Handle을 폐기하지 않는다. Handle 폐기는 해당 입력을 사용하는 형상을 무효로 만들며, 그 Handle을 제거·교체하면 회복한다.

IShaper의 TryShape는 양끝점 위치·읽기 전용 Values·Handle 위치열과 출력 컬렉션을 받는다. 성공 시 양끝점을 포함한 위치열을 출력에 추가하고, 만들 수 없으면 false를 반환한다. 기본 Shaper들은 실패 시 출력 컬렉션을 변경하지 않는다. PolySegment는 결과의 양끝점과 수치 유효성을 확인한 뒤 Hinge를 계산한다. Shaper는 Position만 만들며 Rotation·Distance 계산은 Planar.PolySegment에 둔다.

| Planar.Shapers 구현 | 입력과 형상 |
| --- | --- |
| FreeHandle | 기본 Shaper. From·모든 Handle·To를 순서대로 연결하고 각 조각을 균등 분할 |
| Bezier2 | 양끝점과 Handle 1개로 이차 베지어 생성 |
| Bezier3 | 양끝점과 Handle 2개로 삼차 베지어 생성 |

세 Shaper는 `ShaperValues.TargetSegmentLength`를 공통 키로 사용한다. 값은 인접 Hinge 사이의 목표 길이이며 미지정 기본값은 1이다. 0 이하·NaN·무한대 설정은 변경 전에 ArgumentOutOfRangeException으로 거부한다. 키를 제거하면 기본값으로 돌아간다. FreeHandle은 모든 제어점 위치를 보존하고 긴 간격을 세분화한다. Bezier는 제어 다각형 길이를 기준으로 반복 이분할하며, 위치열을 직선으로 연결한 근사를 제공한다.

매우 작은 목표 길이에 대한 무한 분할과 과도한 할당을 방지하기 위해 기본 Shaper의 생성 점 수는 65,536개, Bezier 분할 깊이는 32까지이다. 한도를 넘거나 float 누적 거리가 증가하는 값을 표현할 수 없으면 형상 생성이 실패한다. 목표 길이를 늘리거나 좌표 크기를 조절하면 회복할 수 있다. Circular와 다른 Segment를 원본으로 받는 Offset은 후속 범위이다.

## Hinge와 PolySegment

`Hinge<P, R>`는 Position·Rotation·Distance를 가진 불변 값이며 시작점과 끝점을 포함한다. Distance는 From부터 각 직선 조각의 실제 길이를 누적한 값이다. 첫 값은 0이고 마지막 값은 Length이다. Rotation은 다음 Hinge로 향하는 방향이며 마지막 Hinge는 직전 Hinge의 Rotation을 사용한다.

Hinges는 직접 편집할 수 없는 목록이며 입력 변경 후 최신 결과를 반영한다. 연속 중복 위치는 계산용 점열에서만 합치며 원본 Handles는 유지한다. 끝점 누락, 필요한 Handle 수 불일치·폐기, 비유한 결과 또는 모든 점이 같은 위치인 경우 Hinges를 비우고 Length를 0으로 만든다. 입력이 유효해지면 다시 생성한다. 사용자 Shaper의 예외는 호출자에게 전달하며 기존 형상은 비우고 무효 상태를 알린다.

조회는 누적 거리에서 해당 조각을 찾아 선형 보간한다. 정확한 내부 Hinge에서 정방향은 다음 조각의 Rotation, 역방향은 이전 조각의 Rotation에 180도를 더한 방향을 반환한다. 양끝에서는 유일한 인접 조각을 사용한다. 이 규칙은 곡선의 해석적 접선이 아니라 실제 Hinge 점열의 진행 방향을 뜻한다.

## Location

Location은 IsForward·DistanceType·Distance를 갖는 불변 값이다. IsForward가 true이면 조회 대상 구간의 From에서 To를 향해 거리를 센다. false이면 To에서 From을 향해 센다. Length는 실제 거리, Rate는 전체 길이에 대한 비율이다. default(Location)은 정방향·Length·0이다.

길이 10인 구간에서 Length 2·정방향은 From으로부터 2, 역방향은 From으로부터 8인 위치이다. Rate 0.2도 같은 의미이다. 역방향 조회는 위치뿐 아니라 반환되는 회전도 180도 반전한다. ToDistance·ToRate는 From 기준 좌표로 환산하며, 범위 검사는 구간의 Try 조회에서 수행한다.

## 형상과 조회 실패

노드 이동은 길이가 같더라도 Shape 변경이다. 계산 구간은 위치·길이·회전을 먼저 갱신한 뒤 알린다. Pin 같은 파생 위치의 간접 자기 참조는 변경 전에 거부한다.

끝점 누락, 영길이·허용 오차 이하 길이, 구간 밖 거리, NaN·무한대 조회 값은 Try 조회가 false와 기본 출력값을 반환한다. 경계에서 Epsilon 이내의 오차는 끝점으로 제한한다. Epsilon은 UniMath.Epsilon을 기본값으로 하며 객체별 또는 호출별로 지정할 수 있다. 음수·비유한 Epsilon은 ArgumentOutOfRangeException이다. Node의 비유한 위치 설정도 거부한다. 폐기 객체의 조회는 ObjectDisposedException이다.

TraversalOptions는 탐색만 제한하며 공간 조회와 연결 편집에 영향을 주지 않는다. Geometry 계약도 Navigation에서 IPropertySubject를 이어받고, 기반 클래스가 제공하는 객체별 Property를 사용한다. 저장소 수명과 직접 구현자의 추가 계약은 [Navigation](Navigation.md)을 따른다.

## 위치 기반 탐색

`IPathfinder<P>`·`Path<P>`·`Arrow<P>`는 Navigation의 대응 타입을 확장하며 위치 타입이 지정된 입력·결과를 제공한다. 탐색에는 회전 타입 R이 필요하지 않다. `Geometry.Pathfinders.AStar<P>`는 생성자로 `Func<P, P, float>`을 받고 여러 목표 중 최소 추정값을 사용한다. 최소 비용을 보장하려면 휴리스틱이 실제 남은 비용을 과대평가하지 않아야 한다. 알고리즘·결과·예외 계약은 [경로 탐색](Pathfinding.md)을 따른다.

[상위 구성](README.md)
