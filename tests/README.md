# UniEngine 테스트

## 구성

`UniEngine.Tests.csproj`는 본체를 ProjectReference로 참조하는 단일 .NET 10·C# 9 테스트 프로젝트이다. 로컬 `BlackThunder.UniTest` 0.4.2 패키지와 NUnit 3을 사용한다. 테스트 코드는 `src`의 계층·모듈 폴더에 대응하도록 배치하며, 모든 테스트를 하나의 `UniEngine.Tests.dll`로 빌드한다.

```text
tests/
├─ UniEngine.Tests.csproj
├─ Support/
├─ 01-Infrastructure/
│  ├─ docs/
│  │  ├─ 02. Plans.md
│  │  └─ 03. Results.md
│  ├─ 01-M.1-Configuration/
│  │  ├─ ConfigurationTests.cs
│  │  ├─ ConfigurationScenarios.cs
│  │  ├─ UniTest/
│  │  │  └─ ConfigurationStateProjectTests.cs
│  │  └─ docs/
│  │     ├─ 01. Tables.md
│  │     ├─ 02. Plans.md
│  │     └─ 03. Results.md
│  ├─ 01-MT.1-Math/
│  │  ├─ UniMathTests.cs
│  │  └─ docs/
│  ├─ 01-MT.2-VectorFunctions/
│  │  ├─ VectorFunctionTests.cs
│  │  └─ docs/
│  ├─ 02-O.1-Objects/
│  │  ├─ InfrastructureObjectTests.cs
│  │  ├─ TransformComparisonTests.cs
│  │  ├─ IntegerVectorComparisonTests.cs
│  │  └─ docs/
│  ├─ 03-M.1-Utilities/
│  │  └─ Json/
│  ├─ 03-M.2-ExceptionHandler/
│  │  ├─ ExceptionHandlerTests.cs
│  │  ├─ UniTest/
│  │  │  └─ ExceptionHandlerStateProjectTests.cs
│  │  └─ docs/
│  │     ├─ 01. Tables.md
│  │     ├─ 02. Plans.md
│  │     └─ 03. Results.md
│  └─ 03-M.3-LogSystem/
│     ├─ LogTests.cs
│     ├─ LogScenarios.cs
│     ├─ UniTest/
│     │  └─ LogStateProjectTests.cs
│     └─ docs/
│        ├─ 01. Tables.md
│        ├─ 02. Plans.md
│        └─ 03. Results.md
└─ 02-Core/
   ├─ 01-O.3-Objects/
   │  ├─ ValueEnumeratorTests.cs
   │  └─ docs/
   │     ├─ 01. Tables.md
   │     └─ 02. Plans.md
   ├─ 02-O.1-Property/
   │  ├─ PropertyTests.cs
   │  ├─ UniTest/
   │  │  └─ PropertyStateProjectTests.cs
   │  └─ docs/
   │     ├─ 01. Tables.md
   │     ├─ 02. Plans.md
   │     └─ 03. Results.md
   ├─ 02-E.1-HandleToken/
   │  ├─ HandleTests.cs
   │  ├─ TokenTests.cs
   │  ├─ HandleTokenIntegrationTests.cs
   │  ├─ UniTest/
   │  │  └─ HandleTokenStateProjectTests.cs
   │  └─ docs/
   │     ├─ 01. Tables.md
   │     ├─ 02. Plans.md
   │     └─ 03. Results.md
   ├─ 02-E.2-AdaptiveCollections/
   │  ├─ AdaptiveCollectionTests.cs
   │  ├─ AdaptiveActionTests.cs
   │  ├─ AdaptiveActionAllocationTests.cs
   │  ├─ AdaptiveDictionaryTests.cs
   │  ├─ AdaptiveHashSetTests.cs
   │  ├─ EnumerationAllocationTests.cs
   │  ├─ UniTest/
   │  │  ├─ CollectionStateProjectTests.cs
   │  │  ├─ DictionaryStateProjectTests.cs
   │  │  └─ HashSetStateProjectTests.cs
   │  └─ docs/
   │     ├─ 01. Tables.md
   │     └─ 02. Plans.md
   ├─ 02-E.3-EventStream/
   │  ├─ EventStreamTests.cs
   │  ├─ EventStreamAllocationTests.cs
   │  ├─ EventStreamPropagationTests.cs
   │  ├─ EventStreamConnectionTests.cs
   │  ├─ EventStreamOperatorTests.cs
   │  ├─ EventStreamCompositionTests.cs
   │  ├─ EventStreamGraphTests.cs
   │  ├─ EventStreamGraphPropagationTests.cs
   │  ├─ EventStreamGraphAllocationTests.cs
   │  ├─ EventStreamLifecycleTests.cs
   │  ├─ EventStreamPendingInputTests.cs
   │  ├─ EventStreamWhereIndexTests.cs
   │  ├─ Benchmarks/
   │  ├─ UniTest/
   │  │  ├─ EventStreamStateProjectTests.cs
   │  │  ├─ EventStreamMergeStateProjectTests.cs
   │  │  └─ EventStreamOperatorStateProjectTests.cs
   │  └─ docs/
   │     ├─ 01. Tables.md
   │     ├─ 02. Plans.md
   │     ├─ 03. Results.md
   │     └─ Historical Code.md
   ├─ 03-E.1-LifecycleHub/
   │  ├─ LifecycleHubTests.cs
   │  ├─ UniTest/
   │  │  └─ LifecycleHubStateProjectTests.cs
   │  └─ docs/
   │     ├─ 01. Tables.md
   │     ├─ 02. Plans.md
   │     └─ 03. Results.md
   ├─ 03-E.2-EventHub/
   │  ├─ EventHubTests.cs
   │  ├─ EventHubFlagsTests.cs
   │  ├─ EventHubRandomContinuousTests.cs
   │  └─ docs/
   │     ├─ 01. Tables.md
   │     ├─ 02. Plans.md
   │     └─ Historical Code.md
   ├─ 04-Bootstrapper/
   │  ├─ BootstrapperTests.cs
   │  ├─ BootstrapperScenarios.cs
   │  └─ docs/
   └─ docs/
      └─ Hub Results.md
```

