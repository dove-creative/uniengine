# Event System

## 목차

1. 소개
2. 구성과 의존 관계
3. 연결과 전파 흐름
4. 폐기와 예외 정책
5. 이관 범위와 후속 검증

## 1. 소개

Event System은 객체 간 이벤트 전달과 연결의 생애주기를 관리하는 UniEngine Core 기능이다.

| 항목 | 설명 |
| --- | --- |
| 개요 | 연결 해제 수단, 순회 중 변경 가능한 컬렉션, 이벤트 스트림과 생애주기 허브를 제공한다. |
| 목적 | 수신자의 등록·삭제와 객체의 폐기가 이벤트 전달 도중 발생해도 연결과 전파 상태를 일관되게 관리한다. |
| 구현 | Handle·Token과 AdaptiveCollections를 기반으로 EventStream과 EventHub 계열을 구성한다. |

공통 동작 기준은 [이벤트 원칙](../01.%20Concepts/04.%20이벤트.md)을 따른다. 각 기능의 계층은 [Hierarchy](../01.%20Concepts/02.%20Hierarchy.md)에 정의한다.

## 2. 구성과 의존 관계

| 구성 | 역할 | 주요 의존 |
| --- | --- | --- |
| Handle·Token | Dispose에 연결된 동작을 보관하고, 연결 해제나 일시 정지 해제 시점을 표현한다. | ExceptionHandler |
| AdaptiveCollections | 순회 중 추가·삭제를 관리한다. AdaptiveAction은 콜백 호출과 수신자 예외 처리를 담당한다. | ExceptionHandler |
| EventStream | 구독과 스트림 연결을 관리하고, 필터·횟수 제한·병합·정렬·라우팅으로 전파를 구성한다. | Handle, AdaptiveCollections, 플래그 연산, Log |
| EventHub·LifecycleHub | 객체의 연결과 폐기 알림을 관리한다. EventHub는 데이터 전파와 Sleep·Awake를 함께 제공한다. | Handle·Token, AdaptiveCollections, ExceptionHandler, Log |

EventStream과 EventHub는 각각 기반 기능을 조합한다. EventHub가 EventStream을 상속하거나 내부 전달 수단으로 사용하는 구조는 아니다.

공개 형식의 기본 네임스페이스는 `Blackthunder.UniEngine.Core`이며, 스트림 연산자와 TerminationMode는 `Blackthunder.UniEngine.Core.EventStreamCore`에 둔다. 로그와 예외 처리는 기존 `BlackThunder.UniEngine.Infrastructure`를 사용한다.

## 3. 연결과 전파 흐름

### 3-1. 스트림 연결

EventStream은 구독자가 데이터를 받는 연결을 만들고 해제용 Handle을 반환한다. Chain·Connect를 통해 연결된 수신자가 IPassiveObserver 계약을 제공하면, 수신자도 상위 연결을 보관하여 자신의 폐기 시 해제한다.

EventRouter는 이벤트 마스크로 수신자를 선택하고, OrderedEventStream은 지정한 순서로 수신자를 호출한다. 스트림 연산자는 같은 연결 흐름 위에서 이벤트를 거르거나 제한하거나 합친다.

Enabled는 활성 객체의 데이터 전파를 제어한다. TerminationMode는 명시적인 종료 또는 마지막 구독 해제에 따른 스트림 폐기 시점을 결정한다.

### 3-2. 허브 연결

EventHub는 수신자별 연결 정보를 보관하고, Builder로 갱신·폐기 알림을 구성한다. 직접 이벤트 구독에도 동일한 폐기·예외 처리 원칙을 적용한다.

Sleep은 데이터 전파를 일시 정지하고 반환된 토큰의 해제로 Awake를 수행한다. LifecycleHub는 데이터 전파 없이 연결된 객체에 폐기 알림을 제공한다.

### 3-3. 순회 중 변경

이벤트 전달에 사용하는 지연 반영 컬렉션에서는 새 수신자를 다음 전파부터 호출하고, 제거한 수신자는 현재 전파의 이후 호출에서 제외한다. Clear나 발신 객체의 폐기 요청으로 현재 전파가 중단된 경우에도 순회 상태와 보류된 변경을 정리한다.

AdaptiveCollections의 즉시 반영 옵션은 기존대로 유지한다. 이 컬렉션은 순회 중 변경을 위한 기능이며, 여러 스레드의 동시 접근을 동기화하는 기능은 제공하지 않는다.

## 4. 폐기와 예외 정책

| 상황 | 처리 |
| --- | --- |
| 폐기 요청 이후 Update·OnNext | ObjectDisposedException을 발생시킨다. |
| 폐기 요청 이후 새 연결·구독 | ObjectDisposedException을 발생시킨다. |
| 폐기 완료 이후 연결 해제·구독 해제 | 무시한다. |
| 반복 Dispose | 무시한다. |
| 종료 후 반복 OnCompleted·OnError | 무시한다. |
| 전파 도중 발신 객체의 폐기 요청 | 현재 콜백 반환 후 다음 수신자부터 호출을 중단하고 정리한다. |
| 수신자 예외 | 실패한 수신자를 제거한 뒤 ExceptionHandler에 보고한다. |
| 예외 보고의 정상 반환 | 다음 수신자에 대한 전파를 계속한다. |
| 예외 보고 중 예외 발생 | 현재 전파를 중단하고 호출자에게 전달하며, 전파 상태와 필요한 자원을 정리한다. |

폐기 검사는 Enabled, 필터, 연산자 처리보다 우선한다. 정상적으로 시작한 전파가 폐기 요청으로 중단되는 것 자체는 예외가 아니다. IsDisposed가 true로 전환되기 전까지 필요한 연결을 해제하며, 전환 이후에는 완료 알림을 전달하는 중이더라도 해제 요청을 무시한다. 폐기 후 요청에 대한 별도의 무시·예외 선택 옵션은 제공하지 않는다.

Debug 연산자는 Infrastructure의 LogLevel을 받는다. 명시적으로 폐기하지 않은 허브에 대한 종료자 경고도 Log를 사용하되, 종료자 밖으로 로그 예외를 전파하지 않는다.

## 5. 이관 범위와 후속 검증

Project-Index의 Event System 구현 20개 파일을 이관하며, 원본 프로젝트와 Unity 테스트·메타데이터는 유지한다. Golden Hind에 Unity 의존성이나 테스트 프로젝트를 추가하지 않는다.

이번 이관에서는 빌드와 테스트를 실행하지 않는다. 후속 검증 대상은 다음과 같다.

- 폐기 후 전파·연결 예외와 반복 정리의 무해성
- 전파 중 폐기 요청 이후 추가 수신자 호출 중단
- 수신자 오류, 예외 핸들러 미등록·실패, 전파 상태 복구
- 순회 중 등록·삭제, Router·OrderedStream·연산자 동작
- Sleep·Awake와 연결 해제·폐기의 생애주기 정리
