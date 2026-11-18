# GeoSystem 시각화 예제

노드를 드래그해 Pin·Clip·Route·Hold의 변화를 보고, 두 Tracer의 위치 기준과 Wheel의 분기 선택을 비교하는 데스크톱 예제이다.

## 실행

.NET 10 SDK가 필요하다. 명령은 저장소 최상위에서 실행한다.

```powershell
dotnet run --project samples/03-Services/GeoSystem/UniEngine.GeoSystem.Sample.csproj -c Release
```

인자 없이 실행하면 Avalonia 창을 연다. 첫 실행은 NuGet 패키지를 복원하므로 네트워크 연결이 필요하다. 브라우저나 별도 서버는 사용하지 않는다.

다시 빌드할 때는 실행 중인 예제 창을 먼저 닫아 실행 파일의 잠금을 해제한다.

창을 열지 않고 시나리오를 검증한다.

```powershell
dotnet run --project samples/03-Services/GeoSystem/UniEngine.GeoSystem.Sample.csproj -c Release -- verify
```

| 명령 | 동작 |
| --- | --- |
| 인자 없음 | 시각화 창을 연다. |
| `help` 또는 `--help` | 실행 방법을 출력한다. |
| `tracer` | Tracer와 Wheel 비교 창을 바로 연다. 기본 창의 'Tracer와 Wheel 열기' 버튼으로도 열 수 있다. |
| `verify` | 기본 장면 17개 조건과 Tracer 장면 9개 조건을 창 없이 검사한다. |

검증에 실패하거나 알 수 없는 명령을 지정하면 종료 코드 1을 반환한다. `samples/UniEngine.Samples.csproj`의 콘솔 명령과 `all` 실행에는 이 독립 프로젝트가 포함되지 않는다.

## 살펴보기

초기 그래프는 A → B → C → D의 직선 구간 세 개이다. Route는 이 구간을 순서대로 참조하며, 노란 Clip은 B–C의 20%부터 80%까지를 참조한다. 흰 화살표는 Route를 따라가는 SegmentPin이고, 보라색 원은 그 Pin을 따라가는 Hold이다.

| 조작 | 확인할 결과 |
| --- | --- |
| A–D 노드 드래그 | 구간·경로 길이와 Pin·Clip·Hold 위치가 함께 갱신된다. |
| 진행 위치 슬라이더 | 경로 전체 길이에 대한 비율로 위치와 회전을 조회한다. |
| 역방향 조회 | 같은 비율을 Route의 To에서부터 세며 회전도 반전된다. 체크를 바꾸면 기준점이 바뀌므로 위치가 이동할 수 있다. |
| 재생·일시정지 / 이동 속도 | 초당 거리 단위로 진행하고 끝에 도달하면 처음부터 반복한다. |
| 시작 핀·끝 핀 슬라이더 | B–C 내의 두 위치를 바꾼다. 시작 핀이 끝 핀을 지나면 Clip 방향이 반전되며, 겹치면 Clip이 무효화된다. |
| 처음으로 | 기존 그래프를 폐기하고 초기 장면과 재생 설정을 복원한다. |
| 휠 / 가운데 버튼 드래그 | 화면 확대·축소 / 이동 |
| 전체 보기 또는 화면에 포커스한 상태에서 `F` | 현재 노드를 화면에 맞춘다. |
| 화면에 포커스한 상태에서 `Space` | 재생·정지를 전환한다. |

최근 변경 패널은 엔진 객체가 보낸 변경 알림을 표시한다. 매 프레임 움직이는 Traveller·Hold의 알림은 패널에 쌓지 않는다.

## Tracer와 Wheel

```powershell
dotnet run --project samples/03-Services/GeoSystem/UniEngine.GeoSystem.Sample.csproj -c Release -- tracer
```

초록색 원은 RouteBased Tracer, 주황색 화살표는 Absolute PolyTracer이다. 둘 다 B–C의 중간에서 출발한다. A를 왼쪽으로 드래그하면 초록색 원이 Route 기준 거리를 유지하며 옮겨가고 주황색 화살표는 B–C 위의 위치를 유지한다.

1. 'B–C 중간에 다시 배치'를 눌러 두 Tracer를 모은다.
2. '다음 분기 1번 예약'과 '두 Tracer의 Route 해제'를 누른다. RouteBased는 Derailed, Absolute는 Rolling이 된다.
3. '두 Tracer 1 unit 이동'을 세 번 누른다. 초기 그래프에서 Absolute는 C를 지나 꺾인 C–E 구간으로 들어가고 예약을 한 개 소비한다. 재배치한 뒤 예약 없이 반복하면 가장 적게 꺾이는 C–D로 간다.
4. Rolling 중 'TrackingFailure로 이동 제한'을 켜고 이동하면 위치가 유지되고 실패와 남은 거리 1이 표시된다.

오른쪽 패널에는 현재 말단, 위치·회전, 직전 이동 결과와 잔여 거리, FIFO 예약 순서가 표시된다. 그래프에 포커스를 두고 Space를 누르면 1 unit씩 이동한다.

## 구성

| 파일 | 책임 |
| --- | --- |
| [GeoDemoScene.cs](GeoDemoScene.cs) | UI 형식을 사용하지 않는 일반 C# 시나리오. 엔진 객체 생성·수정·재생 상태·폐기를 소유한다. |
| [GraphViewport.cs](GraphViewport.cs) | 화면 좌표 변환, 그리기, 노드 드래그·확대·이동 입력을 맡는다. |
| [DemoWindow.cs](DemoWindow.cs) | 버튼·슬라이더를 시나리오에 연결하고 타이머와 표시 상태를 관리한다. |
| [SceneVerification.cs](SceneVerification.cs) | 창 없이 실제 시나리오의 결과를 검사한다. |
| [TracerDemoScene.cs](TracerDemoScene.cs) · [TracerWindow.cs](TracerWindow.cs) | 두 Tracer의 배치·이동, PolySegment 분기, Wheel 정책과 예약을 비교하는 장면과 창이다. |

UniEngine 본체는 기존 POCO 라이브러리이며 예제가 `ProjectReference`로 참조한다. Avalonia 의존성은 이 실행 프로젝트에만 있다. UI는 입력에 따라 시나리오 메서드를 호출하고, 동기 변경 전파가 끝난 뒤 `Changed`를 받아 결과를 다시 그린다. 창을 닫거나 초기화할 때 시나리오가 자신이 만든 객체와 구독을 정리한다.

창·컨트롤·입력·그리기에는 [Avalonia](https://github.com/AvaloniaUI/Avalonia)의 `Avalonia.Desktop`·`Avalonia.Themes.Fluent` 12.1.6을 사용한다. Avalonia는 MIT 라이선스이다.

## 출력

장면은 메모리에서만 편집하며 결과 파일을 만들지 않는다. Bootstrapper가 초기화하는 Configuration은 문서 폴더 아래 `BlackThunder/UniEngine/Projects/UniEngine.GeoSystem.Sample` 디렉터리를 만든다. 빌드 산출물은 이 프로젝트의 `bin`·`obj`에 생성된다.

[GeoSystem 문서](../../../docs/02.%20UniEngine/03-Services/GeoSystem/README.md) · [검증 기록](docs/03.%20Results.md) · [전체 예시](../../README.md)
