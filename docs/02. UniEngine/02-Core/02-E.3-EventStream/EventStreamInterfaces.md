# 공급원과 관찰자 계약

공급원은 외부에서 값을 받아 발행할 수 있다. 그래프는 공급원을 한 번 구독하고, 등록된 경로를 따라 그 값을 전달한다.

| 계약 | 역할 |
| --- | --- |
| `IEventSource<T>` | IObserver, IObservable, IDisposable을 결합한다. Enabled, TerminationMode, Chain·Connect를 제공한다. 기본 구현은 EventSource이다. |
| `IObservable<T>` | 그래프가 요구하는 외부 공급원의 최소 계약이다. EventStream도 이 계약을 통해 다른 스트림의 외부 공급원이 될 수 있다. |
| `IObserver<T>` | 값·완료·오류 콜백을 하나의 수신자로 등록한다. |
| IPassiveObserver | 공급원에서 Chain·Connect로 연결할 때 받은 핸들을 보관하고 수신자 자신의 폐기 시 해제한다. |

## 사용 기준

외부 시스템이 값을 발행하고 앱이 그중 일부를 골라 받을 때는 외부 시스템이 EventSource를 보관하고, 앱이 그 공급원으로 EventStream을 만든다. 구독자에게는 EventPath나 IObservable을 전달해 필요한 권한만 제공할 수 있다.

같은 수신자의 구독도 매번 별도 핸들이 생긴다. 그래프를 폐기하면 외부 공급원과의 연결을 해제하고 외부 공급원 자체의 폐기는 그 소유자에게 맡긴다. 기본 공급원 예제는 [사용 예제](Usage.md)에 있다.
