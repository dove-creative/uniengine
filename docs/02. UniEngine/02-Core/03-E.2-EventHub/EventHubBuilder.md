# EventHubBuilder

EventHub의 연결 하나에 조건별 갱신 콜백과 생애주기 콜백을 등록하는 구성 객체이다.

## 목차

- 역할과 구성
- 동작 흐름
- 설계 기준과 고려사항

## 역할과 구성

- 책임: 갱신의 조건과 콜백을 함께 받아 연결에 등록하고, 소유자의 폐기 전·후 반응을 구성한다.
- 보유 정보: EventHubBuilder&lt;TEventArgs&gt;는 내부 연결 계약 IEventHubConnection만 보관한다. 경로 위치나 EventStream을 보관하지 않는다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| OnUpdated | 조건이 없는 루트, target 값 비교, 조건 대리자, 여러 단계 EventCondition과 콜백을 함께 등록한다. |
| OnFlags | ulong Builder에서 flags enum의 비트 조건과 콜백을 함께 등록한다. |
| OnDisposing·OnDisposed | 소유자의 폐기 단계에 맞춰 정리와 완료 반응을 나누어 등록한다. |

## 동작 흐름

Connect가 리스너의 Connection과 Builder를 만든다. 각 등록은 자기 조건을 완결하여 전달하고 모든 등록 메서드는 같은 Builder를 반환한다. 앞의 OnUpdated나 OnFlags에 넘긴 조건은 뒤의 등록에 영향을 주지 않는다. 조건을 여러 단계로 이어야 할 때는 [EventCondition](EventCondition.md)을 만들어 한 번의 OnUpdated에 전달한다.

Connection은 조건을 루트부터 내부 스트림 경로로 연결하고 전달한 대리자 전체를 구독 하나로 등록한다. 등록은 원본 대리자·조건·콜백 형식과 핸들을 보관한다. 모든 리스너가 하나의 스트림 그래프를 공유하므로 같은 부모 위치의 같은 Where 대리자, 동등한 When target, 같은 마스크 비트는 리스너가 달라도 공유한다. target 단축형은 EventCondition.When(target)을 사용한다. 값·비트 판정과 방문 순서는 [EventStream 연산자](../02-E.3-EventStream/EventStreamOperators.md)의 When·ForFlags 계약을 따른다.

OnDisposing·OnDisposed는 갱신 조건과 관계없이 Connection에 등록된다. Disconnect는 해당 연결의 모든 갱신 핸들을 해제하고 생애주기 콜백을 비운다.

## 설계 기준과 고려사항

Builder는 연결만 구성하고 조건은 등록마다 명시한다. 조건 평가와 전파 중 추가·해제 반영은 EventStream의 계약을 따른다. 허브와 스트림의 구체 형식은 연결 계약 뒤에 두어 Builder의 등록 책임과 분리한다.

- 등록 검사 순서는 연결 수명, 조건 인자, null 콜백이다. 해제된 연결은 InvalidOperationException이며 허브 폐기 이후에는 ObjectDisposedException이 우선한다. 재연결해도 이전 Builder는 되살아나지 않는다.
- null 조건 대리자와 null EventCondition은 ArgumentNullException이며 기존 연결과 등록을 유지한다. object 조건도 변환 람다를 만들기 전에 null을 거부한다. 유효한 조건의 null 콜백은 등록하지 않고 같은 Builder를 반환한다.
- target은 EqualityComparer&lt;TEventArgs&gt;.Default로 비교한다. 참조 형식과 Nullable&lt;T&gt;의 null target도 비교 대상이다. 등록 뒤 target의 Equals·GetHashCode 결과가 바뀌는 경우는 지원하지 않는다. null 리터럴은 오버로드가 모호하므로 `(string)null`, `(Func<string, bool>)null`, `(EventCondition<string>)null`처럼 의도한 형식을 지정한다.
- OnFlags는 값과 마스크의 공통 비트가 하나라도 있을 때 통과하며 원래 ulong 값을 전달한다. 값이 0이면 통과하지 않는다. 마스크는 `[Flags]` enum이고 비트가 0이 아니어야 하며, 잘못된 마스크는 콜백이 null이어도 ArgumentException이다. enum 기반 형식의 선언 비트 폭을 보존한다.
- 같은 연결에서 콜백 형식·원본 콜백 대리자·조건이 모두 같으면 InvalidOperationException이다. 조건은 단계 수와 각 단계의 종류·값으로 비교하며, 단축형은 동등한 한 단계 EventCondition과 중복이다. 생애주기 중복 판정에는 조건을 포함하지 않는다.
- typed/object 값 사용 콜백은 별도 형식이다. object 콜백과 object 조건의 중복은 변환 람다가 아닌 원본 대리자로 검사한다. 인자 없는 콜백은 인터페이스와 관계없이 같은 형식이다.
- 키 행 공유는 EventStream 규칙을 따른다. target 단축형과 따로 만든 같은 값의 When 조건도 부모가 같으면 WhenNode의 행을 공유한다. 부모나 순서 그룹이 다르면 공유하지 않으며 Debug는 공유하지 않는다. object 조건은 형식 변환 람다를 만들므로 같은 원본 조건을 다시 넘겨도 행 공유를 보장하지 않는다.
- When의 발행 중 비교 예외는 같은 위치의 모든 target 경로를 해제한다. 등록 중 비교 예외는 호출자에게 전파하며 실패한 등록으로 새로 만든 빈 행과 노드는 남기지 않는다. 성능은 측정하지 않았고 Unity Mono·IL2CPP 할당도 검증하지 않았다.
- 멀티캐스트 묶음 하나가 등록 하나이다. 항목 하나가 실패하면 묶음 전체가 해제되고 뒤 항목은 생략된다. Disconnect·Dispose도 뒤 항목을 즉시 중단한다. 다른 등록은 유지하되 마지막 등록이 사라지면 연결도 해제한다.
- 폐기 단계마다 Action과 Action&lt;object&gt;를 등록한 순서대로 호출한다.
- object 계약으로 값 형식 페이로드를 전달하면 박싱될 수 있다.

[사용 예제](Usage.md)의 객체 연결과 폐기 알림 절은 루트·조건 갱신과 폐기 완료 콜백을 한 연결에 등록한다. 비트 조건으로 연결하기 절은 OnFlags와 생애주기 알림을 함께 구성한다. 공개 메서드 구성은 [Builder 계약](IEventHubBuilder.md)을 따른다.

[모듈 구성](README.md)
