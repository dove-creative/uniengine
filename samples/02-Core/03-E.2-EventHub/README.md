# EventHub 예시

객체의 갱신과 폐기를 연결된 수신자에게 알리고, 수면 중의 갱신을 한 번으로 모으는 예시이다.

## 실행

```powershell
dotnet run --project samples/UniEngine.Samples.csproj -- event-hub
```

## 내용

- Unit은 EventHub를 소유하고 IEventSubject로 노출한다.
- HealthBar는 OnUpdated로 모든 갱신을, OnUpdatedWhen으로 체력이 0이 된 갱신을 받고, OnDisposed에서 보관한 참조를 비운다.
- Disconnect는 해당 수신자의 콜백을 한 번에 해제한다.
- Sleep 범위 안의 갱신은 생략되고, 범위가 끝나면 onAwake가 최종 값을 한 번 발행한다.

## 출력

서로 다른 수신자 사이의 출력 순서는 보장하지 않는다.

```text
Health bar: 6
Combat log: health 6
Batch: two hits applied while asleep
Health bar: 4
Health bar: 0
Health bar: defeated
Health bar has target: False
```

## 관련 문서

- [EventHub 모듈](../../../docs/02.%20UniEngine/02-Core/03-E.2-EventHub/README.md)
- [EventHub 사용 예제](../../../docs/02.%20UniEngine/02-Core/03-E.2-EventHub/Usage.md)
- [예시 목록](../../README.md)
