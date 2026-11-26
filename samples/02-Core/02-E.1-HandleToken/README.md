# HandleToken 예시

Handle과 Token이 폐기될 때 정리 동작을 한 번만 실행하는 예시이다.

## 실행

```powershell
dotnet run --project samples/UniEngine.Samples.csproj -p:RestoreConfigFile=samples/NuGet.config -- handle-token
```

## 내용

- Handle은 생성할 때 받은 정리 동작을 첫 Dispose에서 한 번만 실행한다.
- Token은 여러 소유자가 Action에 등록한 정리 동작을 Dispose에서 함께 실행한다.
- 실패한 정리 동작은 ExceptionHandler에 보고하고 나머지 동작을 계속 실행한다.
- 폐기된 Token에 동작을 등록하면 ObjectDisposedException이 발생한다.

## 출력

들여 쓴 줄은 SampleEnvironment의 처리기가 출력한 보고이다.

```text
Handle: texture lease released
Texture lease disposed: True
Token: music stopped
  [Reported] InvalidOperationException: Menu cleanup failed.
Token: input released
Registering on a disposed token is rejected (expected).
```

## 관련 문서

- [HandleToken 모듈](../../../docs/02.%20UniEngine/02-Core/02-E.1-HandleToken/README.md)
- [예시 목록](../../README.md)