계층이나 모듈의 테스트를 추가할 때는 대응 폴더에 파일을 두며, 별도 프로젝트를 만들지 않는다. NUnit 직접 테스트는 모듈 폴더에 두고, UniTest 상태 전이 구현은 해당 모듈의 UniTest 폴더에 둔다. 상태·동작 표, 테스트 계획, 결과 기록은 문서가 다루는 테스트 코드가 있는 폴더의 docs에 두며, 테스트 코드가 여러 폴더에 걸치면 가장 가까운 공통 상위 폴더의 docs에 둔다. Infrastructure의 JSON 변환 문서는 Utilities/Json/docs에서 소유한다. 모든 하위 폴더의 테스트는 같은 테스트 어셈블리에 포함하며 빌드 산출물은 공통 `tests/bin`·`tests/obj`에 생성한다.

Plans에는 검증 범위·절차·실행 명령·기대값을, Results에는 해당 계획의 실행 결과·실측값·실패·한계와 원본 기록을 둔다. 구현 변경 설명이나 커밋·파일 정리 같은 작업 이력은 결과 문서에 넣지 않는다. 계획에서 참조하는 현재 소스에 없는 코드는 Plans와 같은 폴더의 `Historical Code.md` 같은 별도 Markdown 부록에 출처와 함께 보존하고 링크로 연결한다. 부록 파일명에는 번호를 붙이지 않는다.

프로젝트는 본체 `src/UniEngine.csproj`와 테스트 `tests/UniEngine.Tests.csproj` 두 개이다. 테스트만 본체를 참조하므로 본체 빌드·배포에 테스트 어셈블리가 포함되지 않는다. 솔루션 `src/UniEngine.sln`에는 본체만 들어 있으므로 테스트 프로젝트는 별도로 실행한다.

