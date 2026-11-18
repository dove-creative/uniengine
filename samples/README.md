# UniEngine 사용 예시

UniEngine의 Infrastructure·Core·Services 모듈 사용 흐름을 콘솔 예시, 데스크톱 시각화와 컴파일되는 스니펫으로 보여 주는 예시 모음이다.

## 목차

- 실행
- 구성
- 실행 예시
- 시각화 예시
- 스니펫
- 실행 환경
- 출력

## 실행

명령은 저장소 최상위에서 실행한다.

대화형 프롬프트를 실행한다. 빈 입력은 무시하고, help는 명령 목록을, exit는 프롬프트 종료를 뜻한다.

```powershell
dotnet run --project samples/UniEngine.Samples.csproj
```

대표 시나리오를 한 번 실행하고 종료한다. 알 수 없는 명령이나 예외가 발생하면 0이 아닌 종료 코드를 반환한다.

```powershell
dotnet run --project samples/UniEngine.Samples.csproj -- event-stream
```

모든 실행 예시를 순서대로 실행한다.

```powershell
dotnet run --project samples/UniEngine.Samples.csproj -- all
```

## 구성

`UniEngine.Samples.csproj`는 콘솔 예시와 스니펫을 담는다. GeoSystem 시각화는 UI 의존성을 가진 독립 실행 프로젝트로 분리한다. 모듈 폴더는 src·tests·docs와 같은 계층·모듈 이름을 사용하고, 각 폴더에 코드와 README를 둔다. BT 예시 프로젝트 지침은 samples 아래에 PascalCase 폴더를 권하지만, 이 저장소는 모듈 문서·소스·테스트와 같은 경로로 예시를 찾을 수 있도록 번호 폴더를 따른다.

예시는 세 종류이다.

| 종류 | 파일 | 용도 |
| --- | --- | --- |
| 실행 예시 | 모듈Sample.cs | 콜백 순서·순회 중 변경·폐기 알림처럼 실행 결과를 보아야 이해하기 쉬운 동작을 명령으로 실행한다. |
| 시각화 예시 | 독립 프로젝트 | GUI에서 객체를 조작하며 공간·경로의 변화를 확인한다. |
| 스니펫 | 모듈Snippet.cs | 실행 명령 없이 사용 코드만 보여 준다. 결과는 각 줄의 주석에 적는다. |

스니펫도 프로젝트를 빌드할 때 함께 컴파일되므로 API가 바뀌면 빌드에서 드러난다. 다만 주석에 적은 결과는 실행으로 확인하지 않으므로 동작이 바뀌면 직접 고친다.

```text
samples/
├─ UniEngine.Samples.csproj
├─ Program.cs                  # command routing and the sample> prompt
├─ SampleEnvironment.cs        # one-time bootstrap, logger, exception handler
├─ 01-Infrastructure/
│  ├─ 01-M.1-Configuration/
│  │  ├─ ConfigurationSnippet.cs
│  │  └─ README.md
│  └─ ...
├─ 02-Core/
│  ├─ 02-E.3-EventStream/
│  │  ├─ EventStreamSample.cs
│  │  └─ README.md
│  └─ ...
└─ 03-Services/
   ├─ StateMachines/Usm/
   │  ├─ UsmSample.cs
   │  └─ README.md
   └─ GeoSystem/
      ├─ UniEngine.GeoSystem.Sample.csproj
      ├─ GeoDemoScene.cs
      └─ README.md
```

Core의 Interfaces 모듈은 별도 폴더를 두지 않고, Core Objects 스니펫에서 IEnumerationSource를 구현하여 함께 보여 준다. Services 계층은 StateMachines.Usm의 콘솔 예시와 GeoSystem의 시각화 예시를 제공한다.

## 실행 예시

명령 이름은 모듈 폴더 이름을 kebab-case로 쓴 것이다.

