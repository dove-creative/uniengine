# EventStreamExtensions

연산자 생성과 연결을 한 번의 호출로 묶어 이벤트 경로를 간결하게 구성하는 정적 확장 기능이다.

## 목차

1. 역할과 구성
2. 동작 흐름
3. 설계 기준과 고려사항
4. 구현 연결

## 역할과 구성

- 책임: 연산자를 만들고 소스에 연결하는 절차를 모든 IEventStream에서 재사용하게 한다.
- 보유 정보: 없다. 호출할 때마다 필요한 연산자와 연결을 새로 만든다.

| 확장 메서드 | 만드는 스트림 | 기본 종료 정책 |
| --- | --- | --- |
| Append | 기본 EventStream | Complete |
| Debug·Skip·SkipWhile·Where·Take·TakeWhile | 이름과 같은 연산자 | NoSubscribers |
| Merge | CompletionMode가 All인 Merge | NoSubscribers |

Append를 제외한 모든 확장은 종료 정책을 직접 지정할 수도 있다.

## 동작 흐름

Append와 필터·진단 확장은 다음 순서로 경로를 구성한다.

1. 소스의 폐기 여부를 확인한다.
2. 연산자를 생성한다.
3. Chain으로 소스에 연결하고, 새 연산자를 IEventStream 형식으로 반환한다.

반환한 스트림이 다음 호출의 소스가 되므로, source.Where(...).Take(2)처럼 호출을 이어 쓰면 그 순서대로 경로가 만들어진다. 기존 소스의 입력 처리나 다른 구독은 바뀌지 않는다.

Merge는 생성자가 각 상위의 폐기 여부를 확인하고 Connect로 자신을 연결한다. 폐기된 상위가 있으면 ObjectDisposedException을 전달하고, 앞서 만든 연결도 정리한다. 정적 형태인 EventStreamExtensions.Merge(left, right)는 받은 상위만 모으고, 인스턴스 형태인 left.Merge(right)는 호출 대상인 left도 입력에 포함한다.

## 설계 기준과 고려사항

경로를 구성하는 일은 확장 메서드에 두고, 카운터·조건·연결 수명은 반환된 연산자가 소유하게 한다. 구성 편의 기능이 전파 상태를 따로 보관하지 않게 하는 분리이다.

- Append는 이미 있는 객체를 연결하는 메서드가 아니다. 새 기본 EventStream을 만드는 편의 기능이며, 기존 Observer는 Chain·Connect로 연결한다.
- Merge 확장은 항상 All 완료 정책을 사용한다. Any가 필요하면 Merge 생성자를 사용한다.
- 반환 형식이 IEventStream이므로 out을 생략한 Chain·Connect 같은 기본 구현 오버로드도 바로 호출할 수 있다.
- 연결 해제와 상위 객체의 폐기는 구분한다. 공통 수명은 [모듈 개요](README.md)를 따른다.

## 구현 연결

개별 연산자의 상태와 처리 흐름은 [EventStream 연산자](EventStreamOperators.md)에서 설명한다. [사용 예제](Usage.md)의 필터 조합과 병합 절은 반환된 하위 스트림을 구독하고 정리하는 흐름을 보여준다.

[소스](../../../../src/02-Core/02-E.3-EventStream/EventStreamExtensions.cs) · [모듈 구성](README.md)
