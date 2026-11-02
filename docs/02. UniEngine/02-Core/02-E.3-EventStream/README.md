# Event Stream

구독으로 이벤트 전달 경로를 만들고, 그 경로의 전달·해제·종료를 함께 관리하는 모듈이다.

## 목차

1. 소개
2. 핵심 용어
3. 스트림의 역할
4. 경로 구성
5. 연산자 조합
6. 연결 수명
7. 전파 규칙
8. 순서와 라우팅
9. 상세 문서

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 구독과 연결로 구성하는 이벤트 전달 경로이다. |
| 목적 | 발신자가 수신자 목록과 연결 정리를 직접 구현하지 않고도 이벤트 경로를 조합하게 한다. |
| 구현 | 수신자 목록을 [AdaptiveAction](../02-E.2-AdaptiveCollections/AdaptiveAction.md)의 지연 추가 모드로 관리하고 구독마다 해제 핸들을 발급한다. 필터·병합·순서·라우팅은 각각 별도 스트림으로 구성한다. |

- 스트림은 이벤트를 받는 쪽이면서 보내는 쪽이다. 따라서 여러 스트림을 이어 하나의 경로를 만들 수 있다.
- 모든 전달은 입력을 호출한 흐름 안에서 동기적으로 일어난다. 스케줄러나 스레드 간 동시 접근은 보장하지 않는다.
- 연결은 핸들로 해제한다. 참조를 버리는 것만으로는 연결이 끊기지 않는다.
- 연산자 경로는 받을 쪽이 사라지면 소스 방향으로 스스로 정리된다.

## 핵심 용어

| 용어 | 뜻 |
| --- | --- |
| 스트림 | 이벤트를 받아 등록된 수신자에게 다시 전달하는 객체이다. EventStream과 그 파생 형식을 가리킨다. |
| 구독 | 스트림에 콜백이나 Observer를 등록하는 일이다. Subscribe가 수행하며 해제 핸들을 반환한다. |
| 연결 | 한 스트림의 출력을 다른 Observer의 입력으로 잇는 구독이다. Chain·Connect가 만든다. |
| 상위·하위 | 연결에서 이벤트를 보내는 쪽이 상위, 받는 쪽이 하위이다. |
| 전파 | OnNext 한 번이 등록된 수신자를 차례로 호출하는 과정이다. |
| 종료 | OnCompleted·OnError로 완료나 오류를 알린 뒤 스트림을 폐기하는 일이다. |

## 스트림의 역할

[EventStream](EventStream.md)은 이벤트를 받는 Observer이면서 수신자를 등록받는 Observable이다. [IEventStream](EventStreamInterfaces.md)은 `IObserver<T>`·`IObservable<T>`·IDisposable을 하나의 계약으로 묶는다. 그래서 같은 스트림이 경로 끝에서 콜백을 실행할 수도 있고, 다른 스트림의 입력이 될 수도 있다.

| 입력 | 의미 | 입력 후 스트림 |
| --- | --- | --- |
| OnNext | 값을 전달한다. | 계속 사용할 수 있다. |
| OnCompleted | 완료를 알린다. | 알린 뒤 폐기된다. |
| OnError | 오류를 알린다. | 알린 뒤 폐기된다. |

Subscribe는 수신자를 등록하고, 그 구독만 해제하는 IDisposable 핸들을 반환한다. 같은 콜백을 여러 번 구독해도 핸들은 등록마다 따로 생긴다.

기본 EventStream은 수신자를 등록한 순서대로 호출한다. 실행 단계를 등록 순서와 따로 정해야 하면 [OrderedEventStream](OrderedEventStream.md)을 사용한다. 이벤트 전달과 수신자 변경의 기본 원칙은 [이벤트](../../../01.%20Concepts/04.%20이벤트.md)를 따른다.

## 경로 구성

스트림은 직렬로 잇거나 여러 갈래로 나눌 수 있다. 앞 스트림의 출력은 다음 스트림의 입력이 된다. 한 발신자에 여러 수신자를 이으면 경로가 나뉘고, 여러 발신자를 한 수신자에 이으면 입력이 한곳으로 모인다.

```text
source ──Chain──▶ Where ──Chain──▶ Take ──Subscribe──▶ callback
  │
  └──Subscribe──▶ callback          (receives every value)

left  ──Connect──┐
                 ├──▶ Merge ──Subscribe──▶ callback
right ──Connect──┘
```

