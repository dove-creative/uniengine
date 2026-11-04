# ILifecycleAndEventSubject

생애주기 또는 이벤트 허브를 제공하는 도메인 객체의 계약 묶음이다.

## 목차

1. 역할과 구성
2. 동작 흐름
3. 설계 기준과 고려사항

## 역할과 구성

- 책임: 도메인 객체가 제공하는 허브를 외부 수신자에게 공통 방식으로 연결한다.
- 상태 계약: ILifecycleSubject는 IsDisposed·LifecycleHub, IEventSubject는 EventHub, 제네릭 변형은 형식 있는 EventHub를 노출한다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| ILifecycleSubject | 객체의 폐기 계약·상태·생애주기 허브를 함께 노출한다. |
| IEventSubject | 이벤트 허브를 생애주기 허브의 역할과 연결한다. |
| IEventSubject&lt;TEventArgs&gt; | 페이로드 형식을 유지한 허브 접근점을 제공한다. |

## 동작 흐름

도메인 객체가 허브를 소유하고 수신자가 Subject를 통해 연결한다. 도메인 객체의 Dispose 구현이 자신의 상태와 허브 정리를 조율한다.

## 설계 기준과 고려사항

허브 자체와 허브를 소유하는 도메인 객체를 별개 계약으로 표현하여 객체의 기능과 이벤트 연결을 분리한다.

- 계약을 구현했다는 이유만으로 허브 생성이나 자동 폐기가 이루어지는 것은 아니다.
- 객체 자체의 IsDisposed와 허브 상태의 일치는 구현체 책임이다.

[모듈 구성](README.md)