| 명령 | 모듈 | 내용 |
| --- | --- | --- |
| handle-token | [HandleToken](02-Core/02-E.1-HandleToken/README.md) | Handle·Token의 1회 정리와 실패한 정리 동작의 보고 |
| adaptive-collections | [AdaptiveCollections](02-Core/02-E.2-AdaptiveCollections/README.md) | 순회 중 추가·제거와 콜백 목록 |
| event-stream | [EventStream](02-Core/02-E.3-EventStream/README.md) | 필터·순서 그룹·비트 조건·분기·병합·병렬 구독 |
| property | [Property](02-Core/02-O.1-Property/README.md) | 도메인 객체의 외부 페이로드 저장 |
| lifecycle-hub | [LifecycleHub](02-Core/03-E.1-LifecycleHub/README.md) | 폐기 완료 알림과 연결 해제 |
| event-hub | [EventHub](02-Core/03-E.2-EventHub/README.md) | 갱신·조건 갱신·수면·폐기 알림 |
| usm | [StateMachines.Usm](03-Services/StateMachines/Usm/README.md) | FSM·BT·Direct 전이, 자식 우선 종료, LoopType·Reframe·Parallel |
| all | - | 모든 콘솔 실행 예시를 순서대로 실행한다. |
| help | - | 명령 목록을 출력한다. |
| exit | - | 대화형 프롬프트를 종료한다. |

## 시각화 예시

[GeoSystem Lab](03-Services/GeoSystem/README.md)은 노드를 드래그해 Pin·Clip·Route·Hold의 변화를 확인한다. Tracer 비교 창에서는 RouteBased와 Absolute의 위치, Route 해제 후의 상태와 Wheel 분기 선택을 살펴본다.

```powershell
dotnet run --project samples/03-Services/GeoSystem/UniEngine.GeoSystem.Sample.csproj -c Release
```

이 프로젝트는 콘솔 `all`에 포함되지 않는다. 창 없는 시나리오 검증은 같은 명령에 `-- verify`를 붙여 실행한다.

## 스니펫

| 계층 | 모듈 | 내용 |
| --- | --- | --- |
| Infrastructure | [Configuration](01-Infrastructure/01-M.1-Configuration/README.md) | 애플리케이션 ID로 초기화하고 경로 읽기 |
| Infrastructure | [Math](01-Infrastructure/01-MT.1-Math/README.md) | 근사 비교·보정, 방향·회전 판정 |
| Infrastructure | [VectorFunctions](01-Infrastructure/01-MT.2-VectorFunctions/README.md) | 직선과 베지어 곡선의 평가 |
| Infrastructure | [Objects](01-Infrastructure/02-O.1-Objects/README.md) | 정수 벡터와 변환의 비교 |
| Infrastructure | [Utilities](01-Infrastructure/03-M.1-Utilities/README.md) | 표시 문자열과 JSON 변환 |
| Infrastructure | [ExceptionHandler](01-Infrastructure/03-M.2-ExceptionHandler/README.md) | 예외 보고와 안전한 대리자 호출 |
| Infrastructure | [LogSystem](01-Infrastructure/03-M.3-LogSystem/README.md) | 로그 기록과 버퍼 저장·폐기 |
| Core | [Core Utilities](02-Core/01-O.1-Utilities/README.md) | 플래그 비트와 토큰 비교 |
| Core | [Core Objects](02-Core/01-O.3-Objects/README.md) | IEnumerationSource와 ValueEnumerator |
| Core | [Bootstrapper](02-Core/04-Bootstrapper/README.md) | 호스트 초기화·기본 틱·종료 |

## 실행 환경

아래는 콘솔 예시의 실행 환경이다. 시각화 프로젝트의 실행 환경은 해당 README를 따른다.

- 프로세스가 시작할 때 SampleEnvironment가 Bootstrapper.Initialize를 한 번 호출하고, 종료할 때 EventStream.Main을 폐기한다. Bootstrapper는 프로세스당 한 번만 초기화할 수 있으므로 대화형 프롬프트의 모든 명령은 같은 환경을 공유한다.
- ExceptionHandler에 보고된 예외는 '[Reported] 형식: 메시지', 로그는 '[수준] 메시지' 형태로 콘솔에 출력된다.
- 각 실행 예시는 자신이 만든 구독과 객체를 정리한다.
- 예시 코드는 Unity로 옮길 수 있도록 C# 9 문법과 .NET Standard 2.1 API만 사용한다.

## 출력

콘솔 실행 예시는 결과 파일을 만들지 않는다. 다만 Bootstrapper가 초기화하는 Configuration은 문서 폴더 아래에 BlackThunder/UniEngine/Projects/UniEngine.Samples 디렉터리를 만든다. 시각화 예제의 출력 위치는 해당 README에서 안내한다.
