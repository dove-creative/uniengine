# Token

저장한 동작을 원하는 폐기 시점에 실행하되, 생성 이후에도 실행할 동작을 추가·제거할 수 있도록 Handle을 확장한 객체이다.

## 목차

1. 역할과 구성
2. 동작 흐름
3. 설계 기준과 고려사항

## 역할과 구성

- 책임: Handle의 실행 유예 기능을 유지하면서 생성 이후에도 실행할 동작의 구성을 변경할 수 있게 한다.
- 목적: 생성 시 실행할 동작이 모두 정해지지 않은 경우에도 여러 참여자의 동작을 같은 폐기 시점에 묶을 수 있게 한다.
- 보유 정보: Description, IsDisposed, protected Disposed 콜백은 Handle에서 상속하며 공개 Action 이벤트가 같은 콜백 목록에 접근한다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| 생성자 | 빈 동작 목록으로 시작하거나 초기 Action을 받아 이후 참여자가 동작을 구성할 수 있게 한다. |
| Action 구독·해제 | 생성 뒤에도 동작을 추가하고 더 이상 실행할 필요가 없는 동작을 제거한다. |
| Dispose | Handle의 폐기 정책에 따라 등록된 동작들을 호출자가 정한 시점에 실행한다. |

## 동작 흐름

1. Token을 빈 상태로 만들거나 초기 동작을 전달하여 만든다.
2. 참여자가 공개 Action 이벤트를 구독하거나 구독을 해제하여 폐기 시 실행할 동작을 구성한다.
3. 소비자가 Dispose를 호출하면 Handle이 먼저 폐기 상태를 설정하고 그 시점에 등록된 호출 목록을 동기적으로 실행한다.
4. 폐기가 끝나거나 예외로 중단되면 Handle이 콜백 참조를 비운다.

다음 예시는 초기 동작에 새 동작을 추가하고, 그중 하나를 제거한 뒤 폐기하는 흐름이다. 제거할 동작은 등록할 때 사용한 대리자를 보관하여 같은 대리자로 구독을 해제한다.

```csharp
using System;
using Blackthunder.UniEngine.Core;

var token = new Token(() => Console.WriteLine("initial"));
var additionalAction = new Action(() => Console.WriteLine("additional"));
var removedAction = new Action(() => Console.WriteLine("removed"));

token.Action += additionalAction;
token.Action += removedAction;
token.Action -= removedAction;

token.Dispose(); // Prints initial, then additional.
token.Dispose(); // No additional calls.

token.Action -= additionalAction; // Ignored after disposal.
```

첫 Dispose에서는 남아 있는 initial과 additional 동작이 순서대로 실행된다. 폐기 이후의 구독 해제와 반복 Dispose는 무시한다.

EventHub는 이 흐름을 Sleep 해제 신호로 사용한다. Token에 깨어날 때의 동작을 연결하고, Token을 폐기하는 시점에 해당 동작이 실행되도록 한다.

## 설계 기준과 고려사항

[Handle](Handle.md)의 상태 전환과 실행·정리 절차를 그대로 사용하면서 콜백 구성만 공개한다. Handle이 생성 시 받은 동작의 실행 시점을 맡기는 객체라면, Token은 그 실행 시점까지 어떤 동작을 실행할지도 바꿀 수 있는 객체이다.

- Action은 구독·해제를 위한 이벤트이며, 저장된 동작의 실행은 Dispose를 통해 이루어진다.
- Dispose가 시작되어 IsDisposed가 true가 된 이후 새 Action 구독은 ObjectDisposedException을 발생시킨다. 구독 해제와 반복 Dispose는 무시한다.
- 콜백 실패 시 예외 보고와 참조 정리는 Handle의 정책을 따른다. 예외 보고에서 예외가 전파되면 남은 콜백은 실행하지 않는다.
- 폐기 상태는 상속한 IsDisposed로 확인하며, bool로의 암시적 변환은 제공하지 않는다.
- CancellationToken과 별개의 타입이다.

[모듈 구성](README.md)
