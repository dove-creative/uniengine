# LifecycleHub 예시

객체가 폐기될 때 연결된 수신자에게 알리고, 수신자가 보관한 참조를 정리하는 예시이다.

## 실행

```powershell
dotnet run --project samples/UniEngine.Samples.csproj -p:RestoreConfigFile=samples/NuGet.config -- lifecycle-hub
```

## 내용

- Door는 LifecycleHub를 소유하고 ILifecycleSubject로 노출한다.
- 수신자는 자신을 키로 Connect하고 OnDisposed로 폐기 반응을 등록한다. DoorSensor는 폐기 알림에서 보관한 참조를 비운다.
- Disconnect한 수신자는 폐기 알림을 받지 않는다.

## 출력

수신자 사이의 알림 순서는 보장하지 않는다.

```text
Sensor: door removed, reference cleared
Minimap: door icon removed
Door disposed: True, sensor has target: False
```

## 관련 문서

- [LifecycleHub 모듈](../../../docs/02.%20UniEngine/02-Core/03-E.1-LifecycleHub/README.md)
- [예시 목록](../../README.md)
