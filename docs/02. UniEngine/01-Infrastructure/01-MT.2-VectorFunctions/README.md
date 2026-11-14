# VectorFunctions

벡터 함수는 매개변수 t에 대응하는 2차원 위치를 계산하는 객체이다.

## 목차

- 공통 계약
- 직선
- 이차 베지어
- 협력과 제약

## 공통 계약

Blackthunder.UniEngine.Mathematics.IVectorFunction은 Evaluate(float t)로 System.Numerics.Vector2를 반환한다. 소비자는 구체 곡선의 종류와 관계없이 같은 계약으로 위치를 얻는다. 호출은 보유 상태를 변경하지 않는다.

Configuration·Math와 같은 Infrastructure 레벨이며 두 모듈에 의존하지 않는다.

## 직선

AffineLine은 Origin과 DirectionVector를 보유하며 Origin + DirectionVector × t를 계산한다. 두 프로퍼티는 가변이고 변경된 값은 다음 평가부터 적용된다.

기본 생성자는 두 값을 영벡터로 설정한다. 값 생성자의 normalize는 기본적으로 false이며 true이면 방향을 정규화한다. Normalize는 원점을 보존하고 유한한 비영 방향 벡터를 단위 벡터로 바꾼다. 아주 작은 비영 값도 허용하며 double 중간 계산으로 크기에 따른 오버플로·언더플로를 피한다.

방향이 영벡터이거나 비유한 성분을 포함하면 Normalize는 InvalidOperationException을 발생시키고 프로퍼티를 바꾸지 않는다. 정규화를 요청하지 않은 영방향 직선은 원점 위치를 평가할 수 있다.

## 이차 베지어

QuadraticBezier는 시작점 P0, 제어점 P1, 끝점 P2를 보유하며 (1-t)²P0 + 2(1-t)tP1 + t²P2를 계산한다. 기본 생성자는 세 점을 영벡터로 설정하고 값 생성자는 전달된 점을 저장한다.

세 프로퍼티는 가변이며 다음 평가부터 반영된다. t = 0에서 P0, t = 1에서 P2를 반환하고 P1은 곡선의 형태를 결정한다.

## 협력과 제약

두 구현은 t를 0~1로 제한하지 않으며 음수와 1보다 큰 값의 외삽도 허용한다. 위치·제어점·t의 계산은 .NET float 연산 규칙을 따르며 비유한 값과 평가 결과의 오버플로를 별도로 보정하지 않는다.

곡선 길이 계산, 접선, 일정 속도 이동, 경로 구성은 이 계약의 범위가 아니다. 소비자는 필요한 t를 정하고 IVectorFunction으로 위치를 요청한다. 소비자가 얻은 위치를 비교·보정할 때는 별도의 [UniMath](../01-MT.1-Math/UniMath.md)를 사용할 수 있다.

[사용 예시](../01-MT.1-Math/Usage.md)

[상위 구성](../README.md)
