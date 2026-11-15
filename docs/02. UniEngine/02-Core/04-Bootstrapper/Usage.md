# Bootstrapper 사용 예제

각 예제는 별도 프로세스에서 실행한다. Bootstrapper는 한 번 성공한 뒤 다시 초기화할 수 없다.

## 콘솔 기본 환경

기본 설치 경로는 실행 파일의 기본 디렉터리이고 애플리케이션 ID는 진입 어셈블리 이름이다. 초기화가 실패하면 실행 루프로 진입하지 않는다. 아래의 짧은 루프는 호스트가 경과 시간을 계산하여 틱을 발행하는 위치를 보여 준다.

```csharp
using System.Diagnostics;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;

Bootstrapper.Initialize();
using var subscription = EventStream.Main.Subscribe(deltaTime => Log.Write($"Tick: {deltaTime}"));
var clock = Stopwatch.StartNew();
var previous = clock.Elapsed.TotalSeconds;
try
{
    for (int frame = 0; frame < 3; frame++)
    {
        var current = clock.Elapsed.TotalSeconds;
        EventStream.Main.OnNext((float)(current - previous));
        previous = current;
    }
}
finally
{
    EventStream.Main.Dispose();
    Log.Print();
}
```

## 호스트 설정과 애플리케이션 초기화

initialize 콜백에서는 로그와 Main을 사용할 수 있다. IsInitialized는 콜백 반환 뒤에만 true가 된다. 초기화 중 예외는 호스트의 catch로 직접 전달되며, 지정한 예외 처리 정책은 이후 일반 실행에서 사용한다.

```csharp
using System;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;

IDisposable ticks = null;
try
{
    Bootstrapper.Initialize(
        installationPath: AppContext.BaseDirectory,
        logger: (message, level) => Console.WriteLine($"[{level}] {message}"),
        exceptionHandler: exception => Console.Error.WriteLine(exception),
        applicationId: "ExampleApplication",
        initialize: () =>
        {
            ticks = EventStream.Main.Subscribe(deltaTime => Log.Write($"Update: {deltaTime}"));
            Log.Write("Application initialized");
        });
}
catch (Exception exception)
{
    ticks?.Dispose();
    Console.Error.WriteLine(exception);
    return;
}

try
{
    EventStream.Main.OnNext(0.016f);
}
finally
{
    ticks?.Dispose();
    EventStream.Main.Dispose();
    Log.Print();
}
```

[모듈 구성](README.md)
