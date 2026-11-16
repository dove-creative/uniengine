# Geometry

Geometry는 Navigation 연결을 공간 구간으로 확장하고, Location을 실제 위치와 방향으로 해석한다.

## 목차

- 공통 계약과 Planar
- Location
- 형상과 조회 실패

## 공통 계약과 Planar

공통 IReadOnlyNode·INode와 IReadOnlySegment·ISegment는 Navigation 계약을 확장하며 서로의 참조 형식을 좁힌다. ILinearSegment는 직선 구간의 분류 계약이다. 이 계층에는 위치·회전 제네릭이나 수치 계산이 없다.

Planar.IPositioned는 Vector2 Position을 제공한다. Planar.Node는 위치를 편집할 수 있고, Planar.LinearSegment는 양끝 노드를 연결하여 직선 길이·위치·방향을 계산한다. Planar.IReadOnlySegment는 TryGetPosition과 TryGetTransform을 제공하며 Transform2D의 Rotation은 도 단위이다. +X 방향이 0도이고 +Y 방향이 90도이다.

## Location

Location은 IsForward·DistanceType·Distance를 갖는 불변 값이다. IsForward가 true이면 조회 대상 구간의 From에서 To를 향해 거리를 센다. false이면 To에서 From을 향해 센다. Length는 실제 거리, Rate는 전체 길이에 대한 비율이다. default(Location)은 정방향·Length·0이다.

길이 10인 구간에서 Length 2·정방향은 From으로부터 2, 역방향은 From으로부터 8인 위치이다. Rate 0.2도 같은 의미이다. 역방향 조회는 위치뿐 아니라 반환되는 회전도 180도 반전한다. ToDistance·ToRate는 From 기준 좌표로 환산하며, 범위 검사는 구간의 Try 조회에서 수행한다.

## 형상과 조회 실패

노드 이동은 길이가 같더라도 Shape 변경이다. 계산 구간은 위치·길이·회전을 먼저 갱신한 뒤 알린다. Pin 같은 파생 위치의 간접 자기 참조는 변경 전에 거부한다.

끝점 누락, 영길이·허용 오차 이하 길이, 구간 밖 거리, NaN·무한대 조회 값은 Try 조회가 false와 기본 출력값을 반환한다. 경계에서 Epsilon 이내의 오차는 끝점으로 제한한다. Epsilon은 UniMath.Epsilon을 기본값으로 하며 객체별 또는 호출별로 지정할 수 있다. 음수·비유한 Epsilon은 ArgumentOutOfRangeException이다. Node의 비유한 위치 설정도 거부한다. 폐기 객체의 조회는 ObjectDisposedException이다.

[상위 구성](README.md)
