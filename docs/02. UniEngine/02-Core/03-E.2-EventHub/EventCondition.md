# EventCondition

갱신 조건과 로그 단계를 루트부터 순서대로 조합하여 여러 등록에서 재사용하는 불변 조건이다.

## 목차

- 역할과 구성
- 동작 흐름
- 동등성과 노드 공유
- 설계 기준과 고려사항

## 역할과 구성

- 책임: 조건의 구성을 연결 수명과 분리하고, 등록할 때 적용할 단계의 순서와 의미를 보존한다.
- 보유 정보: EventCondition&lt;T&gt;는 단계 종류와 값의 불변 목록을 보관한다. EventHub나 리스너를 소유하지 않는다.
- 구성 진입점: 정적 EventCondition은 Where·When·Flags로 첫 단계를 만든다. EventCondition&lt;T&gt;의 Where·When·Debug와 ulong 조건의 Flags 확장은 뒤에 단계를 추가한 새 조건을 반환한다.

| 단계 | 입력과 의미 | 스트림 경로 |
| --- | --- | --- |
| Where | `Func<T, bool>`이 true일 때 다음 단계로 전달한다. | Where 노드이다. |
| When | target과 값을 EqualityComparer&lt;T&gt;.Default로 비교한다. | 같은 부모의 동등한 target이 공유하는 WhenEntry이며 WhenNode 아래에 모인다. |
| Flags | ulong 값과 `[Flags]` enum 마스크의 공통 비트가 하나라도 있을 때 통과한다. | 기존 Flags 노드이며 원래 ulong 값을 유지한다. |
| Debug | 메시지와 로그 수준으로 통과한 값을 기록한다. 기본값은 `"Debug"`, LogLevel.Log이다. | 공유하지 않는 Debug 노드이다. |

## 동작 흐름

조건은 루트 쪽부터 쌓는다. 단계 추가는 원본을 바꾸지 않으므로 공통 조건을 정적 필드에 보관하거나, 그 조건에서 서로 다른 후속 조건을 만들어 여러 리스너에 등록할 수 있다. Debug는 형식이 결정된 조건에 이어 붙이는 메서드만 제공한다.

```csharp
var positive = EventCondition.Where<int>(value => value > 0);
var selected = positive.When(3).Debug("Selected");

builder.OnUpdated(positive, OnPositive);
builder.OnUpdated(selected, OnSelected);
```

위 구조에서 두 등록은 양수 판정 단계를 공유한다. 두 번째 등록만 값 3을 비교하고 로그를 남긴다. OnUpdated는 같은 Builder를 반환하며 조건 객체를 바꾸지 않는다. Connection은 내부 PathInfo의 단계 구조를 사용하여 조건을 EventStream 경로에 연결한다.

단축형 OnUpdated(target, callback), OnUpdated(predicate, callback), OnFlags(mask, callback)은 각각 When·Where·Flags 한 단계 조건의 등록과 같다. 중복 판정과 전파 순서도 같은 규칙을 따른다.

## 동등성과 노드 공유

등록 중복은 조건 객체나 경로 객체의 참조가 아니라 단계의 값으로 판정한다. 단계 수가 같고 대응하는 단계의 종류와 아래 값이 모두 같아야 같은 조건이다. 조건이 없는 루트 등록은 루트끼리만 같다. 이 비교는 등록 중복에 사용하는 규칙이며 EventCondition의 공개 Equals 계약을 바꾸지 않는다.

| 단계 | 중복 판정에 사용하는 값 |
| --- | --- |
| Where | 원본 조건 대리자의 Equals 결과이다. object 조건도 변환 람다가 아닌 원본 대리자를 사용한다. |
| When | target을 EqualityComparer&lt;T&gt;.Default로 비교한다. |
| Flags | 변환한 마스크 비트이다. enum 형식이 달라도 비트가 같으면 같다. |
| Debug | 메시지와 로그 수준이다. |

같은 연결에서 콜백 형식과 원본 콜백 대리자도 같으면 중복을 거부한다. 따라서 target 단축형과 같은 값의 When 조건, 따로 만든 같은 값의 When 조건은 같은 콜백으로 함께 등록할 수 없다. 조건이나 콜백 형식이 다르면 별도 등록이다.

노드 공유는 EventStream의 규칙을 따른다. 같은 위치의 같은 조건 대리자, 동등한 target, 같은 마스크 비트는 리스너가 달라도 공유하고 Debug는 공유하지 않는다. target 단축형과 별도로 만든 같은 값의 When 조건도 같은 WhenEntry를 공유한다. 부모나 순서 그룹이 다르면 공유하지 않는다. 같은 위치의 target들은 WhenNode 하나에 묶이고, 다른 형제와의 순서는 그 묶음의 생성 순서이다. 발행 값에 해당하는 항목 하나만 방문한다.

## 설계 기준과 고려사항

- 조건 생성과 단계 추가 시 입력을 검증한다. null 조건 대리자는 ArgumentNullException이다. When은 같은 형식의 target만 받으며 참조 형식과 Nullable&lt;T&gt;의 null target도 유효한 비교 값이다. 함수 조건은 Where로 받으며 When의 조건 함수·object 전용 오버로드는 없다.
- Flags는 ulong 조건에만 제공하며 `where TFlags : struct, Enum` 제약을 가진다. `[Flags]`가 없는 enum과 비트가 0인 마스크는 ArgumentException이다. enum의 선언 비트 폭을 보존하며 값 0은 통과하지 않는다.
- 최대 깊이는 256단계이다. 이를 넘기는 단계 추가는 기존 스트림 경로와 같은 제한으로 거부한다.
- 불변성은 단계 목록과 재사용을 보장한다. 대리자가 캡처한 외부 상태나 target 객체 자체를 복제하거나 불변으로 만들지는 않는다. 등록 뒤 target의 Equals·GetHashCode 결과가 바뀌는 경우는 지원하지 않는다.
- 조건은 허브 연결과 독립적으로 만들 수 있다. Builder의 단축형과 OnFlags 호출 안에서 이루어지는 조건 생성·검증은 연결 수명 검사 뒤에 수행한다.
- When의 발행 중 비교 예외는 같은 위치의 WhenNode와 모든 target 하위를 통지 없이 해제하고 ExceptionHandler.Report로 보고한다. 형제 경로는 계속 방문한다. 등록 중 비교 예외는 등록 호출자에게 전파하고 실패한 등록으로 새로 만든 빈 노드는 남기지 않는다.
- EventStream·EventPath의 When 경로를 사용하며 Where·Debug·ForFlags의 기존 계약은 유지한다. ulong의 When은 전체 값 비교이므로 Flags와 함께 사용해도 서로 다른 경로이다.
- When의 성능은 측정하지 않았으며 Unity Mono·IL2CPP에서의 할당도 검증하지 않았다.

[Builder 계약](IEventHubBuilder.md) · [사용 예제](Usage.md) · [모듈 구성](README.md)