| 메서드 | 하는 일 | 반환값 |
| --- | --- | --- |
| Subscribe | 콜백이나 Observer를 등록한다. | 구독 해제 핸들 |
| Chain | 하위 Observer를 연결한다. | 연결한 하위 Observer. 다음 단계를 이어 붙일 때 쓴다. |
| Connect | Chain과 같은 연결을 만든다. | 현재 상위 스트림. 같은 발신자에서 갈래를 더 낼 때 쓴다. |
| Append | 새 기본 EventStream을 만들어 연결한다. | 새 하위 스트림 |

Chain과 Connect는 반환값만 다르다. 둘 다 out 매개변수로 그 연결의 해제 핸들을 돌려줄 수 있다. 하위 Observer가 [IPassiveObserver](EventStreamInterfaces.md)이면 이 핸들을 하위에도 넘긴다. 하위는 자신이 폐기될 때 그 핸들로 상위와의 연결을 끊는다. Subscribe만 호출하면 이 인계는 일어나지 않는다.

여러 입력의 완료까지 함께 다루려면 Merge를 사용한다. 연결 메서드의 오버로드는 [EventStream 인터페이스](EventStreamInterfaces.md), 생성과 연결을 묶은 편의 기능은 [EventStreamExtensions](EventStreamExtensions.md)에서 설명한다.

## 연산자 조합

연산자는 원본을 바꾸지 않는다. 확장 메서드가 새 하위 스트림을 만들어 원본에 연결할 뿐이므로, 원본의 다른 구독은 계속 모든 값을 받는다. 연산자를 이어 붙이면 붙인 순서대로 각 단계가 자신의 입력을 처리한다.

| 연산자 | 값 처리 | 스스로 완료하는 시점 |
| --- | --- | --- |
| Skip | 처음 지정한 개수를 버리고 이후 값을 전달한다. | 없음 |
| SkipWhile | 조건이 처음 false가 될 때까지 버린다. 그 뒤로는 조건을 보지 않는다. | 없음 |
| Where | 값마다 조건을 검사하여 true인 값만 전달한다. | 없음 |
| Take | 지정한 개수만큼 전달한다. | 개수를 채웠을 때 |
| TakeWhile | 조건이 true인 동안 전달한다. | 조건이 처음 false일 때. 그 값은 전달하지 않는다. |
| Debug | 값과 메시지를 로그에 남기고 그대로 전달한다. | 없음 |
| Merge | 여러 상위의 값을 하나로 모은다. | All은 모든 상위, Any는 첫 상위가 완료했을 때 |

카운터와 조건 상태는 연산자 인스턴스에 속한다. 같은 연산자를 여러 수신자가 구독하면 그 상태도 함께 쓴다. 조건을 받는 SkipWhile·Where·TakeWhile은 값을 받는 조건과, 값 없이 외부 상태만 보는 조건을 모두 지원한다.

직접 만든 연산자도 같은 방식으로 쓸 수 있다. EventStream을 상속하여 OnNext에서 입력을 처리하고, 전달할 값만 기본 OnNext로 넘긴다. 이때 폐기 검사와 Enabled 처리는 기본 연산자와 같게 유지한다. 완성한 연산자는 Chain으로 경로에 붙인다. 각 연산자의 상세 동작은 [EventStream 연산자](EventStreamOperators.md)에서 설명한다.

## 연결 수명

구독은 콜백을 강하게 참조한다. 핸들을 버리거나 수신자의 다른 참조를 없애도 구독은 해제되지 않는다. 수신이 끝나면 구독 핸들이나 소유한 스트림을 Dispose해야 한다.

하위 스트림은 Chain·Connect로 받은 상위 연결 핸들을 보관했다가 자신이 폐기될 때 해제한다. 그 뒤 상위가 어떻게 되는지는 상위의 [TerminationMode](TerminationMode.md)가 정한다.

| 상위의 TerminationMode | 마지막 구독이 해제되면 | 기본값으로 쓰는 곳 |
| --- | --- | --- |
| Complete | 상위는 남는다. | 기본 EventStream, Append가 만든 스트림 |
| NoSubscribers | 상위도 폐기되고, 같은 판단이 그 위로 이어진다. | 모든 연산자 |

