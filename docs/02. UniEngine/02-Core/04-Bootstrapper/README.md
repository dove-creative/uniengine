# Bootstrapper

호스트 환경을 받아 UniEngine의 공통 환경과 애플리케이션을 순서대로 초기화하는 Core Level 4 모듈이다.

## 목차

- 소개
- 구성과 역할
- 대표 흐름
- 상세 문서

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 프로그램 실행 전에 호출하는 단일 초기화 진입점이다. |
| 목적 | 호스트가 설정·예외 정책·로그·기본 틱의 준비 순서를 반복 구현하지 않게 한다. |
| 구현 | 정적 Bootstrapper가 초기화 상태를 관리하며 공통 환경을 준비한 뒤 호스트의 동기 콜백을 실행한다. |

## 구성과 역할

Configuration은 설치 경로와 데이터 저장 경로를 보관한다. ExceptionHandler는 일반 실행의 예외 처리 정책을, Log는 출력기와 로그 버퍼를, EventStream.Main은 기본 틱 전달을 담당한다. Bootstrapper는 이들을 준비하는 순서와 초기화 실패 처리를 맡는다.

하위 모듈은 Bootstrapper에 의존하지 않는다. 호스트는 Initialize가 정상 반환한 뒤에 실행 루프로 진입한다. 시간 계산·틱 발행·프로그램 종료와 애플리케이션 자원 관리는 호스트의 책임이다.

## 대표 흐름

1. 설치 경로·애플리케이션 ID를 확인하고 Configuration을 확정한다.
2. 예외 정책을 연결하고 Log와 EventStream.Main을 준비한다.
3. 호스트의 초기화 콜백을 실행하고 정상 반환하면 초기화 완료 상태로 바꾼다.
4. 호스트가 실행 루프를 시작한다. 초기화 실패는 Bootstrapper가 Report를 호출하지 않고 외부로 전파한다.

## 상세 문서

| 문서 | 내용 |
| --- | --- |
| [Bootstrapper](Bootstrapper.md) | 입력 기본값, 초기화 상태와 실패 계약이다. |
| [사용 예제](Usage.md) | 콘솔 기본값과 사용자 로거·예외 정책·초기화 콜백을 사용하는 흐름이다. |
| [상태·동작 표](../../../../tests/02-Core/04-Bootstrapper/docs/01.%20Tables.md) | 초기화·실패·재진입의 검증 기준이다. |
| [테스트 계획](../../../../tests/02-Core/04-Bootstrapper/docs/02.%20Plans.md) | 프로세스 격리와 실행 방법이다. |
| [검증 결과](../../../../tests/02-Core/04-Bootstrapper/docs/03.%20Results.md) | 실행 결과와 확인 범위이다. |

[Core 구성](../README.md)
