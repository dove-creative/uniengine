# 경로 연산자

연산자는 두 종류이다. 상태가 없는 Where, When, ForFlags, Debug, ForOrder, ForOrderAsync는 경로를 기록하고, 상태가 있는 Take·TakeWhile·Skip·SkipWhile은 새 EventStream을 시작한다. 그래서 공유되는 경로 노드에는 상태가 없다.

| 연산 | 값이 도달할 때의 동작 | 결과 | 공유 범위 |
| --- | --- | --- | --- |
| Where(predicate) | 참일 때만 하위를 방문한다. | EventPath | 같은 부모 아래 같은 대리자 |
| When(target) | EqualityComparer&lt;T&gt;.Default로 값 전체가 같을 때 하위를 방문한다. | EventPath | 같은 부모 아래 동등한 target |
| ForFlags(mask) | ulong 값과 마스크에 공통 비트가 하나라도 있을 때 하위를 방문한다. | EventPath | 같은 부모 아래 같은 마스크 비트 |
| Debug | 값과 메시지를 로그로 기록하고 다음 노드로 전달한다. | EventPath | 해당 경로 등록 |
| ForOrder(order) | 순서 그룹으로 묶어 같은 위치의 그룹끼리 값이 작은 쪽부터 방문한다. | EventPath | 같은 부모 아래 같은 순서 값 |
| ForOrderAsync(order) | 같은 숫자의 ForOrder보다 먼저 병렬 구독을 모두 마친다. API와 OnNext 반환은 동기이다. | EventPath | 같은 부모 아래 같은 순서 값·실행 방식 |
| Skip(n) | 처음 n개를 버리고 이후 값을 전달한다. 0 이하이면 처음부터 전달한다. | EventStream | 반환된 스트림 |
| SkipWhile(predicate) | 처음 거짓인 값부터 전달하며 이후 조건 검사를 멈춘다. | EventStream | 반환된 스트림 |
| Take(n) | n번째 값을 전달한 뒤 완료한다. 0 이하이면 처음 도달한 값을 전달하지 않고 완료한다. | EventStream | 반환된 스트림 |
| TakeWhile(predicate) | 참인 동안 전달한다. 처음 거짓인 값은 전달하지 않고 완료한다. | EventStream | 반환된 스트림 |

## 조건과 공유

Where·When·Debug·ForOrder·ForOrderAsync는 EventStream과 EventPath의 인스턴스 메서드이다. `stream.Where(predicate)`나 `stream.When(target)`으로 첫 경로를 만들고, 반환된 경로에 연산을 이어 붙일 수 있다. 경로 구성만으로 구독이 생기지는 않으며 Subscribe할 때 등록한다. EventPathExtensions의 Where·Debug·ForOrder를 정적으로 호출하던 코드는 스트림의 인스턴스 호출로 변경한다. ForFlags는 EventPathExtensions에 남으므로 기존 호출을 그대로 사용한다. 이전 DLL에 대해 컴파일한 소비 프로젝트는 새 DLL을 참조해 다시 빌드한다.

조건은 `Func<T, bool>` 대리자로 받는다. Where는 메서드와 대상이 같은 대리자를 같은 조건으로 본다. 정적 메서드, 필드에 저장한 대리자, 같은 객체의 인스턴스 메서드는 공유된다. 호출마다 새 클로저를 만드는 람다나 위치가 다른 람다는 본문이 같아도 별도 조건으로 평가한다. 같은 대리자를 서로 다른 순서 그룹이나 다른 스트림에서 쓰면 각각 평가한다. SkipWhile과 TakeWhile의 조건은 공유하지 않는다.

같은 부모 연결 위치의 Where는 WhereNode 하나에 모이고, 동등한 predicate마다 행 하나를 공유한다. 각 행은 자기 Gate와 다음 단계 노드를 참조하는 연결을 가진다. `Where(a).Where(b)`는 서로 다른 위치의 WhereNode 두 개이며 첫 노드의 a행이 두 번째 노드를 참조한다. 같은 위치의 `Where(a)`와 `Where(c)`는 WhereNode 하나의 a행과 c행이다. 행은 노드가 아니며 NodeCount에 포함하지 않는다.

