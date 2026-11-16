# Math 사용 예시

## 목차

- 비교와 보정
- 방향과 회전
- 위치 함수
- Unity 코드 이전
- 라디안 API에서 이전

## 비교와 보정

UniMath와 벡터 함수 타입은 BlackThunder.UniEngine.Mathematics에 있고 벡터·쿼터니언은 System.Numerics 타입이다.

```csharp
using System.Numerics;
using BlackThunder.UniEngine.Mathematics;

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
// System.Numerics takes radians; UniEngine's angular tolerance uses degrees.
var rotated = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)(0.01d * System.Math.PI / 180d));
var withinAngle = identity.IsSameRotationAs(rotated, 0.02f); // true, degrees
```

거리 오차와 각도 오차는 서로 다른 단위이다. IsParallelWith의 epsilon은 정규화한 내적의 차이이며, 회전 비교의 angularTolerance는 도 단위이다.

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

- UniEngine.Math 대신 BlackThunder.UniEngine.Mathematics를 가져오고 UnityEngine 벡터·쿼터니언을 System.Numerics 타입으로 바꾼다. Unity 타입 변환 어댑터는 제공하지 않는다.
- Quaternion.Is의 기본 회전 동등성은 IsSameRotationAs로 옮긴다. 기존 allowInverse = true처럼 같은 회전 또는 180도 차이를 모두 허용하려면 두 판정 결과를 OR로 결합한다.
- 기존 Quaternion 비교의 epsilon과 angularTolerance는 모두 도 단위이므로 명시적으로 지정한 오차는 그대로 전달한다. 기본값은 AngularEpsilon = 0.00001f이며 0.00001도를 뜻한다.

## 라디안 API에서 이전

- 이전 UniEngine API에 라디안으로 전달하던 Transform2D.Rotation과 명시적 angularTolerance는 180/π를 곱해 도 단위로 바꾼다. Transform2D JSON의 Rotation 숫자도 같은 변환이 필요하다.
- AngularEpsilon의 수치는 0.00001f로 유지되며 단위는 도이다. 이전 기본값 0.00001라디안과 같은 허용 범위가 필요하면 환산한 오차를 명시적으로 전달한다.
- System.Numerics의 각도 기반 쿼터니언 생성 API와 삼각함수의 라디안 계약은 그대로이다. 도 단위 각도를 전달할 때 π/180을 곱하고, Transform3D의 쿼터니언 성분은 변환하지 않는다.

[모듈 구성](README.md)
