# Core Utilities 스니펫

플래그 enum을 비트로 다루고, 보관한 토큰이 현재 토큰과 같은지 확인하는 스니펫이다.

## 코드

[CoreUtilitiesSnippet.cs](CoreUtilitiesSnippet.cs)에 코드가 있다. 결과는 각 줄의 주석에 적었다.

## 내용

- ToBits는 Flags 특성이 있는 enum을 ulong 비트로 바꾼다. 결과는 Combine·HasAny·HasAll과 함께 쓴다.
- int·uint·ulong 마스크는 Combine으로 여러 값을 한 번에 합칠 수 있다.
- IsValidAndSameTokenWith는 보관한 토큰이 있고 비교 대상과 같은 인스턴스일 때만 true이다. 비어 있는 칸은 null과 비교해도 false이다.

## 관련 문서

- [Core Utilities 모듈](../../../docs/02.%20UniEngine/02-Core/01-O.1-Utilities/README.md)
- [예시 목록](../../README.md)
