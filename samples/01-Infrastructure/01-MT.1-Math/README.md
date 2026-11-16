# Math 스니펫

UniMath의 근사 비교·격자 보정과 방향·회전 판정을 보여 주는 스니펫이다.

## 코드

[MathSnippet.cs](MathSnippet.cs)에 코드가 있다. 결과는 각 줄의 주석에 적었다.

## 내용

- 근사 비교와 보정: Is, IsZero, Snap, SnapTo
- 방향과 회전: IsParallelWith, IsSameRotationAs, IsHalfTurnFrom
- 벡터와 쿼터니언은 System.Numerics 형식을 사용한다.
- IsParallelWith의 epsilon은 정규화한 내적의 차이이고, 회전 비교의 angularTolerance는 도 단위이다. System.Numerics의 회전 생성에는 라디안으로 변환한 각도를 전달한다.

## 관련 문서

- [Math 모듈](../../../docs/02.%20UniEngine/01-Infrastructure/01-MT.1-Math/README.md)
- [Math 사용 예시](../../../docs/02.%20UniEngine/01-Infrastructure/01-MT.1-Math/Usage.md)
- [예시 목록](../../README.md)
