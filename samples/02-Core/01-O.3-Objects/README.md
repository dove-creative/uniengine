# Core Objects 스니펫

IEnumerationSource를 구현한 컬렉션을 ValueEnumerator로 순회하는 스니펫이다.

## 코드

[CoreObjectsSnippet.cs](CoreObjectsSnippet.cs)에 코드가 있다. 결과는 각 줄의 주석에 적었다.

## 내용

- Countdown은 IEnumerationSource를 구현하여 순회마다 별도의 커서를 관리한다. ValueEnumerator는 시작·이동·종료만 요청한다.
- 첫 MoveNext에서 BeginEnumeration이 호출되고, 끝에 도달하거나 열거자를 폐기하면 EndEnumeration이 호출된다.
- foreach는 GetEnumerator가 반환한 구조체 열거자를 직접 사용하므로 열거 자체에서 박싱이 생기지 않는다.
- break로 일찍 빠져나와도 열거자 폐기를 통해 순회가 종료된다.

## 관련 문서

- [Core Objects 모듈](../../../docs/02.%20UniEngine/02-Core/01-O.3-Objects/README.md)
- [Interfaces 모듈](../../../docs/02.%20UniEngine/02-Core/01-O.2-Interfaces/README.md)
- [예시 목록](../../README.md)
