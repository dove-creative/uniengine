# EventStream 인터페이스

스트림의 입력·구독·연결 계약과, 연결 핸들을 하위에 넘기는 계약을 정의한다.

## 목차

1. 역할과 구성
2. 동작 흐름
3. 설계 기준과 고려사항
4. 구현 연결

## 역할과 구성

### IEventStream

- 책임: 구체 형식을 몰라도 스트림의 입력·구독·연결·수명을 같은 방식으로 다루게 한다.
- 결합한 계약: `IObservable<T>`, `IObserver<T>`, IDisposable
- 상태: Enabled, TerminationMode, IsDisposed

| 제공 서비스 | 제공 이유 |
| --- | --- |
| OnNext·OnCompleted·OnError | 스트림이 다른 스트림의 입력이 될 수 있게 한다. |
| Subscribe | 해제 핸들을 반환하여 구독을 언제 끝낼지 호출자가 정하게 한다. |
| Chain·Connect | Observer를 연결하고, 다음에 이어 붙일 대상을 반환값으로 구분한다. |
| With | 종료 정책을 설정하고 같은 스트림을 반환한다. 새 스트림을 만드는 연산자와 구분한다. |

### IPassiveObserver

- 책임: 외부에서 만든 상위 연결을 자신의 수명에 묶는다.
- 보유 정보: 넘겨받은 연결 핸들을 보관한다. 보관 방식은 구현체가 정한다.

TakeConnection은 상위가 만든 구독 핸들을 넘겨받는다. 연결을 만드는 쪽은 상위이지만, 그 연결이 필요 없어지는 시점은 하위가 안다. 두 객체가 다르므로 핸들을 건네는 계약을 따로 둔다.

## 동작 흐름

### 연결

1. 상위가 하위 Observer를 구독시키고 연결 핸들을 얻는다.
2. 하위가 IPassiveObserver이면 TakeConnection으로 핸들을 넘긴다. EventStream은 이 핸들을 상위 연결 집합에 보관했다가 자신의 Dispose에서 해제한다.
3. 인계가 실패하면 방금 만든 연결을 해제하고 인계 예외를 전달한다.
4. 연결 메서드에 따라 하위 또는 상위를 반환한다.

| 메서드 | 반환값 | 쓰는 경우 |
| --- | --- | --- |
| Chain | 연결한 하위 Observer | source → A → B처럼 직렬로 이어 붙일 때 |
| Connect | 현재 상위 스트림 | source → A, source → B처럼 같은 발신자에서 갈래를 낼 때 |

out 연결 핸들은 반환값과 별개로, 그 연결 하나를 끊을 권한을 준다.

### 오버로드

| 형태 | 정의 위치 | 호출 방법 |
| --- | --- | --- |
| Observer와 out 핸들을 받는 Chain·Connect | 구체 EventStream | 어떤 형식의 변수로도 호출할 수 있다. |
| out을 생략한 Chain·Connect | IEventStream 기본 구현 | IEventStream 형식으로 호출한다. |
| Observer를 새로 만드는 `Chain<TObserver>()`·`Connect<TObserver>()` | IEventStream 기본 구현 | IEventStream 형식으로 호출한다. |

기본 구현 메서드는 인터페이스 형식으로만 보인다. EventStream이나 연산자 형식의 변수에서 이 오버로드를 쓰려면 IEventStream 형식으로 받는다. 확장 메서드는 결과를 IEventStream 형식으로 반환하므로 그대로 이어 쓸 수 있다.

## 설계 기준과 고려사항

전달 계약과 수명 계약을 한 인터페이스로 묶어, 별도 변환 어댑터 없이 스트림을 단계적으로 잇게 한다. 저장 구조나 스케줄러는 이 계약에서 정하지 않는다.

- 기본 구현 메서드는 Observer를 만들거나 연결하기 전에 상위의 폐기 여부를 먼저 확인한다.
- Subscribe만 호출하면 하위가 IPassiveObserver여도 핸들을 넘기지 않는다. 이 경우와 IPassiveObserver가 아닌 수신자는 반환된 핸들을 호출자가 관리한다.
- TakeConnection은 연결 과정에서 호출되므로 사용자 코드가 직접 호출할 필요가 없다.
- 연결 해제는 상위 객체의 폐기와 다르다. 마지막 구독이 해제될 때 상위도 폐기되는지는 상위의 TerminationMode가 정한다.
- 인터페이스만으로 동시 접근이나 스케줄러 동작을 보장하지 않는다.

## 구현 연결

[EventStream](EventStream.md)은 두 계약을 함께 구현하여 이벤트 전달과 연결 수명을 관리한다. [사용 예제](Usage.md)는 구독 핸들 해제와 연산자 체인의 정리 흐름을 보여준다.

[모듈 구성](README.md)
