# Utilities 스니펫

값을 읽기 쉬운 문자열로 바꾸고, UniEngine 값 형식을 JSON으로 저장·복원하는 스니펫이다.

## 코드

[UtilitiesSnippet.cs](UtilitiesSnippet.cs)에 코드가 있다. 결과는 각 줄의 주석에 적었다.

## 내용

- ToCompactString은 큰 수를 K·M 단위로 줄이고, ToDisplayString은 시간과 컬렉션을 표시 문자열로 만든다.
- AddUniEngineConverters는 UniEngine 값 형식과 System.Numerics 형식의 JSON 변환기를 등록한다. 같은 형식의 변환기가 이미 있으면 추가하지 않는다.
- 변환기는 객체의 프로퍼티로 들어 있는 값 형식에도 적용된다.

## 관련 문서

- [Utilities 모듈](../../../docs/02.%20UniEngine/01-Infrastructure/03-M.1-Utilities/README.md)
- [Json 변환기](../../../docs/02.%20UniEngine/01-Infrastructure/03-M.1-Utilities/Json/README.md)
- [예시 목록](../../README.md)
