# UniEngine Core

객체 제어, 공통 계약, 데이터 표현과 이벤트 전파를 제공하는 기반 기능 계층이다.

## 목차

1. UniEngine Core
    - 1-1. 소개
    - 1-2. 계층별 책임
    - 1-3. 구현 범위
    - 1-4. 상세 문서

2. Event System
    - 2-1. 구성과 책임
    - 2-2. 대표 협력 흐름
    - 2-3. 실패와 폐기

## 1. UniEngine Core

### 1-1. 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 객체의 준비·제어·데이터 보관과 연결·전파 수명을 다룬다. |
| 목적 | 도메인 객체들이 독립적으로 활동하면서 필요한 관계를 맺고 정리할 수 있게 한다. |
| 구현 | 공통 계약과 재사용 객체 위에 컬렉션·스트림·허브의 책임을 차례로 구성한다. |

### 1-2. 계층별 책임

Level 1은 공통 도구와 계약, 그 계약으로 동작하는 재사용 객체를 제공한다. Level 2는 값 저장·페이로드·연결 해제·이벤트 전달의 개별 책임을 구성한다. Level 3은 이를 객체별 연결과 생애주기 관리로 결합한다.

Level 4의 Bootstrapper는 Configuration·예외 정책·Log·Main을 준비하고 애플리케이션의 초기화 콜백을 실행한다. 호스트는 초기화 성공 후 실행 루프를 시작하며 하위 모듈은 Bootstrapper에 의존하지 않는다.

예를 들어 IEnumerationSource는 순회 수명을 정의하고 ValueEnumerator는 그 계약을 호출한다. AdaptiveCollection은 저장 목록과 변경 정책을 소유한다. 각 층이 자신의 정보와 기능을 소유하므로 공통 열거자는 구체 컬렉션에 의존하지 않는다.

### 1-3. 구현 범위

공통 도구·계약·ValueEnumerator, Property, Handle·Token, AdaptiveCollections, EventStream과 LifecycleHub·EventHub, Bootstrapper에는 구현이 있다. EventStream.Main은 기본 틱 스트림을 제공하며 시간 계산과 실행 루프는 호스트가 담당한다. Bootstrapper는 초기화가 완료되기 전의 실행 순서와 실패 정리를 조율한다. 수학 계산과 Transform의 근사 비교·직렬화는 Infrastructure에서 제공한다.

### 1-4. 상세 문서

| 층       | Objects                                                                                                                 | Event System                                                                                                                                           | Bootstrapper                              |
| ------- | ----------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------- |
| Level 1 | [Utilities](01-O.1-Utilities/README.md), [Interfaces](01-O.2-Interfaces/README.md), [Objects](01-O.3-Objects/README.md) | —                                                                                                                                                      | —                                         |
| Level 2 | [Property](02-O.1-Property/README.md)                                                                                   | [Handle·Token](02-E.1-HandleToken/README.md), [AdaptiveCollections](02-E.2-AdaptiveCollections/README.md), [EventStream](02-E.3-EventStream/README.md) | —                                         |
| Level 3 | —                                                                                                                       | [LifecycleHub](03-E.1-LifecycleHub/README.md), [EventHub](03-E.2-EventHub/README.md)                                                                   | —                                         |
| Level 4 | —                                                                                                                       | —                                                                                                                                                      | [Bootstrapper](04-Bootstrapper/README.md) |

## 2. Event System

### 2-1. 구성과 책임

Event System은 객체 사이의 연결을 유지하고 변경·실패·폐기 상황에서도 이벤트 전달 상태를 정리하는 기능 묶음이다. 연결 해제, 순회 중 변경, 스트림 전파와 객체별 생애주기를 각각의 책임으로 나누어, 수신자 변경이나 실패가 다른 연결의 정합성을 훼손하지 않게 한다.

| 모듈 | 책임 |
| --- | --- |
| [Handle·Token](02-E.1-HandleToken/README.md) | 연결 해제나 정리 동작을 폐기에 묶는다. |
| [AdaptiveCollections](02-E.2-AdaptiveCollections/README.md) | 순회 중 변경과 반영 시점을 관리한다. |
| [EventStream](02-E.3-EventStream/README.md) | 구독과 조건·순서에 따른 이벤트 전파, 스트림 사이의 연결 수명을 관리하고 Main으로 기본 틱 스트림을 제공한다. |
| [LifecycleHub](03-E.1-LifecycleHub/README.md) | 객체별 연결과 폐기 완료 알림을 관리한다. |
| [EventHub](03-E.2-EventHub/README.md) | 생애주기 계약을 확장하여 객체별 연결, Sleep·Awake, 갱신과 폐기 전후 알림을 관리한다. |

Property는 외부 페이로드를 저장하는 별개 책임이다. 이벤트 객체에 종속되지 않으며 필요한 도메인 객체가 저장소를 제공한다.

### 2-2. 대표 협력 흐름

1. 제공자가 구독을 만들고 해제 동작을 Handle에 담아 반환한다.
2. EventStream에 값을 발행하면 등록된 그래프의 조건을 따라 수신자에게 전달한다. 기본 틱은 호스트가 EventStream.Main에 초 단위 경과 시간을 발행한다.
3. LifecycleHub는 객체별 연결과 폐기 완료 알림을 관리한다. EventHub는 생애주기 계약을 확장하고 내부 스트림을 사용하여 갱신·수면·폐기 전후 알림을 조율한다.
4. 수신자가 연결을 해제하거나 소유자가 폐기되면 구독과 보관 참조를 정리한다. 연산으로 생성한 스트림은 자신이 소유한 상위 연결도 해제한다.

EventHub는 객체별 구독 핸들과 생애주기를 소유하고, 내부 EventStream은 갱신 전파·변경 반영·실패한 구독 해제와 예외 보고를 담당한다. 순서·조건 공유·구독 변경의 세부 규칙은 [EventStream](02-E.3-EventStream/EventStream.md)과 [EventHub](03-E.2-EventHub/EventHub.md)를 따른다.

### 2-3. 실패와 폐기

실패한 구독·경로는 담당 모듈이 정리하고 ExceptionHandler에 보고한다. 예외가 처리되면 다른 전달을 계속하고, 재전파되면 남은 호출을 중단하되 이미 시작한 작업과 참조 정리를 마친다.

폐기 상태의 전환과 알림 시점, 재진입 정책은 객체마다 다르다. [Handle](02-E.1-HandleToken/Handle.md)은 보관한 동작의 실행을, [EventStream](02-E.3-EventStream/EventStream.md)은 전파 종료와 구독 정리를, [LifecycleHub](03-E.1-LifecycleHub/LifecycleHub.md)와 [EventHub](03-E.2-EventHub/EventHub.md)는 객체별 생애주기 알림을 정의한다.

연결과 전파의 설계 원칙은 [이벤트](../../01.%20Concepts/04.%20이벤트.md)와 [객체 활동과 연결 원칙](../../01.%20Concepts/03.%20객체%20활동과%20연결%20원칙.md)을 따른다.

[전체 계층](../README.md)
