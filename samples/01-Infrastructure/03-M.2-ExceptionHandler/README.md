# ExceptionHandler 스니펫

예외를 처리기에 보고하고, 일부 처리기가 실패해도 나머지 대리자를 계속 호출하는 스니펫이다.

## 코드

[ExceptionHandlerSnippet.cs](ExceptionHandlerSnippet.cs)에 코드가 있다. 결과는 각 줄의 주석에 적었다.

## 내용

- 처리기는 프로세스 전체가 공유하며, 호스트는 보통 Bootstrapper.Initialize로 지정한다.
- SafeInvoke는 멀티캐스트 대리자를 하나씩 호출한다. 실패한 처리기는 보고하고 나머지를 계속 호출한다.
- Report는 등록된 처리기에 예외를 전달한다. 처리기가 없으면 예외를 다시 던진다.

## 관련 문서

- [ExceptionHandler 모듈](../../../docs/02.%20UniEngine/01-Infrastructure/03-M.2-ExceptionHandler/README.md)
- [예시 목록](../../README.md)
