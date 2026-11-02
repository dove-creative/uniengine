# UniEngine Core

객체 제어, 공통 계약, 데이터 표현과 이벤트 전파를 제공하는 기반 기능 계층이다.

## 목차

1. 소개
2. 계층별 책임
3. 대표 협력 흐름
4. 구현 범위
5. 상세 문서

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 객체의 준비·제어·데이터 보관과 연결·전파 수명을 다룬다. |
| 목적 | 도메인 객체들이 독립적으로 활동하면서 필요한 관계를 맺고 정리할 수 있게 한다. |
| 구현 | 공통 계약과 재사용 객체 위에 컬렉션·스트림·허브의 책임을 차례로 구성한다. |

## 계층별 책임

Level 1은 공통 도구와 계약, 그 계약으로 동작하는 재사용 객체를 제공한다. Level 2는 값 저장·페이로드·연결 해제·이벤트 전달의 개별 책임을 구성한다. Level 3은 이를 객체별 연결과 생애주기 관리로 결합한다.

예를 들어 IEnumerationSource는 순회 수명을 정의하고 ValueEnumerator는 그 계약을 호출한다. AdaptiveCollection은 저장 목록과 변경 정책을 소유한다. 각 층이 자신의 정보와 기능을 소유하므로 공통 열거자는 구체 컬렉션에 의존하지 않는다.

## 대표 협력 흐름

1. 제공자가 구독을 만들고 해제 동작을 Handle에 담아 반환한다.
2. EventStream은 변경 가능한 컬렉션을 통해 수신자에게 이벤트를 전달한다.
3. EventHub는 내부 스트림을 사용하며 객체별 연결, 수면과 폐기 알림을 조율한다.

Property는 외부 페이로드를 저장하는 별개 책임이다. 이벤트 객체에 종속되지 않으며 필요한 도메인 객체가 저장소를 제공한다. 연결·전파·폐기의 공통 흐름은 [Event System](EventSystem.md)에 정리한다.

## 구현 범위

공통 도구·계약·ValueEnumerator, Property, Handle·Token, AdaptiveCollections, EventStream과 EventHub에는 구현이 있다. Math·데이터 도구·Loco·Bootstrapper는 현재 계층표에 배치된 설계 항목이다. 이들의 구체 API와 상태를 구현 사실로 설명하지 않는다.

## 상세 문서

| 층 | Objects | Event System | Utilities·기타 |
| --- | --- | --- | --- |
| Level 1 | [Utilities](01-O.1-Utilities/README.md), [Interfaces](01-O.2-Interfaces/README.md), [Objects](01-O.3-Objects/README.md) | — | — |
| Level 2 | [Property](02-O.1-Property/README.md) | [Handle·Token](02-E.1-HandleToken/README.md), [AdaptiveCollections](02-E.2-AdaptiveCollections/README.md), [EventStream](02-E.3-EventStream/README.md) | [Math·데이터 도구](02-U-Utilities/README.md), 설계 단계 |
| Level 3 | — | [EventHub](03-E-EventHub/README.md) | [TransformDataExtensions·Loco](03-U-Utilities/README.md), 설계 단계 |
| Top | — | — | [Kernel.Bootstrapper](Top-Kernel/Bootstrapper.md), 설계 단계 |

[전체 계층](../README.md)
