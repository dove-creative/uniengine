# LogSystem 스니펫

값과 컬렉션을 로그로 기록하고, 버퍼에 모인 로그를 파일로 저장하거나 버리는 스니펫이다.

## 코드

[LogSystemSnippet.cs](LogSystemSnippet.cs)에 코드가 있다. 결과는 각 줄의 주석에 적었다.

## 내용

- Log는 각 항목을 호스트가 Bootstrapper.Initialize에 넘긴 logger로 전달한다.
- Write는 값을 문자열로, 컬렉션을 하나의 항목으로 기록한다. 인자 없는 Log.Write는 호출 지점을 'Called'로 남긴다.
- WriteCollection은 선택자로 바꾼 값을 기록한다.
- Log는 항목을 버퍼에도 보관한다. Print는 버퍼를 Configuration.DataPath의 파일로 저장한 뒤 비우고, Reset은 저장하지 않고 비운다.

## 관련 문서

- [LogSystem 모듈](../../../docs/02.%20UniEngine/01-Infrastructure/03-M.3-LogSystem/README.md)
- [예시 목록](../../README.md)
