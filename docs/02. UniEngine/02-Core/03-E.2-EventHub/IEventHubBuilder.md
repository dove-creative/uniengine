# IEventHubBuilder

연결된 수신자의 조건별 갱신과 생애주기 알림을 등록하는 계약 묶음이다.

## 목차

- 역할과 구성
- 동작 흐름
- 설계 기준과 고려사항

## 역할과 구성

- 책임: 허브의 내부 저장 구조를 드러내지 않고 연결 하나의 반응을 등록하게 한다.
- 상태 계약: Builder는 경로 위치를 갖지 않으며 모든 등록이 같은 Builder 인스턴스를 반환한다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| IEventHubBuilder | object 조건 대리자와 콜백, 조건 없는 갱신, 폐기 알림을 등록한다. |
| IEventHubBuilder&lt;TEventArgs&gt; | 형식 있는 갱신과 조건 함수·값 비교를 등록한다. 상위 계약의 인자 없는 갱신과 폐기 알림은 형식 있는 메서드로 전달한다. |

## 동작 흐름

IEventHubBuilder는 [ILifecycleHubBuilder](../03-E.1-LifecycleHub/ILifecycleHubBuilder.md)의 완료 알림 계약을 확장한다. Connect의 반환값에 등록 종류를 구분한 OnUpdated·OnUpdatedWhere·OnUpdatedWhen·OnUpdatedForFlags를 사용한다. 각 호출은 독립된 등록이며, 앞의 등록 조건은 다음 등록에 전달되지 않는다. 제네릭 계약은 필요한 반환 형식을 new로 재선언하고, 인자 없는 갱신과 폐기 알림의 형식 없는 호출을 형식 있는 메서드로 전달한다. object 콜백과 object 조건의 등록은 전달하는 기본 구현이 없으며 구현체가 직접 구현한다. 형식 있는 변환 람다로 전달하면 원본 대리자와 object 콜백 형식을 잃어 중복 판정 계약을 지킬 수 없기 때문이다. EventHub의 Builder는 원본 대리자를 보존하여 중복을 판정한다.

| 등록 조건 | 형식 있는 Builder | object Builder |
| --- | --- | --- |
| 루트 | `OnUpdated(Action<TEventArgs>)`, `OnUpdated(Action)` | `OnUpdated(Action<object>)`, `OnUpdated(Action)` |
| 값 비교 | `OnUpdatedWhen(TEventArgs target, Action<TEventArgs>)`, `OnUpdatedWhen(TEventArgs target, Action)` | 제공하지 않는다. |
| 조건 대리자 | `OnUpdatedWhere(Func<TEventArgs, bool> condition, Action<TEventArgs>)`, `OnUpdatedWhere(Func<TEventArgs, bool> condition, Action)` | `OnUpdatedWhere(Func<object, bool> condition, Action<object>)`, `OnUpdatedWhere(Func<object, bool> condition, Action)` |
| 비트 조건 | `IEventHubBuilder<ulong>`에 대한 `OnUpdatedForFlags<TFlags>(TFlags mask, Action<ulong>)`, `OnUpdatedForFlags<TFlags>(TFlags mask, Action)` 확장 메서드이다. | 제공하지 않는다. |
| 폐기 전 알림 | `OnDisposing(Action)`, `OnDisposing(Action<object>)` | 같은 인자 계약이다. |
| 폐기 후 알림 | `OnDisposed(Action)`, `OnDisposed(Action<object>)` | 같은 인자 계약이다. |

OnUpdatedWhere·OnUpdatedWhen·OnUpdatedForFlags는 각각 내부 스트림의 Where·When·ForFlags 한 단계에 대응한다. 형식 없는 계약은 OnUpdatedWhen을 제공하지 않는다. 값 비교에는 페이로드 형식을 유지한 제네릭 계약을 사용한다. 여러 판정을 결합하려면 하나의 조건 함수 안에서 조합하고, 단계별 경로 구성·Debug·ForOrder가 필요하면 EventStream을 사용한다.

EventHubBuilderExtensions의 OnUpdatedForFlags는 `where TFlags : struct, Enum` 제약을 가진다. EventHub&lt;ulong&gt;에서 반환한 Builder에 등록하고 같은 인스턴스를 반환한다. 마스크는 `[Flags]` enum이며 ulong 마스크를 직접 받거나 enum 페이로드의 허브에서 호출하는 오버로드는 없다. null Builder는 ArgumentNullException이고, EventHubBuilder&lt;ulong&gt;이 아닌 별도 인터페이스 구현은 NotSupportedException이다.

## 설계 기준과 고려사항

구성 계약은 연결 하나의 구독 소유권과 수명을 유지한다. 조건의 종류는 메서드 이름으로 구분하며 각 등록은 루트 또는 단일 조건에 완결된다.

- 구성 계약은 연결 해제 핸들이 아니다. 연결 제거는 허브의 Disconnect로 요청한다.
- 검사 순서는 연결 수명 → 조건 인자 → null 콜백이다. 해제된 Builder는 InvalidOperationException이며, 허브가 폐기되었으면 ObjectDisposedException이 우선한다.
- OnUpdatedWhere의 null 조건 대리자는 ArgumentNullException으로 거부한다. object 조건도 변환 람다로 감싸기 전에 거부한다. 잘못된 등록은 기존 연결과 콜백을 바꾸지 않는다.
- OnUpdatedForFlags의 비트가 0인 마스크와 `[Flags]`가 없는 enum은 ArgumentException이다. 연결 수명 검사가 마스크 검증보다 우선한다. 판정은 HasAny이며 통과한 원래 ulong 값을 유지한다.
- 유효한 조건의 null 콜백은 등록하지 않고 같은 Builder를 반환한다. null target은 EqualityComparer&lt;TEventArgs&gt;.Default로 비교한다.
- OnUpdatedWhen과 OnUpdatedWhere는 서로 다른 이름이므로 null target과 null 조건을 구분하기 위한 형변환이 필요하지 않다. 콜백 자체에 null을 넘길 때는 Action과 Action&lt;TEventArgs&gt; 중 원하는 형식을 명시한다.
- 같은 연결·콜백 형식·원본 콜백 대리자·조건의 중복 등록은 InvalidOperationException이다. 조건의 비교와 노드 공유, 콜백 등록 단위는 [EventHubBuilder](EventHubBuilder.md)를 따른다.

[모듈 구성](README.md)
