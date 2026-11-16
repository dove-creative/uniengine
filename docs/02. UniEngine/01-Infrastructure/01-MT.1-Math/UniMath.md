# UniMath

UniMath는 실수·벡터의 오차 기반 비교와 보정, 방향·회전 관계 판정을 제공하는 무상태 수학 도구이다.

## 목차

- 역할과 구성
- 오차와 실패 계약
- 비교와 보정
- 방향과 회전

## 역할과 구성

공개 타입은 BlackThunder.UniEngine.Mathematics의 정적 UniMath 클래스이며 벡터·쿼터니언은 System.Numerics 타입을 사용한다. 추가 수학 패키지와 Unity 의존성은 없다.

UniMath는 값을 보관하거나 수정하지 않는다. 비교는 bool을 반환하고 보정은 새 값을 반환한다. 근사 비교는 허용 오차에 따른 판정이므로 Equals·GetHashCode·사전 키 동등성을 대체하지 않는다.

## 오차와 실패 계약

| 용도 | 기준 | 기본값 |
| --- | --- | --- |
| 실수 비교·SnapTo | 입력값과 같은 단위의 절대 차이 | Epsilon = 0.00001f |
| 벡터 비교 | 좌표와 같은 단위의 유클리드 거리 | Epsilon = 0.00001f |
| 평행 판정 | 정규화한 내적의 절대값과 1의 차이, 무차원 | Epsilon = 0.00001f |
| 회전 판정 | 두 회전 사이의 최단 각도, 도 단위 | AngularEpsilon = 0.00001f |

허용 오차는 유한한 0 이상 값이며 먼저 검사한다. 음수·NaN·무한대이면 ArgumentOutOfRangeException이 발생한다. 상대 오차는 적용하지 않는다. 실수·벡터의 Is와 IsZero는 경계를 포함하며 오차 0에서도 같은 유한 값은 참이다. 거리와 정규화는 큰 값의 제곱 오버플로와 작은 값의 제곱 언더플로로 판정이 바뀌지 않도록 double 중간 계산을 사용한다.

일반 실수·벡터 비교의 피연산자에 NaN·무한대가 있으면 거짓이다. 방향·회전 비교에서 영벡터·영쿼터니언 또는 비유한 성분이 있으면 방향을 정의할 수 없으므로 ArgumentException이 발생한다. 허용 오차 0은 계산 결과의 정확한 일치를 요구하며 서로 다르게 표현된 방향·회전의 반올림 차이까지 허용하지 않는다.

## 비교와 보정

Is와 IsZero는 float 및 Vector2·Vector3·Vector4를 지원한다. 벡터는 성분별 허용 범위가 아니라 전체 거리를 비교한다. Vector4.IsZero도 생략한 epsilon에 Epsilon을 사용한다.

부호와 대소 판정은 기존 tolerant 정책을 유지한다. 아래 식에서 v는 값, c는 기준값, e는 허용 오차이다. 값·기준값이 비유한 경우에는 식과 관계없이 거짓이다.

| 판정 | tolerant = false | tolerant = true |
| --- | --- | --- |
| IsPositive | v > e | v > -e |
| IsNegative | v < -e | v < e |
| IsMoreThan | v - e >= c | v + e >= c |
| IsLessThan | v <= c - e | v <= c + e |

Snap은 값을 단위의 배수로 반올림하며 중간값은 짝수 배수를 선택한다. 단위는 유한한 양수여야 하며 Epsilon보다 작은 값도 허용한다. 잘못된 단위에는 ArgumentOutOfRangeException이 발생한다. Vector2·Vector3에는 공통 단위 또는 성분별 단위를 지정할 수 있다. 값의 NaN·무한대와 반환 float의 표현 범위를 벗어난 결과에는 .NET 부동소수점 규칙을 적용한다.

SnapTo는 기준값과의 절대 차이가 허용 오차 이하면 기준값과 isSnapped = true를 반환한다. 그 밖에는 원래 값과 false를 반환한다. 비유한 피연산자도 보정하지 않는다.

## 방향과 회전

IsParallelWith는 두 Vector2를 정규화하여 평행 여부를 판정한다. 평행하면서 내적이 양수이면 isSameDirection = true이고, 반대 방향 또는 비평행이면 false이다. 영벡터는 정확히 길이가 0인 값이며 작은 비영 방향도 사용할 수 있다.

IsSameRotationAs는 두 Quaternion이 같은 회전을 나타내는지, IsHalfTurnFrom은 회전 차이가 180도인지 판정한다. 입력은 내부적으로 정규화하되 원본을 수정하지 않으며 q와 -q는 같은 회전으로 취급한다. 각도 경계는 포함한다. AngularEpsilon은 도 단위의 독립된 상수이며 기본 허용 오차는 0.00001도이다.

기존 Unity 구현의 Quaternion.Is와 allowInverse·isSameDirection 오버로드는 제공하지 않는다. 기존의 180도 차이 허용은 IsHalfTurnFrom으로 표현한다. 이는 쿼터니언의 역원이나 특정 방향 벡터의 반대 방향을 검사하는 API가 아니다.

[사용 예시](Usage.md)

[모듈 구성](README.md)