Where는 각 행의 조건을 검사하고 참인 행의 연결을 방문한다. 행 간 호출 순서는 보장하지 않는다. 등록 검색은 16행 이하일 때 선형이며, 17번째 행을 등록하면 색인을 만든다. 줄어든 뒤에도 유지하고 마지막 행을 제거할 때 해제한다.

같은 위치의 같은 키는 행을 공유한다. 일반 노드·행·구독의 호출 순서는 보장하지 않는다. 등록·삭제는 루트·Order 종료 때 반영하며 새 구독은 소유 스트림의 다음 발행부터 참여한다. 같은 단위에서 삭제를 요청한 기존 구독도 그 단위의 현재 값까지 받는다. [반영 경계](OrderGroups.md)를 따른다.

## 값 조건과 공유

When은 스트림과 같은 형식의 target을 받고 EqualityComparer&lt;T&gt;.Default로 값 전체의 동등성을 판정한다. 참조 형식과 Nullable&lt;T&gt;의 null target도 유효하며 null 값과 일치한다. 조건 함수를 받는 When 오버로드는 없으며 함수 조건은 Where로 표현한다. object로 바꾸어 받는 별도 오버로드도 없다.

같은 부모 아래에는 WhenNode 하나를 두고, 동등한 target은 내부 행 하나를 공유한다. 별도로 만든 경로나 다른 구독·리스너라도 target이 같으면 공유하며, 부모나 순서 그룹이 다르면 공유하지 않는다. null이 아닌 target은 EqualityComparer&lt;T&gt;.Default를 사용하는 사전으로 찾고 null target은 별도 행으로 찾는다. 등록 검색과 발행 모두 이 기준을 사용하며, 발행할 때는 값과 같은 행 하나만 방문한다. 사전에서 찾은 행도 연결 상태와 발행 시작 시점의 Registration을 확인한 뒤 방문한다. 값 형식의 null 검사에는 박싱이 없다. 등록한 뒤 target의 Equals·GetHashCode 결과가 바뀌는 경우는 지원하지 않는다.

WhenNode와 다른 경로의 호출 순서는 보장하지 않는다. 완료·오류·폐기는 target과 관계없이 모든 행에 전달한다. 등록·삭제는 루트·Order 경계에서 반영하며 새 구독은 다음 발행부터 받는다. 연결이 빈 행은 검색 구조에서도 제거하고 마지막 행이 사라지면 노드도 제거한다.

When 호출은 경로 깊이 한 단계이며 기존 256단계 제한을 따른다. Where·ForOrder·ForFlags와 상태 있는 연산자를 앞뒤로 조합할 수 있고, When에서 탈락한 값은 뒤의 Take·Skip에 도달하지 않는다. `EventStream<ulong>`의 When은 전체 값 비교이고 ForFlags는 공통 비트 판정이다. 두 연산은 같은 부모 아래에서도 서로 다른 노드를 사용한다. When의 성능은 측정하지 않았으며 Unity Mono·IL2CPP에서의 할당도 검증하지 않았다.

## 비트 조건과 공유

ForFlags는 `EventStream<ulong>`과 `EventPath<ulong>`에서 flags enum을 마스크로 받는다. `(valueBits & maskBits) != 0`인 HasAny 판정이므로 여러 비트 중 하나만 겹쳐도 통과하고, 값이 0이면 어떤 마스크에도 전달하지 않는다. enum 기반 형식은 제한하지 않으며, 마스크는 [ToBits](../01-O.1-Utilities/FlagExtensions.md)로 변환해 선언된 비트 폭을 보존한다. 경로를 구성하는 ForFlags 호출 시점에 `[Flags]`가 없는 enum이나 비트가 0인 마스크는 ArgumentException으로 거부한다. ulong 마스크를 직접 받거나 enum 형식 스트림에서 호출하는 오버로드는 없다.

