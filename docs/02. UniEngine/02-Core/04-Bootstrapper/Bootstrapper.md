# Bootstrapper

정적 Bootstrapper는 프로그램의 공통 환경을 초기화하고 호스트의 동기 초기화 콜백을 실행한다. IsInitialized는 모든 단계가 성공했을 때만 true이다.

## 입력과 기본값

| Initialize 입력 | 기본값과 의미 |
| --- | --- |
| installationPath | null이면 AppContext.BaseDirectory이다. 기존 디렉터리를 절대 경로로 정규화한다. 상대 경로는 현재 작업 디렉터리를 기준으로 해석하며 설치 디렉터리는 생성하지 않는다. |
| logger | null이면 Log의 콘솔 출력기를 사용한다. Action&lt;string, LogLevel&gt;로 메시지와 수준을 받는다. |
| exceptionHandler | null이면 ExceptionHandler의 기본 재전파 정책이다. 기존 정책이 있다면 전달한 정책으로 교체한다. |
| applicationId | null이면 진입 어셈블리 이름이다. 이름을 얻을 수 없는 호스트는 명시해야 한다. 빈 값과 잘못된 디렉터리 이름은 거부한다. |
| initialize | 선택적인 동기 Action이다. Main이 준비된 뒤 호출하며 애플리케이션 객체 생성과 구독 등록을 수행할 수 있다. |

기본값은 특정 호스트에 의존하지 않는 순수 .NET 환경을 기준으로 한다. 설치 경로는 Configuration.InstallationPath로, 사용자 문서 아래의 데이터 저장 경로는 Configuration.DataPath로 제공한다. 경로와 파일의 관리 기준은 [Configuration](../../01-Infrastructure/01-M.1-Configuration/Configuration.md)을 따른다.

## 초기화와 실행

순서는 입력 확인 → Configuration → 예외 처리 정책 연결 → Log → EventStream.ResetMain → 애플리케이션 초기화 → 완료이다. 콜백 실행 중에는 IsInitialized가 false이며, 콜백이 정상 반환한 뒤 true가 된다. Initialize는 반환값 없이 성공하거나 예외를 전파한다.

Bootstrapper는 한 프로세스의 공통 환경을 준비한다. 초기화 중 동시 호출·재진입과 완료 후 중복 호출은 InvalidOperationException이다. 현재 스레드에 EventDispatchScope가 있으면 설정을 변경하기 전에 거부하며, 동기·병렬·완료·오류 콜백에 같은 규칙을 적용한다.

호스트는 Initialize가 정상 반환한 뒤 실행 루프에서 Main에 초 단위 경과 시간을 발행한다. 하위 모듈에 Bootstrapper를 참조하는 실행 검사를 추가하지 않으며, Bootstrapper가 루프나 타이머를 실행하지 않는다.

## 실패와 소유권

입력 확인이나 Configuration 초기화에 실패하면 미초기화 상태를 유지하고 예외를 전파한다. 원인을 수정한 뒤 다시 호출할 수 있다. 이미 직접 초기화한 Configuration을 Bootstrapper가 인수하는 방식은 지원하지 않는다.

Configuration 확정 이후 실패하면 실패 상태로 고정한다. IsInitialized는 false이며 같은 프로세스에서 재시도할 수 없다. Configuration을 되돌리거나 저장 디렉터리를 삭제하지 않으며, 연결된 예외 정책과 로그 버퍼를 진단에 사용할 수 있게 유지한다.

Main을 준비한 뒤 콜백이 실패하면 Bootstrapper가 준비했던 그 Main을 폐기한다. 콜백이 만든 애플리케이션 자원은 콜백·호스트가 정리한다. 단일 실패는 원래 예외와 스택을 보존해 전파한다. Main 정리도 실패하면 최초 예외, 정리 예외 순서의 AggregateException을 전달한다.

Bootstrapper는 초기화 예외를 ExceptionHandler.Report로 보고하지 않는다. 연결한 정책은 일반 실행에 사용한다. 콜백이나 정리 대상 자체가 Report를 사용하는 기존 동작은 유지하며, Bootstrapper가 그 예외를 추가로 보고하지 않는다.

[모듈 구성](README.md)
