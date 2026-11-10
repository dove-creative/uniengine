# EventHubBuilder

EventHub에 등록한 리스너 하나에 묶여 갱신 경로를 감싸는 구성 객체이다.

## 목차

- 역할과 구성
- 동작 흐름
- 설계 기준과 고려사항

## 역할과 구성

- 책임: 한 리스너의 갱신 구독을 EventStream·EventPath에 연결하고 생애주기 콜백은 리스너 단위로 구성한다.
- 보유 정보: EventHub&lt;TEventArgs&gt;.Builder는 특정 연결의 Order와 경로 위치를 보관한다. 루트 위치는 허브의 EventStream이고, 그 아래 위치는 EventPath이다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| Where·Debug | EventPath의 조건·로그 연산을 적용한 새 Builder를 반환한다. 같은 리스너 연결을 유지한다. |
| OnUpdated | 현재 경로에 값을 받는 콜백 또는 인자 없는 콜백을 구독하고 같은 Builder를 반환한다. |
| OnDisposing·OnDisposed | 소유자의 폐기 단계에 맞춰 정리와 완료 반응을 나누어 등록한다. |

## 동작 흐름

Connect가 리스너의 Order와 루트 Builder를 만든다. Where·Debug는 기존 Builder를 바꾸지 않고 새 경로를 감싼 Builder를 만든다. OnUpdated는 전달한 대리자 전체를 현재 위치의 구독 하나로 등록하고, 원본 대리자·경로·콜백 형식과 핸들을 Order에 보관한다. 모든 리스너가 하나의 스트림 그래프를 공유하므로 같은 부모 위치의 같은 Where 대리자는 한 번만 평가된다.

OnDisposing·OnDisposed는 경로의 조건과 관계없이 Order에 등록된다. 어느 경로의 Builder에서 호출해도 같은 리스너의 생애주기 콜백이다. Disconnect는 모든 갱신 핸들을 해제하고 생애주기 콜백을 비운다.

## 설계 기준과 고려사항

Order 하나를 구성 대상으로 고정하여 경로가 갈라져도 구독이 어느 연결에 속하는지 유지한다. 조건 평가와 전파 중 추가·해제 반영은 EventStream의 계약을 따른다.

- 다른 수신자의 설정에 재사용하는 Builder가 아니다.
- Where(null)은 경로를 구성할 때 ArgumentNullException이며 기존 연결과 구독은 유지된다.
- 멀티캐스트는 묶음 하나가 등록 하나이다. 항목 하나가 실패하면 묶음 전체가 해제되고 뒤 항목은 생략된다. Disconnect·Dispose도 뒤 항목을 즉시 중단한다. 다른 등록은 유지하되 마지막 등록이 사라지면 연결도 해제한다.
- 같은 연결·경로·콜백 형식의 동일한 원본 대리자 등록은 InvalidOperationException이다. 경로는 Builder가 보관한 경로 인스턴스로 구분하며, 별도로 구성한 조건 경로의 동등성은 추론하지 않는다. 생애주기 등록의 중복 판정에는 경로를 포함하지 않는다.
- typed/object 값 사용 콜백은 별도 형식이다. object 어댑터도 원본 대리자로 중복을 검사하며, 인자 없는 콜백은 인터페이스와 관계없이 같은 형식이다.
- 폐기 단계마다 Action과 Action<object>를 등록한 순서대로 호출한다.
- 명시적 해제·마지막 등록 제거로 해제되거나 재연결 전의 Builder의 모든 메서드는 InvalidOperationException이다. 허브 폐기 이후에는 ObjectDisposedException이 우선한다.
- object 인자 계약에서 형식 있는 계약으로 연결할 때 값 형식 페이로드는 박싱될 수 있다.
- object Where는 형식 변환 람다를 만들므로 같은 object 조건 대리자를 다시 넘겨도 조건 공유를 보장하지 않는다. null은 람다로 감싸기 전에 거부한다.

[사용 예제](Usage.md)의 객체 연결과 폐기 알림 절은 루트·조건 경로의 갱신 콜백과 폐기 완료 콜백을 한 연결에 구성한다. 공개 메서드 구성은 [Builder 계약](IEventHubBuilder.md)을 따른다.

[모듈 구성](README.md)
