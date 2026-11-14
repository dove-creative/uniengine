# Transform3D

3차원 위치와 회전을 함께 전달하는 가변 값 형식이다.

## 목차

- 역할과 구성
- 동작 흐름
- 설계 기준

## 역할과 구성

- 책임: 위치와 쿼터니언 회전을 하나의 자세 값으로 다루어 전달·비교의 기준을 통일한다.

보유 정보는 다음과 같다.

- Position: System.Numerics.Vector3로 표현한 위치이다.
- Rotation: Quaternion으로 표현한 회전이다.
- Identity: 원점과 Quaternion.Identity로 구성된 기준 자세이다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| 생성자·성분 접근 | 자세를 구성하고 위치·회전을 갱신할 수 있게 한다. |
| Is | 위치 거리와 회전각에 별도 허용 오차를 적용하여 같은 자세인지 판정한다. |
| 동등성·해시 | 두 자세의 저장 성분을 값으로 비교할 수 있게 한다. |
| ToString | 위치와 회전을 함께 표시하여 자세를 진단할 수 있게 한다. |

## 동작 흐름

호출자가 위치와 쿼터니언을 하나의 값으로 저장·복사한다. [Transform3DJsonConverter](../03-M.1-Utilities/Json/Transform3DJsonConverter.md)는 위치와 회전을 각각의 성분 객체로 변환하여 저장한다.

## 설계 기준

- 기본값 구조체의 회전은 영 쿼터니언이므로 Identity와 다르다.
- Is의 positionTolerance는 위치 거리, angularTolerance는 최단 회전각의 라디안 오차이다. 기본값은 각각 [UniMath](../01-MT.1-Math/UniMath.md)의 Epsilon·AngularEpsilon이며 경계를 포함한다. 회전은 IsSameRotationAs로 비교하므로 q와 -q, 같은 회전을 나타내는 비단위 쿼터니언도 일치한다.
- 허용 오차는 유한한 0 이상 값이어야 하며 잘못된 값에는 ArgumentOutOfRangeException이 발생한다. 비유한 위치는 거짓이고 영 쿼터니언·비유한 회전에는 ArgumentException이 발생한다. 위치가 달라도 회전과 두 오차를 검사한다. 따라서 기본값 Transform3D는 Is로 비교할 수 없으며 유효한 기준 자세에는 Identity를 사용한다.
- Equals·연산자·해시는 저장 성분의 정확한 동등성을 유지한다. Is는 사전·집합의 키 동등성으로 사용하지 않는다. 비교 중 정규화는 계산에만 적용하며 저장된 위치·회전을 수정하지 않는다.
- 회전을 자동 정규화하지 않는다. 배율·부모 관계·계층 변환을 포함하지 않으며 값의 해석은 소비자가 담당한다.

[모듈 구성](README.md)
