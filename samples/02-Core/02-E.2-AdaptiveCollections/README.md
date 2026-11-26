# AdaptiveCollections 예시

순회 중에 항목을 추가·제거할 때 방문 대상이 어떻게 정해지는지 보여 주는 예시이다.

## 실행

```powershell
dotnet run --project samples/UniEngine.Samples.csproj -p:RestoreConfigFile=samples/NuGet.config -- adaptive-collections
```

## 내용

- 기본값인 지연 추가에서는 순회 중 추가한 항목을 다음 순회부터 방문한다. deferredUpdate를 false로 지정하면 같은 순회에서 방문한다. Count는 두 경우 모두 추가를 반영한다.
- 순회 중 제거한 항목은 남은 순회에서 방문하지 않는다.
- AdaptiveAction은 콜백이 자신을 제거할 수 있게 하고, 예외를 던진 콜백은 제거한 뒤 보고한다.
- AdaptiveDictionary는 순회 중 항목 제거를 허용한다. AdaptiveHashSet도 같은 변경 정책을 따른다.

## 출력

들여 쓴 줄은 SampleEnvironment의 처리기가 출력한 보고이다.

```text
Deferred pass visits: A, B, count after: 3
Immediate pass visits: A, B, C, count after: 3
Pass that removes B visits: A, C
Listener: runs only once
Listener: runs every tick
  [Reported] InvalidOperationException: Broken listener.
Listener: runs every tick
Listeners left: 1
Cooldowns left: Heal
```

## 관련 문서

- [AdaptiveCollections 모듈](../../../docs/02.%20UniEngine/02-Core/02-E.2-AdaptiveCollections/README.md)
- [예시 목록](../../README.md)