두 프로젝트는 [플랫폼 기준 - C#](../guidelines/blackthunder-conventions/instructions/플랫폼%20기준%20-%20CSharp.md)을 따른다.

| 프로젝트 | 대상 프레임워크 | 이유 |
| --- | --- | --- |
| 본체 | `netstandard2.1` 라이브러리 | Unity 등 .NET Standard 환경에서 DLL로 불러올 수 있게 한다. |
| 테스트 | `net10.0` 실행 프로젝트 | 테스트는 실행할 런타임이 필요하므로 현재 .NET LTS를 대상으로 한다. |

언어 버전 C# 9, nullable 해제, ImplicitUsings 해제는 저장소 루트의 `Directory.Build.props`가 두 프로젝트에 함께 적용한다. 프로젝트 파일에는 LangVersion을 따로 적지 않는다. netstandard2.1에는 System.Text.Json이 포함되어 있지 않으므로, 본체는 JSON 변환을 위해 System.Text.Json NuGet 패키지를 참조한다.

본체는 `InternalsVisibleTo("UniEngine.Tests")`로 테스트 어셈블리에 internal 접근 권한을 허용한다. 이 선언은 역방향 프로젝트 참조를 만들지 않으며 private 접근은 허용하지 않는다. 현재 Infrastructure 테스트는 공개 API를 사용한다.

## 테스트 작성 기준

변경 유형별 검증 범위는 [AGENTS](../AGENTS.md)와 UniTest의 [테스트 작성 워크플로](https://github.com/dove-creative/unitest/blob/e53ef8b012f58dc2e851f44498985cb8df772ecd/docs/Wiki.ko/05.%20Test%20Authoring%20Workflow.md) 6절을 따른다. 동작 변화가 없는 코드 변경은 빌드만 수행하고, 국소적인 동작 변경은 영향을 받는 테스트만 실행한다. 여러 모듈에 걸친 코드 변경, 공개 API·빌드 설정·의존성 변경, 릴리즈·패키징 작업은 전체 테스트를 실행한다. 국소적인 수정의 통합 테스트도 영향을 받는 연동 범위로 제한한다.

별도 범위 지정이 없는 UniTest 기반 테스트 작성·보강 작업에서는 대상 모듈의 `01. Tables.md`와 `02. Plans.md`를 기준으로 다음 두 범위를 모두 작성하고 실행하는 것을 기본으로 한다.

- 기본 범위: 계획에 포함된 상태-동작 칸을 검증하는 모듈 테스트와 대표 유스케이스·실패 시나리오의 통합 테스트이다. 각 검증은 랜덤 경로의 선택 결과와 관계없이 직접 수행한다.
- 랜덤 연속 테스트 3개: 같은 표의 동작 후보와 검증 조건을 사용하여 서로 다른 고정 seed 0, 1, 2로 각각 연속 실행한다. 각 실행은 독립된 초기 상태에서 시작하고, 여러 단계에 걸쳐 이전 동작의 결과를 누적하면서 기대 상태와 불변 조건을 검증한다.

랜덤 연속 테스트 3개는 각 seed로 한 번씩, 총 3회 연속 실행하는 것을 뜻하며, 하나의 매개변수화된 테스트로 작성할 수 있다. 서로 다른 seed가 서로 다른 경로를 보장하는 것은 아니다.
연속 실행의 기본 깊이는 각 seed마다 100이다. 적용할 깊이와 실행 제한은 `02. Plans.md`에 기록하고, 기본 깊이를 조정하면 대상의 상태 전이와 실행 비용을 근거로 이유를 함께 적는다.

실행 중에는 대상별 랜덤 연속 테스트 3회의 실제 실행시간 합계가 1분(60초)을 넘으면 가능한 경우 사용자에게 계속 실행할지 중간 확인한다. 빌드와 의존성 복원 시간은 이 합계에서 제외한다.
60초는 중간 확인 시점이며 자동 취소 제한이 아니다. 확인 중에도 실행을 계속하며, 중간 확인이 불가능하거나 사용자 응답이 없으면 계속 진행한다. 사용자가 중단을 요청하면 실행 중인 테스트의 취소를 요청하고 남은 실행을 중단한다.
취소는 실행 중인 동기 콜백을 강제로 종료하지 않으므로 실제 종료까지 시간이 더 걸릴 수 있다. 사전에 장시간 실행이 예상되는 검증은 저장소의 실행 전 확인 지침을 따른다.

실행에 사용한 seed, 실제 도달 깊이, 실행시간과 결과는 `03. Results.md`에 기록한다. 꼭 필요한 실행 기록은 테스트 문서 폴더의 `Runs`에 두고 연결하며, 버전 관리에서 제외되는 TRX·로그의 위치는 적지 않는다. 중단된 실행과 미실행 항목은 완료된 검증과 구분한다.

다시 사용하는 스크립트는 해당 테스트 문서와 같은 `docs` 폴더에 두고, 그럴 수 없으면 `tests/tools`에 둔다. 한 번만 쓰는 스크립트는 저장소의 무시되는 폴더에도 남기지 않고 사용을 마치면 지운다. 문서는 버전 관리에서 제외되는 스크립트를 실행 명령으로 안내하지 않는다.

UniTest 실행 이력의 각 행은 노드 ID, 부모 노드 ID, 깊이, 상태, Lab ID 순서이다. 부모 노드 ID를 따라 후보까지의 실행 경로를 재구성한다.

현재 적용 대상과 모듈별 실행 기록은 [랜덤 연속 테스트 결과](docs/Result.md)에서 확인한다. 랜덤 연속 사례만 다시 실행할 때는 `--filter 'TestCategory=RandomContinuous'`를 사용한다.

## Infrastructure 실행 구성

Core의 [Bootstrapper 테스트](02-Core/04-Bootstrapper/docs/02.%20Plans.md)도 아래 별도 프로세스 실행기를 사용한다. 공통 환경 준비와 애플리케이션 초기화 콜백, 실패 정리와 중복·동시·재진입 호출을 NUnit 직접 사례로 검증한다.

NUnit 직접 사례는 값·입력·실패 계약을 검증한다. UniTest는 Configuration·Log·ExceptionHandler의 연속 상태 전이를 검증하며, 구현은 각 모듈의 UniTest 폴더에 있다. 구성은 [Infrastructure 안내](01-Infrastructure/README.md), 통합 검증 절차는 [공통 계획](01-Infrastructure/docs/02.%20Plans.md)을 따른다.

Configuration과 Log는 직접 사례와 랜덤 검증에 고유 애플리케이션 ID와 새 프로세스를 사용한다. Support의 테스트 전용 진입점은 `--infrastructure-scenario` 인자로 지정한 시나리오만 실행한다. NUnit 테스트 실행은 기존 어댑터로 수행하며 자동 진입점 생성은 끈다.

UniTest는 이전 Lab을 재실행한다. Configuration과 Log의 고정 경로는 각 단계까지의 전체 흐름을 새 프로세스에서 재현한다. 랜덤 연속 테스트의 Configuration은 후보 이력마다 새 프로세스를 사용하고, Log는 seed마다 새 프로세스를 사용해 후보 이력마다 초기 상태를 재현한다. 실행기는 현재 테스트 런타임의 .NET 호스트를 사용하고 제한 시간·종료 코드·표준 출력·오류를 확인한다. 자식 프로세스의 제한 시간은 일반 사례와 Configuration의 경우 30초, Log의 랜덤 실행은 120초다. 테스트 전용 경로는 실제 Configuration 규칙인 문서 폴더 아래에 생성하며, 부모가 검증한 GUID 기반 소유 경로만 정리한다.

## 실행

```powershell
dotnet build src/UniEngine.sln -c Release
dotnet test tests/UniEngine.Tests.csproj -c Release
```

본체는 netstandard2.1이므로 .NET 9 SDK로도 빌드할 수 있다. 테스트 명령에는 .NET 10 SDK가 필요하다. 기본 `dotnet`이 .NET 9이고 사용자 위치에 .NET 10이 설치되어 있다면 다음 명령을 사용한다.

```powershell
& "$env:LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/UniEngine.Tests.csproj -c Release
```

## 범위

- Spatial System의 CollisionSpace 대리자·질의·알림·수명과 Unity 연결 테스트 소스를 제공한다. PhysicalSpace는 미구현이며 현재 검증 대상에서 제외한다. `TestCategory=SpatialSystem`과 Unity의 `SpatialSystemUnity`를 구분하며, 공통 .NET 사례는 통과하였고 Unity 실행은 미검증이다. [계획](01-Infrastructure/04-T-SpatialSystem/docs/02.%20Plans.md)·[결과](01-Infrastructure/04-T-SpatialSystem/docs/03.%20Results.md)를 따른다.
- GeoSystem의 Navigation 양방향 연결·Location과 Planar 직선·Pin·Clip·Route의 계산·알림·수명 전파를 검증한다. `--filter 'TestCategory=GeoSystem'`으로 실행하며 [공통 계획](03-Services/GeoSystem/Common/docs/02.%20Plans.md)·[결과](03-Services/GeoSystem/Common/docs/03.%20Results.md)와 [Planar 계획](03-Services/GeoSystem/Planar/docs/02.%20Plans.md)·[결과](03-Services/GeoSystem/Planar/docs/03.%20Results.md)를 따른다.
- StateMachines.Usm의 직접 생성·소유권·수명·종료·예외, FSM·Direct 전이·Payload·순환 제한, BT 선택·순서·선점·LoopType, Reframe·Parallel과 혼합 계층 검증. `--filter 'TestCategory=Usm'`으로 실행하며 [계획](03-Services/StateMachines/Usm/docs/02.%20Plans.md)과 [결과](03-Services/StateMachines/Usm/docs/03.%20Results.md)는 해당 모듈에서 관리한다.
- StateMachines.Scp 초안의 기존 NUnit 사례 5개를 이관하였다. `--filter 'TestCategory=Scp'`으로 실행하며 [계획](03-Services/StateMachines/Scp/docs/02.%20Plans.md)과 [결과](03-Services/StateMachines/Scp/docs/03.%20Results.md)는 해당 모듈에서 관리한다.
- Math의 절대 오차·거리·방향·회전 비교, 스냅, 직선·이차 베지어 평가와 입력 실패 계약. NUnit 직접 사례이며 `--filter 'TestCategory=Math|TestCategory=VectorFunctions'`으로 실행한다. [검증 계획](01-Infrastructure/01-MT.1-Math/docs/02.%20Plans.md)과 [결과](01-Infrastructure/01-MT.1-Math/docs/03.%20Results.md)는 Math 모듈에서 관리하고 [VectorFunctions 계획](01-Infrastructure/01-MT.2-VectorFunctions/docs/02.%20Plans.md)·[결과](01-Infrastructure/01-MT.2-VectorFunctions/docs/03.%20Results.md)는 별도 모듈에서 관리한다.
- Property의 등록·형식 지정 조회·실패 시 상태 보존·회수·표준 사전 계약과 IPropertySubject 소비 시나리오·연속 상태 전이 검증
- 공통 ValueEnumerator의 순회 수명과 실패 처리- AdaptiveCollection·AdaptiveDictionary·AdaptiveHashSet의 순회 중 변경·중단·복사본 처리
- AdaptiveAction과 EventStream의 순회 소비 회귀 검증
- EventStream·순서 그룹·필터 경로·상태 연산자·Merge의 구독 후 첫 값 전달과 반복 전달 할당량 검증
- EventStream의 멀티캐스트 해제, 순서·필터 공유, 해제한 콜백의 참조 정리 검증
- EventHub의 OnUpdated·OnUpdatedWhere·OnUpdatedWhen·OnUpdatedForFlags 등록, 단일 조건 공유와 값 기준 중복, 리스너별 수명·직접 구독·실패·수면·폐기와 무할당 순회 검증
- LifecycleHub의 연결·해제·폐기 완료 알림과 무할당 폐기 검증
- Handle·Token의 실행 유예·등록·해제·폐기·예외·참조 정리, EventStream·EventHub 소비 사례와 UniTest 고정·랜덤 연속 수명 검증
- AdaptiveDictionary·AdaptiveHashSet 두 반영 모드의 누적 변경·순회·실패 복구와 LifecycleHub의 등록·해제·폐기 조합에 대한 UniTest 고정 경로 및 랜덤 연속 검증
- 스트림 연결 수명, 직접 입력, 경로 연산자 경계값, 순서 그룹의 조합, Branch의 Enabled·구독 수명에 대한 UniTest 상태 전이 검증
- 직접 순회·사전과 집합의 반복 변경·Action의 등록 후 첫 호출과 반복 호출 할당량 검증
- 호출 목록 캐시의 등록 경로와 약한 참조 수명 검증
- UniTest 상태 전이 실행과 재실행
- Infrastructure 값 형식의 정확한 동등성·허용 거리·회전각 비교, 표시 유틸리티·JSON 변환과 등록 계약
- Configuration의 초기화·경로 생성 실패와 Log의 출력·저장·복구
- ExceptionHandler의 보고 정책과 SafeInvoke 전체 오버로드

Core 순회 범위의 상태와 동작 기준과 실행 구성은 ValueEnumerator의 [상태·동작 표](02-Core/01-O.3-Objects/docs/01.%20Tables.md)·[테스트 계획](02-Core/01-O.3-Objects/docs/02.%20Plans.md)과 Adaptive Collections의 [상태·동작 표](02-Core/02-E.2-AdaptiveCollections/docs/01.%20Tables.md)·[테스트 계획](02-Core/02-E.2-AdaptiveCollections/docs/02.%20Plans.md)을 따른다. Infrastructure 보강 당시의 전체 실행 기록과 모듈별 결과는 [Infrastructure 검증 결과](01-Infrastructure/docs/03.%20Results.md)에 있다.

Property 모듈의 [상태·동작 표](02-Core/02-O.1-Property/docs/01.%20Tables.md), [테스트 계획](02-Core/02-O.1-Property/docs/02.%20Plans.md), [실행 결과](02-Core/02-O.1-Property/docs/03.%20Results.md)는 해당 모듈의 docs 폴더에서 관리한다.

EventStream 모듈의 [상태·동작 표](02-Core/02-E.3-EventStream/docs/01.%20Tables.md), [테스트 계획](02-Core/02-E.3-EventStream/docs/02.%20Plans.md), [실행 결과](02-Core/02-E.3-EventStream/docs/03.%20Results.md)는 해당 모듈의 docs 폴더에서 관리한다.

LifecycleHub의 [상태·동작 표](02-Core/03-E.1-LifecycleHub/docs/01.%20Tables.md)·[테스트 계획](02-Core/03-E.1-LifecycleHub/docs/02.%20Plans.md)과 EventHub의 [상태·동작 표](02-Core/03-E.2-EventHub/docs/01.%20Tables.md)·[테스트 계획](02-Core/03-E.2-EventHub/docs/02.%20Plans.md)은 각각 해당 테스트 파일 옆의 docs 폴더에서 관리한다. 두 모듈을 함께 다루는 [실행 결과](02-Core/docs/Hub%20Results.md)는 가장 가까운 공통 상위 폴더인 02-Core의 docs에 둔다.
