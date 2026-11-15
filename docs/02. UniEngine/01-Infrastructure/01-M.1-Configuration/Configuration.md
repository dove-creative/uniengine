# Configuration

애플리케이션별 설치 경로와 데이터 저장 루트 경로를 한 번 확정하는 정적 설정 객체이다.

## 목차

- 역할과 구성
- 동작 흐름
- 설계 기준

## 역할과 구성

- 책임: 저장 위치를 한곳에서 결정하여 로그 등 여러 모듈이 같은 애플리케이션 데이터 영역을 사용하게 한다.

보유 정보는 다음과 같다.

- DataPath: 초기화가 완료된 데이터 저장 경로이다.
- InstallationPath: 프로그램 리소스를 찾는 기존 설치 디렉터리의 절대 경로이다.
- 초기화 잠금: 동시에 들어온 초기화 요청이 서로 다른 경로를 확정하지 못하게 한다. 경로 조회에는 사용하지 않는다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| Initialize | 애플리케이션 ID를 검증하고 폴더를 준비하여 소비자가 경로 생성 규칙을 반복하지 않게 한다. |
| DataPath 조회 | 확정된 경로를 공유하며, 초기화되지 않은 설정을 조기에 드러낸다. |
| InstallationPath 조회 | 설치된 리소스의 위치를 데이터 저장 위치와 구분하여 제공한다. |

## 동작 흐름

1. 애플리케이션이 Initialize에 자신의 ID와 선택적인 설치 경로를 전달한다. 기존 단일 인자 호출은 설치 경로로 AppContext.BaseDirectory를 사용한다.
2. 설치 경로를 절대 경로로 정규화하고 기존 디렉터리인지 확인한다. null은 기본값이며 빈 값·공백·존재하지 않는 디렉터리·파일 경로는 거부한다. 상대 경로는 현재 작업 디렉터리를 기준으로 해석한다.
3. 사용자 문서 폴더 아래 `BlackThunder/UniEngine/Projects/<applicationId>`를 준비하고 두 경로를 함께 확정한다.
4. [Log](../03-M.3-LogSystem/Log.md) 등 소비자가 DataPath를 읽어 저장 위치를 정하고 리소스 소비자는 InstallationPath를 읽는다.

## 설계 기준

- DataPath는 실행 중 생성하는 데이터 위치이며 InstallationPath와 구분한다. 설치 디렉터리를 만들거나 기존 파일을 수정하지 않는다.
- 두 속성은 초기화 전 조회 시 InvalidOperationException이다. 두 경로를 변경 불가능한 객체에 담아 초기화 성공 시 volatile 참조 하나로 공개한다. getter는 이 참조를 읽으며 lock을 사용하지 않는다. 다른 스레드에서도 한 경로만 확정된 상태를 노출하지 않는다.
- 성공한 초기화의 재호출은 InvalidOperationException이다. 설치 경로 검증이나 데이터 폴더 준비에 실패하면 두 경로 모두 미확정 상태를 유지하며 원인을 수정한 뒤 다시 초기화할 수 있다.
- 단일 인자 Initialize(string applicationId)와 두 인자 Initialize(string applicationId, string installationPath)를 제공한다. 호스트의 전체 초기화 흐름은 [Bootstrapper](../../02-Core/04-Bootstrapper/README.md)에서 조율한다.

[모듈 구성](README.md)
