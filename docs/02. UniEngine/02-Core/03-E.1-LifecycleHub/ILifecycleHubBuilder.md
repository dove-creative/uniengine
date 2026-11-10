# ILifecycleHubBuilder

연결된 수신자의 폐기 완료 반응을 구성하는 최소 계약이다.

## 목차

- 역할과 구성
- 동작 흐름
- 설계 기준과 고려사항

## 역할과 구성

- 책임: 허브의 내부 저장 방식을 드러내지 않고 완료 반응을 등록한다.
- 상태 계약: 구성 대상과 콜백의 저장 방식은 구현체가 소유한다.
- 제공 서비스: OnDisposed는 인자 없는 완료 콜백을 받고 생애주기 Builder를 반환한다.

## 동작 흐름

수신자는 ILifecycleHub.Connect로 Builder를 얻고 OnDisposed로 완료 반응을 구성한다. 완료 알림의 실행 시점과 중복 등록 규칙은 허브 구현을 따른다.

## 설계 기준과 고려사항

Builder는 연결 해제 핸들이 아니며, 연결 제거는 허브의 Disconnect로 요청한다. [IEventHubBuilder](../03-E.2-EventHub/IEventHubBuilder.md)는 이 계약을 확장하여 갱신 경로와 폐기 전후 반응을 구성한다.

[모듈 구성](README.md)
