# IEventHubBuilder

연결된 수신자의 알림을 단계적으로 설정하는 구성 계약 묶음이다.

## 목차

- 역할과 구성
- 동작 흐름
- 설계 기준과 고려사항

## 역할과 구성

- 책임: 허브의 내부 저장 구조를 드러내지 않고 연결 하나의 반응을 구성하게 한다.
- 상태 계약: 구성 대상의 저장 방식은 구현체가 소유한다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| IEventHubBuilder | object 경로의 조건·로그·갱신·폐기 알림을 구성한다. |
| IEventHubBuilder&lt;TEventArgs&gt; | 형식 있는 갱신 구성과 상위 계약의 변환 경로를 제공한다. |

## 동작 흐름

IEventHubBuilder는 [ILifecycleHubBuilder](../03-E.1-LifecycleHub/ILifecycleHubBuilder.md)의 완료 알림 계약을 확장한다. Connect의 반환 계약에서 Where·Debug로 경로를 구성하고 OnUpdated로 구독한다. Where·Debug는 같은 리스너의 새 구성기를 반환하고, 콜백 등록은 현재 구성기를 반환한다. 제네릭 기본 구현은 object 대리자를 형식 있는 경로로 변환하여 전달한다. EventHub의 Builder는 원본 대리자와 object 콜백 형식을 보존하는 등록 경로를 제공하여 변환 이후에도 중복을 판정한다.

| 기능 | 형식 있는 Builder | object Builder |
| --- | --- | --- |
| 조건 경로 | `Where(Func<TEventArgs, bool> predicate)` | `Where(Func<object, bool> predicate)` |
| 로그 경로 | `Debug(string message = "Debug", LogLevel logType = LogLevel.Log)` | 같은 인자와 기본값이다. |
| 값 사용 구독 | `OnUpdated(Action<TEventArgs> onUpdated)` | `OnUpdated(Action<object> onUpdated)` |
| 값 미사용 구독 | `OnUpdated(Action onUpdated)` | `OnUpdated(Action onUpdated)` |
| 폐기 전 알림 | `OnDisposing(Action)`·`OnDisposing(Action<object>)` | 같은 인자 계약이다. |
| 폐기 후 알림 | `OnDisposed(Action)`·`OnDisposed(Action<object>)` | 같은 인자 계약이다. |

target·filter를 함께 받는 OnUpdated 오버로드는 없다. 대상 비교는 `Where(value => value == target).OnUpdated(callback)`처럼 조건으로 표현하고, 기존 필터 등록은 `Where(filter).OnUpdated(callback)`으로 옮긴다. 인자 없는 콜백도 같은 경로 규칙을 따른다.

## 설계 기준과 고려사항

구성 계약은 설정 대상의 저장 방식과 분리하며, 경로가 갈라져도 생애주기와 구독 핸들의 소유권은 같은 리스너에 둔다.

- 구성 계약은 연결 해제 핸들이 아니다.
- 연결 제거는 허브의 Disconnect로 요청한다.
- Where의 null 조건은 등록 시점에 ArgumentNullException으로 거부한다. object 조건도 null을 변환 람다로 감싸지 않는다.
- 해제되거나 재연결 전의 Builder는 InvalidOperationException이며, 허브가 폐기되었으면 ObjectDisposedException이 우선한다.
- 같은 연결·경로·콜백 형식에 같은 원본 대리자를 다시 등록하면 InvalidOperationException이다. 생애주기 등록은 경로와 무관하다. 구체적인 등록 단위는 [EventHubBuilder](EventHubBuilder.md)를 따른다.

[모듈 구성](README.md)
