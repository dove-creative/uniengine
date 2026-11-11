# Handle

실행할 동작을 저장해 두었다가 사용자가 원하는 시점에 Dispose를 호출하면 실행하는 객체이다.

## 목차

- 역할과 구성
- 동작 흐름
- 설계 기준과 고려사항

## 역할과 구성

- 책임: 생성 시 받은 동작을 보관하고 첫 폐기 과정에서 호출한다.
- 목적: 동작을 정하는 시점과 실행하는 시점을 분리하여, 사용자가 원하는 시점까지 실행을 유예할 수 있게 한다.
- 보유 정보: 설명 문자열 Description, 폐기 상태 IsDisposed, protected internal Disposed 콜백을 보관한다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| Handle(Action) | 나중에 실행할 동작을 받아 보관하여 실행 시점을 호출자에게 맡길 수 있게 한다. || Dispose | 호출자가 저장된 동작의 실행 시점을 정하고, 같은 객체의 반복 폐기로 동작이 다시 실행되는 것을 막는다. || ThrowIfDisposed | 파생 형식이 폐기 이후의 새 작업을 거부할 공통 검사를 제공한다. |

연결 해제와 자원 정리는 대표적인 활용이다. 제공자가 내부 해제 절차를 Handle에 담아 반환하면, 소비자는 그 절차를 알지 않아도 반환 객체를 보관했다가 자신의 사용 수명이 끝날 때 정리를 요청할 수 있다.

## 동작 흐름

1. 제공자가 나중에 실행할 동작을 콜백으로 전달하여 Handle을 만든다. 생성 시에는 콜백을 보관한다.
2. 소비자는 Handle을 보관했다가 원하는 시점에 Dispose를 호출한다.
3. 첫 Dispose가 IsDisposed를 true로 바꾼 뒤 저장된 호출 목록을 순서대로 동기적으로 실행한다.
4. 콜백 실행이 끝나거나 예외로 중단되더라도 전체 콜백 참조를 비운다.

다음 예시는 생성 시에는 동작을 저장하고 첫 Dispose에서 실행하는 흐름을 보여준다. 반복 Dispose는 실행 횟수를 늘리지 않는다.

```csharp
using System;
using Blackthunder.UniEngine.Core;

var executionCount = 0;
var handle = new Handle(() => executionCount++);

Console.WriteLine(executionCount); // 0

handle.Dispose();
handle.Dispose();

Console.WriteLine(executionCount); // 1
Console.WriteLine(handle.IsDisposed); // True
```

실행 시점이 사용 범위의 끝과 같다면 using으로 Dispose를 호출하게 할 수도 있다.

콜백이 실패하면 해당 콜백을 제거하고 ExceptionHandler에 보고한다. 보고가 정상 반환하면 다음 콜백을 실행하며, 예외가 전파되면 남은 콜백은 실행하지 않는다.

## 설계 기준과 고려사항

실행 내용은 콜백에, 실행 시점은 Handle을 보관한 호출자에게 맡긴다. 이 분리를 통해 특정 연결이나 자원 형식에 의존하지 않고 실행을 유예할 수 있다. 콜백 실행 전에 폐기 상태를 전환하여 재진입으로 같은 폐기 과정이 다시 시작되는 것을 막는다.

- 반복 Dispose는 무시한다.
- 실행 유예는 Dispose를 나중에 호출한다는 뜻이다. 저장된 콜백은 Dispose 호출 안에서 동기적으로 실행된다.
- ExceptionHandler에 처리기가 없으면 원래 예외가 전파되며, 등록된 처리기가 던진 예외도 호출자에게 전파된다. 어느 경우에도 폐기 상태와 콜백 참조 정리는 유지된다.
- Disposed는 동작을 보관하는 이벤트이고 IsDisposed는 상태 값이다. Disposed의 접근 수준은 protected internal이므로 파생 형식과 같은 어셈블리의 코드가 해제 알림을 등록할 수 있다.
- 현재 공개 생성자는 Action을 받으며, 인자 없는 생성자는 protected internal이다. 생성 이후의 공개 구독·해제 기능은 [Token](Token.md)이 제공한다.
- bool로의 암시적 변환은 제공하지 않는다. 폐기 전 객체인지 확인할 때는 null 여부와 IsDisposed를 검사한다.
- Handle 자체가 연결 대상이나 자원을 자동으로 찾아 해제하지는 않는다.

[모듈 구성](README.md)
