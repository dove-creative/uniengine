# Event Hub

도메인 객체의 연결과 생애주기를 이벤트 전달 경로에 연결하는 모듈이다.

## 목차

1. 소개
2. 구성과 역할
3. 대표 흐름
4. 상세 문서

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 객체별 연결과 생애주기에 연동한 이벤트 전달이다. |
| 목적 | 도메인 객체가 수신자 관계를 관리하면서 갱신 전파 구현을 별도로 반복하지 않게 한다. |
| 구현 | 허브가 객체별 연결과 생애주기를 관리하고 갱신 전달은 내부 스트림에 위임한다. |

## 구성과 역할

Subject는 허브를 제공하는 도메인 객체의 계약이다. 수신자는 Hub 계약으로 연결하고 Builder로 알림을 구성한다. EventHub는 갱신과 수면·폐기를, LifecycleHub는 폐기 완료만 관리한다. 내부 스트림은 EventHub의 연결 수신자와 직접 구독자의 실행 단계를 담당한다.

## 대표 흐름

1. 소유자가 허브를 준비하고 수신자가 객체 단위로 연결한다.
2. Builder로 등록한 콜백을 내부 전달 경로에 연결하여 갱신을 전파한다.
3. 수신자 해제 또는 소유자 폐기 시 해당 연결과 보관 참조를 정리한다.

## 상세 문서

| 객체·계약 | 역할 |
| --- | --- |
| [EventHub](EventHub.md) | 객체별 연결과 생애주기를 관리하고 내부 스트림을 통해 갱신을 전달하는 허브이다. |
| [EventHubBuilder](EventHubBuilder.md) | EventHub에 등록한 수신자 하나의 콜백을 구성하는 객체이다. |
| [LifecycleHub](LifecycleHub.md) | 갱신 이벤트 없이 객체의 폐기 완료만 연결된 수신자에게 알리는 허브이다. |
| [ILifecycleAndEventHub](ILifecycleAndEventHub.md) | 수신자가 소유자의 허브에 연결하고 알림을 관찰할 수 있게 하는 계약 묶음이다. |
| [ILifecycleAndEventSubject](ILifecycleAndEventSubject.md) | 생애주기 또는 이벤트 허브를 제공하는 도메인 객체의 계약 묶음이다. |
| [ILifecycleAndEventHubBuilder](ILifecycleAndEventHubBuilder.md) | 연결된 수신자의 알림을 단계적으로 설정하는 구성 계약 묶음이다. |
| [HubCallbacks](HubCallbacks.md) | 허브의 직접 생애주기 구독을 개별 항목으로 관리하고 격리 실행하는 내부 객체이다. |

[Core 구성](../README.md)
