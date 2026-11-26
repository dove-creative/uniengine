# EventStream 예시

이벤트 스트림의 필터, 순서 그룹, 비트 조건, 횟수 제한, 분기, 병합, 병렬 구독을 보여 주는 예시이다.

## 실행

```powershell
dotnet run --project samples/UniEngine.Samples.csproj -p:RestoreConfigFile=samples/NuGet.config -- event-stream
```

## 내용

- Where는 조건으로 값을 거르고, When은 같은 값에만 반응한다.
- ForOrder는 구독 순서와 관계없이 enum 순서대로 그룹을 실행한다.
- ForFlags는 마스크와 비트가 하나라도 겹치는 값을 전달하며, 서로 다른 enum의 비트를 한 스트림에 함께 발행할 수 있다.
- Take가 반환한 스트림은 구독자끼리 남은 횟수를 공유한다. Branch는 부모의 값과 분기에 직접 넣은 값을 모두 전달한다.
- Merge는 두 입력의 값을 전달하고, 모든 입력이 완료되면 완료된다.
- 같은 순서 그룹의 ForOrderAsync 구독은 병렬로 실행되고, 동기 구독은 그 뒤 발행 스레드에서 실행된다.
- EventStream.Main은 [Bootstrapper 스니펫](../04-Bootstrapper/README.md)에서 다룬다.

## 출력

ForFlags 구독 사이의 출력 순서는 보장하지 않는다.

```text
Where enemy*: enemy-1
When boss: boss music starts
Simulate 1
Render 1
Flags: shape
Flags: shape
Flags: location
Take early: 10
Take early: 20
Take late: 20
Branch: 5
Branch: -3
Merge: Space
Merge: A
Merge: all inputs completed
Parallel results applied: 14 and 21
```

## 관련 문서

- [EventStream 모듈](../../../docs/02.%20UniEngine/02-Core/02-E.3-EventStream/README.md)
- [EventStream 사용 예제](../../../docs/02.%20UniEngine/02-Core/02-E.3-EventStream/Usage.md)
- [예시 목록](../../README.md)