예를 들어 source → Where → Take 경로에서 Take가 개수를 채우면 다음 순서로 정리된다.

1. Take가 완료를 알리고 폐기된다.
2. Take가 보관한 Where와의 연결을 해제한다.
3. Where는 NoSubscribers이고 남은 구독이 없으므로 폐기된다.
4. Where가 보관한 source와의 연결을 해제한다.
5. source는 Complete이므로 연결만 끊기고 남는다. source의 다른 구독도 그대로 유지된다.

| 요청 | 결과 |
| --- | --- |
| Enabled = false | 값 전달을 잠시 멈춘다. 연산자의 카운터와 조건 검사도 진행하지 않는다. 완료·오류 통지는 막지 않는다. |
| 구독 핸들 Dispose | 그 구독만 해제한다. NoSubscribers 스트림에서 마지막 구독이었다면 스트림도 폐기한다. |
| OnCompleted·OnError | 통지를 시도하고, 통지 중 예외가 나더라도 스트림을 폐기한다. |
| 스트림 Dispose | 상위 연결과 자신의 구독을 모두 정리한다. 완료 통지는 보내지 않는다. |

## 전파 규칙

전파 도중의 구독 변경은 AdaptiveAction의 지연 추가 모드를 따른다.

- 전파 중 추가한 구독은 다음 전파부터 참여한다.
- 전파 중 해제한 구독은 남은 호출에서 바로 빠진다. 이미 실행 중인 콜백은 끝까지 실행된다.
- 전파 중 스트림을 폐기하면 남은 수신자는 호출하지 않는다.
- 콜백 안에서 같은 스트림의 OnNext를 다시 호출할 수 없다. 이 호출은 그 콜백의 실패로 처리된다.

콜백이 예외를 던지면 그 구독을 먼저 해제하고 [ExceptionHandler](../../01-Infrastructure/03-M.2-ExceptionHandler/ExceptionHandler.md)에 보고한다. 보고가 정상 반환하면 다음 수신자로 진행한다. 처리기가 없거나 처리기가 예외를 던지면 예외가 호출자에게 전달된다. 자세한 실패 처리 순서는 [EventStream](EventStream.md)을 따른다.

## 순서와 라우팅

[OrderedEventStream](OrderedEventStream.md)은 수신자를 PreUpdate·Update·PostUpdate 같은 실행 단계, 즉 순서 그룹으로 나누어 호출한다. 구독한 시점과 관계없이 그룹 순서대로 실행하며, 같은 그룹 안에서는 등록 순서를 따른다. 순서를 지정하지 않은 구독은 생성할 때 정한 기본 그룹에 들어간다.

[EventRouter](EventRouter.md)는 실행 순서 대신 값을 받을 수신자를 고른다. 이벤트를 비트 마스크로 바꾸고, 구독 마스크와 겹치는 비트가 하나라도 있는 수신자에게만 값을 전달한다. 대상을 지정하지 않은 일반 구독은 모든 값을 받는다.

## 상세 문서

| 문서 | 내용 |
| --- | --- |
| [EventStream 인터페이스](EventStreamInterfaces.md) | IEventStream의 입력·구독·연결 계약과 IPassiveObserver의 연결 핸들 인계 |
| [EventStream](EventStream.md) | 값·완료·오류를 전달하고 구독과 연결의 수명을 관리하는 기본 스트림 |
| [TerminationMode](TerminationMode.md) | 마지막 구독 해제를 폐기 조건으로 삼을지 정하는 종료 정책 |
| [EventStream 연산자](EventStreamOperators.md) | Debug·Skip·SkipWhile·Where·Take·TakeWhile·Merge의 상태와 동작 |
| [EventStreamExtensions](EventStreamExtensions.md) | 연산자 생성과 연결을 한 번에 하는 확장 메서드 |
| [OrderedEventStream](OrderedEventStream.md) | 순서 그룹별로 수신자를 호출하는 스트림 |
| [EventRouter](EventRouter.md) | 비트 마스크가 겹치는 수신자에게만 값을 전달하는 스트림 |
| [사용 예제](Usage.md) | 구독과 해제, 필터 조합, 병합, 순서 그룹, 대상 라우팅 예제 |

[Core 구성](../README.md)