같은 부모 아래에는 FlagsNode 하나를 두고, 같은 마스크 비트는 내부 행 하나를 공유한다. enum 형식이 달라도 비트가 같으면 공유하며, 다른 마스크나 다른 순서 그룹의 행은 공유하지 않는다. 각 FlagsNode는 발행마다 값의 비트를 한 번 읽고, 행을 생성 순서대로 체인 순회하며 판정한다. 등록 검색은 Where와 같이 16행까지 선형이고 17번째 행 등록 시 색인을 만든다. 줄어든 뒤에도 유지하며 마지막 행을 제거할 때 해제한다.

FlagsNode와 다른 일반 경로의 호출 순서는 보장하지 않는다. 등록·삭제는 루트·Order 종료 때 반영하며 새 구독은 다음 발행부터 참여한다. 완료·오류·폐기는 마스크와 관계없이 모든 행의 연결에 전달한다.

ForFlags는 경로 깊이 한 단계로 센다. 앞뒤로 Where·Debug·ForOrder와 Take·Skip·Branch 등을 조합할 수 있으며, ForFlags에서 탈락한 값은 뒤의 상태 있는 연산자에 도달하지 않는다. Where 대비 성능은 측정하지 않았다.

## 상태 있는 연산자

상태는 연산자를 호출할 때 만들어진 스트림의 입구 노드가 가진다. 같은 스트림을 구독한 쪽끼리 남은 횟수와 진행 상태를 공유하고, 연산자를 다시 호출하면 새 상태로 시작한다.

- 스트림은 첫 구독 때 상위에 연결된다. 직접 입력한 값은 구독 여부와 관계없이 연산 상태에 반영된다. Enabled가 false이면 상위 입력과 직접 입력 모두 상태를 바꾸지 않는다. 늦게 구독한 쪽은 남은 상태를 이어받는다.
- 먼저 적용한 Where·When·ForFlags에서 탈락한 값은 뒤의 Take·Skip에 도달하지 않는다. 스트림에 직접 넣은 값은 위에서 온 값과 같게 센다.
- Take·TakeWhile이 끝나면 일반 종료 규칙에 따라 활성 구독에 완료를 통지하고 폐기된다. 마지막 값을 처리하는 동안 추가된 구독은 그 값을 받지 않고 완료만 받는다. 상위에서는 그 스트림으로 가는 연결만 빠진다.
- 끝난 스트림에 구독하면 ObjectDisposedException이 발생한다.
- 구독 전 직접 입력으로 Take·TakeWhile이 끝난 경우에도 폐기되며 이후 구독을 거부한다. 이 과정에서 상위 연결은 만들지 않는다.

## 실패와 비용

Where 조건 실패는 해당 행만, Debug 로그 실패는 해당 경로를, SkipWhile·TakeWhile 조건 실패는 해당 스트림을 정리한다. 실패가 처리되면 다른 경로를 계속하며 처리기가 없거나 보고가 재전파되면 미실행 호출을 중단한다.

When의 발행 중 Equals·GetHashCode 실패는 같은 위치의 모든 target 경로를 통지 없이 정리한다. 검색 사전과 구독은 소유한 루트·Order 경계에서 정리하며, 사전을 먼저 비워 실패한 비교를 다시 호출하지 않는다. 등록 중 비교 실패는 빈 행·노드를 남기지 않으며, 대기 중 등록은 반영 경계에서 실패를 보고한다. 첫 구독 실패로 빈 관계 스트림은 상위 연결을 해제하고 폐기된다.

Take의 마지막 값에서 미처리 예외가 발생하면 미실행 값·완료 통지를 생략하고 연결을 정리한다. 값의 실패가 처리되면 완료 통지를 진행하며, 완료 통지의 미처리 실패도 이후 통지를 중단한다. 정리 중의 오류가 먼저 기록한 예외를 덮어쓰지 않는다.

상태 있는 연산자는 호출할 때마다 스트림을 하나 만든다. 고정된 구독에서는 전파 할당이 없지만, 매 프레임 Take(1)을 만드는 코드는 그만큼 할당이 늘어난다. Debug는 값을 문자열로 포맷해 로그를 쓰므로 그 비용은 무할당 전파 측정에서 제외한다. [전파와 실패 규칙](EventStream.md)

[모듈 구성](README.md)
