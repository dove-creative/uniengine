# VectorFunctions 스니펫

IVectorFunction 계약으로 직선과 2차 베지어 곡선의 위치를 평가하는 스니펫이다.

## 코드

[VectorFunctionsSnippet.cs](VectorFunctionsSnippet.cs)에 코드가 있다. 결과는 각 줄의 주석에 적었다.

## 내용

- AffineLine과 QuadraticBezier를 같은 IVectorFunction 계약으로 평가한다.
- t가 0과 1 사이를 벗어나도 평가하며, 곡선은 외삽한 위치를 반환한다.
- normalize를 지정하면 방향 벡터를 단위 길이로 바꾼다. 0 벡터의 정규화는 InvalidOperationException으로 거부한다.

## 관련 문서

- [VectorFunctions 모듈](../../../docs/02.%20UniEngine/01-Infrastructure/01-MT.2-VectorFunctions/README.md)
- [Math 사용 예시](../../../docs/02.%20UniEngine/01-Infrastructure/01-MT.1-Math/Usage.md)
- [예시 목록](../../README.md)
