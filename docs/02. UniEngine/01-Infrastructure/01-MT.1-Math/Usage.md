# Math 사용 예시

## 목차

- 비교와 보정
- 방향과 회전
- 위치 함수
- Unity 코드 이전

## 비교와 보정

UniMath와 벡터 함수 타입은 Blackthunder.UniEngine.Mathematics에 있고 벡터·쿼터니언은 System.Numerics 타입이다.

```csharp
using System.Numerics;
using Blackthunder.UniEngine.Mathematics;

var close = 1f.Is(1.125f, 0.125f); // true, including the boundary
var zero = Vector4.Zero.IsZero(); // true
var grid = new Vector2(2.5f, -3.5f).Snap(1f); // (2, -4)
var value = 1.125f.SnapTo(1f, 0.125f, out var snapped); // 1, true
```

근사 비교의 기준은 [UniMath](UniMath.md)에서 확인한다.

## 방향과 회전

```csharp
var parallel = Vector2.UnitX.IsParallelWith(-Vector2.UnitX, out var sameDirection);
// parallel = true, sameDirection = false

var identity = Quaternion.Identity;
var sameRotation = identity.IsSameRotationAs(Quaternion.Negate(identity)); // true
var halfTurn = new Quaternion(0f, 1f, 0f, 0f);
var separatedByHalfTurn = identity.IsHalfTurnFrom(halfTurn); // true
var rotated = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.01f);
var withinAngle = identity.IsSameRotationAs(rotated, 0.02f); // true, radians
```

거리 오차와 각도 오차는 서로 다른 단위이다. IsParallelWith의 epsilon은 정규화한 내적의 차이이며, 회전 비교의 angularTolerance는 라디안이다.

## 위치 함수

```csharp
IVectorFunction line = new AffineLine(new Vector2(1f, 2f), new Vector2(4f, 6f));
var midpoint = line.Evaluate(0.5f); // (3, 5)

IVectorFunction curve = new QuadraticBezier(Vector2.Zero, new Vector2(2f, 4f), new Vector2(4f, 0f));
var onCurve = curve.Evaluate(0.5f); // (2, 2)
var extrapolated = curve.Evaluate(2f); // (8, -16)
```

[VectorFunctions](../01-MT.2-VectorFunctions/README.md)에서 가변 프로퍼티와 정규화 실패 계약을 확인한다.

## Unity 코드 이전

- UniEngine.Math 대신 Blackthunder.UniEngine.Mathematics를 가져오고 UnityEngine 벡터·쿼터니언을 System.Numerics 타입으로 바꾼다. Unity 타입 변환 어댑터는 제공하지 않는다.
- Quaternion.Is의 기본 회전 동등성은 IsSameRotationAs로 옮긴다. 기존 allowInverse = true처럼 같은 회전 또는 180도 차이를 모두 허용하려면 두 판정 결과를 OR로 결합한다.
- 기존 Quaternion 비교의 epsilon은 도 단위였으므로 라디안으로 환산하여 angularTolerance에 전달한다. 새 기본값은 AngularEpsilon이며 기존 기본 각도 오차와 수치적으로 동일한 각도를 뜻하지 않는다.

[모듈 구성](README.md)
