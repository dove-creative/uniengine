# LifecycleHub

갱신 이벤트 없이 객체의 폐기 완료만 연결된 수신자에게 알리는 허브이다.

## 목차

1. 역할과 구성
2. 동작 흐름
3. 설계 기준과 고려사항
4. 구현 연결

## 역할과 구성

- 책임: 갱신 전달이 필요 없는 객체에 폐기 완료 연결만 제공한다.
- 보유 정보: 수신자별 완료 콜백 사전, 직접 OnDisposed 구독 목록과 폐기 상태를 보관한다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| Connect·Builder.OnDisposed | 수신자별 완료 반응을 구성하여 소유자의 끝을 관찰하게 한다. |
| Disconnect | 종료 전에 더 이상 필요 없는 수신자 연결을 제거한다. |
| Dispose·OnDisposed | 완료 상태를 통지하고 허브가 보관한 연결 참조를 정리한다. |

## 동작 흐름

Dispose는 IsDisposed를 먼저 true로 바꾸고 연결별 콜백의 스냅샷을 실행한 뒤 직접 OnDisposed 구독을 호출한다. 실패한 연결을 제거하고 ExceptionHandler에 보고하며 finally에서 전체 참조를 비운다.

## 설계 기준과 고려사항

완료 알림만 필요한 객체에 갱신·수면·필터 상태를 추가하지 않도록 EventHub보다 작은 책임을 가진다. 완료 상태를 먼저 확정한 뒤 스냅샷을 통지하여 완료 알림 안에서 새 연결이 생기지 않게 한다.

- 등록은 Builder.OnDisposed에서 확정되며 같은 수신자의 중복 등록은 Dictionary.Add에서 거부한다.
- 폐기 중·완료 후 Disconnect는 무시하므로 통지 중 다른 연결의 스냅샷을 취소하는 용도로 사용할 수 없다.
- 공개 Dispose 메서드는 있지만 현재 형식 선언에 IDisposable 구현은 없다.

## 구현 연결

[LifecycleHub.cs](../../../../src/02-Core/03-E-EventHub/LifecycleHub.cs) · [LifecycleHubBuilder.cs](../../../../src/02-Core/03-E-EventHub/LifecycleHubBuilder.cs) · [모듈 구성](README.md)
