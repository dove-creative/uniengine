# Log

진단 메시지를 출력기로 전달하고 버퍼에 모아 파일로 저장하는 정적 로그 객체이다.

## 목차

1. 역할과 구성
2. 동작 흐름
3. 설계 기준

## 역할과 구성

- 책임: 즉시 출력과 누적 기록을 같은 입력으로 처리하여 애플리케이션이 출력 방식과 파일 저장 시점을 선택하게 한다.

보유 정보는 다음과 같다.

- 출력기: 메시지와 LogLevel을 받는 대리자이며 기본값은 콘솔 출력이다.
- 버퍼와 LogWithStackTrace: 누적 메시지 및 스택 추적 포함 여부이다.
- 프로젝트 경로와 DebugFilePath: 초기화 때 확보한 저장 루트와 마지막으로 저장에 성공한 파일 경로이다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| Initialize | Configuration의 경로와 출력기를 연결하여 기록 환경을 준비한다. |
| Write·WriteCollection·WriteLine | 단일 값·컬렉션·줄 구분을 같은 버퍼에 모아 진단 내용을 구성한다. |
| Print | 누적 내용을 UTF-8 파일로 저장하고 반환하여 기록을 확정한다. |
| Reset | 파일 저장 없이 현재 버퍼만 비워 새 기록을 시작하게 한다. |

## 동작 흐름

1. [Configuration](../01-M.1-Configuration/Configuration.md)이 데이터 경로를 확정하고 Log.Initialize가 이를 확보한다.
2. Write가 메시지와 [LogLevel](LogLevel.md)을 버퍼에 추가한 뒤 외부 출력기를 호출한다. 컬렉션 표현은 [DataFormattingExtensions](../03-M.1-Utilities/DataFormattingExtensions.md)에 위임한다.
3. Print가 파일 저장을 완료하면 DebugFilePath를 갱신하고 버퍼를 비운다. 저장 실패 시에는 버퍼를 유지한다.

## 설계 기준

- 외부 출력기는 잠금 밖에서 호출하므로 출력기의 재진입과 버퍼 잠금을 분리한다. 출력기에서 발생한 예외는 호출자에게 전달된다.
- 파일명 충돌 시 번호를 붙여 기존 파일을 덮어쓰지 않는다.
- Initialize 재호출은 버퍼와 마지막 저장 경로를 초기화한다. 파일 저장에는 초기화로 확보한 경로가 필요하며, 빈 버퍼의 Print는 파일을 만들지 않는다.

[모듈 구성](README.md)
