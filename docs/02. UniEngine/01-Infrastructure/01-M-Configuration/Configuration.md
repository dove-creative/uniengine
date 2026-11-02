# Configuration

애플리케이션별 데이터 저장 루트 경로를 한 번 확정하는 정적 설정 객체이다.

## 목차

1. 역할과 구성
2. 동작 흐름
3. 설계 기준
4. 구현 연결

## 역할과 구성

- 책임: 저장 위치를 한곳에서 결정하여 로그 등 여러 모듈이 같은 애플리케이션 데이터 영역을 사용하게 한다.

보유 정보는 다음과 같다.

- ProjectPath: 초기화가 완료된 데이터 저장 경로이다.
- 초기화 잠금: 동시에 들어온 초기화 요청이 서로 다른 경로를 확정하지 못하게 한다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| Initialize | 애플리케이션 ID를 검증하고 폴더를 준비하여 소비자가 경로 생성 규칙을 반복하지 않게 한다. |
| ProjectPath 조회 | 확정된 경로를 공유하며, 초기화되지 않은 설정을 조기에 드러낸다. |

## 동작 흐름

1. 애플리케이션이 Initialize에 자신의 ID를 전달한다.
2. Configuration이 사용자 문서 폴더 아래 `BlackThunder/UniEngine/Projects/<applicationId>`를 준비하고 경로를 확정한다.
3. [Log](../03-M.3-LogSystem/Log.md) 등 소비자가 ProjectPath를 읽어 저장 위치를 정한다.

## 설계 기준

- 경로는 소스 저장소 위치가 아니라 실행 중 생성하는 애플리케이션 데이터 위치이다.
- 초기화 전 경로 조회와 성공한 초기화의 재호출은 InvalidOperationException이다. 폴더 준비에 실패한 경우에는 경로를 확정하지 않는다.

## 구현 연결

[소스](../../../../src/01-Infrastructure/01-M-Configuration/Configuration.cs) · [모듈 구성](README.md)
