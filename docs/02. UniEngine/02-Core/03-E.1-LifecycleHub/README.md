# Lifecycle Hub

객체별 연결과 폐기 완료 알림을 관리하는 생애주기 모듈이다.

## 목차

- 소개
- 구성과 역할
- 대표 흐름
- 상세 문서

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 갱신 이벤트가 필요 없는 객체의 종료를 연결된 수신자에게 알린다. |
| 목적 | 도메인 객체가 폐기 알림과 연결 참조 정리를 반복 구현하지 않게 한다. |
| 구현 | Subject가 허브를 제공하고 수신자가 Builder로 폐기 완료 반응을 구성한다. |

연결과 삭제의 무결성 원칙은 [객체 활동과 연결 원칙](../../../01.%20Concepts/03.%20객체%20활동과%20연결%20원칙.md)을 따른다.

## 구성과 역할

ILifecycleSubject는 허브를 제공하는 객체의 폐기 계약과 상태를 나타낸다. 수신자는 ILifecycleHub로 연결하고 ILifecycleHubBuilder로 완료 알림을 등록한다. 직접 OnDisposed 구독도 같은 사전을 사용하며 대리자 자체를 식별자로 삼는다. LifecycleHub는 등록된 Action에 폐기를 통지하고 보유 참조를 정리한다.

[EventHub](../03-E.2-EventHub/README.md)는 이 모듈의 생애주기 계약을 확장하여 갱신·필터·수면·폐기 전후 알림을 제공한다. 두 구현 클래스는 독립적이며, LifecycleHub는 하나의 식별자·Action 사전으로 연결과 직접 구독을 관리한다. LifecycleHub 모듈은 EventHub에 의존하지 않는다.

## 대표 흐름

1. 도메인 객체가 허브를 준비하고 수신자가 자신을 식별자로 연결한다.
2. 수신자가 Builder.OnDisposed로 완료 반응을 등록한다.
3. 소유자가 폐기되면 허브의 완료 상태를 먼저 확정하고 등록 경로 구분 없이 완료 반응을 통지한다.
4. 보관한 연결과 콜백 참조를 정리한다.

## 상세 문서

| 문서 | 내용 |
| --- | --- |
| [LifecycleHub](LifecycleHub.md) | 객체별 연결과 폐기 완료 알림의 책임과 동작이다. |
| [LifecycleHubBuilder](LifecycleHubBuilder.md) | 수신자의 완료 반응을 등록하는 구성 객체이다. |
| [ILifecycleHub](ILifecycleHub.md) | 수신자의 연결·해제와 폐기 상태 조회 계약이다. |
| [ILifecycleHubBuilder](ILifecycleHubBuilder.md) | 폐기 완료 반응의 구성 계약이다. |
| [ILifecycleSubject](ILifecycleSubject.md) | 생애주기 허브를 제공하는 도메인 객체의 계약이다. |
| [테스트 상태·동작 표](../../../../tests/02-Core/03-E.1-LifecycleHub/docs/01.%20Tables.md) | 연결·해제·폐기와 할당의 검증 기준이다. |
| [테스트 계획](../../../../tests/02-Core/03-E.1-LifecycleHub/docs/02.%20Plans.md) | 기준별 테스트와 실행 방법이다. |
| [공통 검증 결과](../../../../tests/02-Core/docs/Hub%20Results.md) | 두 Hub와 전체 테스트의 실행 기록이다. |

[Core 구성](../README.md)
