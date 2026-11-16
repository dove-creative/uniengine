# GeoSystem 시각화 예제

노드 드래그와 슬라이더로 실제 GeoSystem 객체를 변경하고, Pin·Clip·Route·Hold에 전파되는 결과를 보는 데스크톱 예제이다.

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
| `verify` | 노드 이동 전파, Clip 반전·무효화·복구, 정방향·역방향 원점, Hold 추종, 재생·정지, 초기화·폐기 등 17개 조건을 검사한다. |

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

최근 변경 패널은 엔진 객체가 보낸 변경 알림을 표시한다. 매 프레임 움직이는 Traveller·Hold의 알림은 패널에 쌓지 않는다. PolySegment와 경로 탐색은 이 예제에 포함하지 않는다.

## 구성

| 파일 | 책임 |
| --- | --- |
| [GeoDemoScene.cs](GeoDemoScene.cs) | UI 형식을 사용하지 않는 일반 C# 시나리오. 엔진 객체 생성·수정·재생 상태·폐기를 소유한다. |
| [GraphViewport.cs](GraphViewport.cs) | 화면 좌표 변환, 그리기, 노드 드래그·확대·이동 입력을 맡는다. |
| [DemoWindow.cs](DemoWindow.cs) | 버튼·슬라이더를 시나리오에 연결하고 타이머와 표시 상태를 관리한다. |
| [SceneVerification.cs](SceneVerification.cs) | 창 없이 실제 시나리오의 결과를 검사한다. |

UniEngine 본체는 기존 POCO 라이브러리이며 예제가 `ProjectReference`로 참조한다. Avalonia 의존성은 이 실행 프로젝트에만 있다. UI는 입력에 따라 시나리오 메서드를 호출하고, 동기 변경 전파가 끝난 뒤 `Changed`를 받아 결과를 다시 그린다. 창을 닫거나 초기화할 때 시나리오가 자신이 만든 객체와 구독을 정리한다.

창·컨트롤·입력·그리기에는 [Avalonia](https://github.com/AvaloniaUI/Avalonia)의 `Avalonia.Desktop`·`Avalonia.Themes.Fluent` 12.1.6을 사용한다. Avalonia는 MIT 라이선스이다.

## 출력

장면은 메모리에서만 편집하며 결과 파일을 만들지 않는다. Bootstrapper가 초기화하는 Configuration은 문서 폴더 아래 `BlackThunder/UniEngine/Projects/UniEngine.GeoSystem.Sample` 디렉터리를 만든다. 빌드 산출물은 이 프로젝트의 `bin`·`obj`에 생성된다.

[GeoSystem 문서](../../../docs/02.%20UniEngine/03-Services/GeoSystem/README.md) · [검증 기록](docs/03.%20Results.md) · [전체 예시](../../README.md)
