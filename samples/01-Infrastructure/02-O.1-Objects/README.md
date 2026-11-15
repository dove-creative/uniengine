# Objects 스니펫

정수 벡터와 2D·3D 변환의 정확한 비교와 근사 비교를 보여 주는 스니펫이다.

## 코드

[ObjectsSnippet.cs](ObjectsSnippet.cs)에 코드가 있다. 결과는 각 줄의 주석에 적었다.

## 내용

- Vector2Int·Vector3Int는 ==로 정확히 비교하고, Is로 거리 오차 안에 있는지 판정한다.
- Transform2D.Is는 한 바퀴 차이 나는 회전을, Transform3D.Is는 부호가 반대인 쿼터니언을 같은 자세로 본다.
- 변환의 ==는 값이 정확히 같을 때만 true이다.

## 관련 문서

- [Objects 모듈](../../../docs/02.%20UniEngine/01-Infrastructure/02-O.1-Objects/README.md)
- [예시 목록](../../README.md)
