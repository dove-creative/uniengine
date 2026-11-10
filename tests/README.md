# UniEngine 테스트

## 구성

`UniEngine.Tests.csproj`는 본체를 ProjectReference로 참조하는 단일 .NET 10·C# 9 테스트 프로젝트이다. 로컬 `BlackThunder.UniTest` 0.4.1 패키지와 NUnit 3을 사용한다. 테스트 코드는 `src`의 계층·모듈 폴더에 대응하도록 배치하며, 모든 테스트를 하나의 `UniEngine.Tests.dll`로 빌드한다.

```text
tests/
├─ UniEngine.Tests.csproj
├─ Support/
├─ 01-Infrastructure/
│  ├─ 01-M.1-Configuration/
│  │  ├─ ConfigurationTests.cs
│  │  ├─ ConfigurationScenarios.cs
│  │  ├─ UniTest/
│  │  │  └─ ConfigurationStateProjectTests.cs
│  │  └─ docs/
│  │     ├─ 01. Tables.md
│  │     ├─ 02. Plans.md
│  │     └─ 03. Results.md
│  ├─ 02-O.1-Objects/
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
   ├─ 02-E.2-AdaptiveCollections/
   │  ├─ AdaptiveCollectionTests.cs
   │  ├─ AdaptiveActionTests.cs
   │  ├─ AdaptiveActionAllocationTests.cs
   │  ├─ AdaptiveDictionaryTests.cs
   │  ├─ AdaptiveHashSetTests.cs
   │  ├─ EnumerationAllocationTests.cs
   │  ├─ UniTest/
   │  │  └─ CollectionStateProjectTests.cs
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
   │     └─ 03. Results.md
   ├─ 03-E.1-LifecycleHub/
   │  ├─ LifecycleHubTests.cs
   │  └─ docs/
   │     ├─ 01. Tables.md
   │     └─ 02. Plans.md
   ├─ 03-E.2-EventHub/
   │  ├─ EventHubTests.cs
   │  └─ docs/
   │     ├─ 01. Tables.md
   │     └─ 02. Plans.md
   └─ docs/
      └─ Hub Results.md
```

계층이나 모듈의 테스트를 추가할 때는 대응 폴더에 파일을 두며, 별도 프로젝트를 만들지 않는다. NUnit 직접 테스트는 모듈 폴더에 두고, UniTest 상태 전이 구현은 해당 모듈의 UniTest 폴더에 둔다. 상태·동작 표, 테스트 계획, 결과 기록은 문서가 다루는 테스트 코드가 있는 폴더의 docs에 두며, 테스트 코드가 여러 폴더에 걸치면 가장 가까운 공통 상위 폴더의 docs에 둔다. Infrastructure의 JSON 변환 문서는 Utilities/Json/docs에서 소유한다. 모든 하위 폴더의 테스트는 같은 테스트 어셈블리에 포함하며 빌드 산출물은 공통 `tests/bin`·`tests/obj`에 생성한다.

프로젝트는 본체 `src/UniEngine.csproj`와 테스트 `tests/UniEngine.Tests.csproj` 두 개이다. 테스트만 본체를 참조하므로 본체 빌드·배포에 테스트 어셈블리가 포함되지 않는다. 솔루션 `src/UniEngine.sln`에는 본체만 들어 있으므로 테스트 프로젝트는 별도로 실행한다.

두 프로젝트는 [플랫폼 기준 - C#](../guidelines/blackthunder-conventions/instructions/플랫폼%20기준%20-%20CSharp.md)을 따른다.

| 프로젝트 | 대상 프레임워크 | 이유 |
| --- | --- | --- |
| 본체 | `netstandard2.1` 라이브러리 | Unity 등 .NET Standard 환경에서 DLL로 불러올 수 있게 한다. |
| 테스트 | `net10.0` 실행 프로젝트 | 테스트는 실행할 런타임이 필요하므로 현재 .NET LTS를 대상으로 한다. |

언어 버전 C# 9, nullable 해제, ImplicitUsings 해제는 저장소 루트의 `Directory.Build.props`가 두 프로젝트에 함께 적용한다. 프로젝트 파일에는 LangVersion을 따로 적지 않는다. netstandard2.1에는 System.Text.Json이 포함되어 있지 않으므로, 본체는 JSON 변환을 위해 System.Text.Json NuGet 패키지를 참조한다.

본체는 `InternalsVisibleTo("UniEngine.Tests")`로 테스트 어셈블리에 internal 접근 권한을 허용한다. 이 선언은 역방향 프로젝트 참조를 만들지 않으며 private 접근은 허용하지 않는다. 현재 Infrastructure 테스트는 공개 API를 사용한다.

## 테스트 작성 기준

별도 범위 지정이 없는 UniTest 기반 테스트 작성·보강 작업에서는 대상 모듈의 `01. Tables.md`와 `02. Plans.md`를 기준으로 다음 두 범위를 모두 작성하고 실행하는 것을 기본으로 한다.

- 기본 범위: 계획에 포함된 상태-동작 칸을 검증하는 모듈 테스트와 대표 유스케이스·실패 시나리오의 통합 테스트이다. 각 검증은 랜덤 경로의 선택 결과와 관계없이 직접 수행한다.
- 랜덤 연속 테스트 3개: 같은 표의 동작 후보와 검증 조건을 사용하여 서로 다른 고정 seed 0, 1, 2로 각각 연속 실행한다. 각 실행은 독립된 초기 상태에서 시작하고, 여러 단계에 걸쳐 이전 동작의 결과를 누적하면서 기대 상태와 불변 조건을 검증한다.

랜덤 연속 테스트 3개는 각 seed로 한 번씩, 총 3회 연속 실행하는 것을 뜻하며, 하나의 매개변수화된 테스트로 작성할 수 있다. 서로 다른 seed가 서로 다른 경로를 보장하는 것은 아니다.
연속 실행의 기본 깊이는 각 seed마다 100이다. 적용할 깊이와 실행 제한은 `02. Plans.md`에 기록하고, 기본 깊이를 조정하면 대상의 상태 전이와 실행 비용을 근거로 이유를 함께 적는다.

실행 중에는 대상별 랜덤 연속 테스트 3회의 실제 실행시간 합계가 1분(60초)을 넘으면 가능한 경우 사용자에게 계속 실행할지 중간 확인한다. 빌드와 의존성 복원 시간은 이 합계에서 제외한다.
60초는 중간 확인 시점이며 자동 취소 제한이 아니다. 확인 중에도 실행을 계속하며, 중간 확인이 불가능하거나 사용자 응답이 없으면 계속 진행한다. 사용자가 중단을 요청하면 실행 중인 테스트의 취소를 요청하고 남은 실행을 중단한다.
취소는 실행 중인 동기 콜백을 강제로 종료하지 않으므로 실제 종료까지 시간이 더 걸릴 수 있다. 사전에 장시간 실행이 예상되는 검증은 저장소의 실행 전 확인 지침을 따른다.

실행에 사용한 seed, 실제 도달 깊이, 실행시간, 실행 이력과 결과는 `03. Results.md` 또는 그 문서에서 연결한 실행 산출물에 기록한다. 중단된 실행과 미실행 항목은 완료된 검증과 구분한다.

현재 적용 대상과 모듈별 실행 기록은 [랜덤 연속 테스트 결과](docs/Result.md)에서 확인한다. 랜덤 연속 사례만 다시 실행할 때는 `--filter 'TestCategory=RandomContinuous'`를 사용한다.

## Infrastructure 실행 구성

NUnit 직접 사례는 값·입력·실패 계약을 검증한다. UniTest는 Configuration·Log·ExceptionHandler의 연속 상태 전이를 검증하며, 구현은 각 모듈의 UniTest 폴더에 있다. [Infrastructure 결과와 모듈별 문서](01-Infrastructure/README.md)를 따른다.

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

- 공통 ValueEnumerator의 순회 수명과 실패 처리- AdaptiveCollection·AdaptiveDictionary·AdaptiveHashSet의 순회 중 변경·중단·복사본 처리
- AdaptiveAction과 EventStream의 순회 소비 회귀 검증
- EventStream·순서 그룹·필터 경로·상태 연산자·Merge의 구독 후 첫 값 전달과 반복 전달 할당량 검증
- EventStream의 멀티캐스트 해제, 순서·필터 공유, 해제한 콜백의 참조 정리 검증
- EventHub의 리스너별 구독 수명, 공유 Where·Debug 경로, 직접 구독 순서·실패, Sleep·폐기와 무할당 순회 검증
- LifecycleHub의 연결·해제·폐기 완료 알림과 무할당 폐기 검증
- 스트림 연결 수명, 직접 입력, 경로 연산자 경계값, 순서 그룹의 조합, Branch의 Enabled·구독 수명에 대한 UniTest 상태 전이 검증
- 직접 순회·사전과 집합의 반복 변경·Action의 등록 후 첫 호출과 반복 호출 할당량 검증
- 호출 목록 캐시의 등록 경로와 약한 참조 수명 검증
- UniTest 상태 전이 실행과 재실행
- Infrastructure 값 형식·표시 유틸리티·JSON 변환과 등록 계약
- Configuration의 초기화·경로 생성 실패와 Log의 출력·저장·복구
- ExceptionHandler의 보고 정책과 SafeInvoke 전체 오버로드

Core 순회 범위의 상태와 동작 기준과 실행 구성은 ValueEnumerator의 [상태·동작 표](02-Core/01-O.3-Objects/docs/01.%20Tables.md)·[테스트 계획](02-Core/01-O.3-Objects/docs/02.%20Plans.md)과 Adaptive Collections의 [상태·동작 표](02-Core/02-E.2-AdaptiveCollections/docs/01.%20Tables.md)·[테스트 계획](02-Core/02-E.2-AdaptiveCollections/docs/02.%20Plans.md)을 따른다. 전체 실행 기록과 Infrastructure 모듈별 결과는 [Infrastructure 테스트](01-Infrastructure/README.md)를 따른다.

EventStream 모듈의 [상태·동작 표](02-Core/02-E.3-EventStream/docs/01.%20Tables.md), [테스트 계획](02-Core/02-E.3-EventStream/docs/02.%20Plans.md), [실행 결과](02-Core/02-E.3-EventStream/docs/03.%20Results.md)는 해당 모듈의 docs 폴더에서 관리한다.

LifecycleHub의 [상태·동작 표](02-Core/03-E.1-LifecycleHub/docs/01.%20Tables.md)·[테스트 계획](02-Core/03-E.1-LifecycleHub/docs/02.%20Plans.md)과 EventHub의 [상태·동작 표](02-Core/03-E.2-EventHub/docs/01.%20Tables.md)·[테스트 계획](02-Core/03-E.2-EventHub/docs/02.%20Plans.md)은 각각 해당 테스트 파일 옆의 docs 폴더에서 관리한다. 두 모듈을 함께 다루는 [실행 결과](02-Core/docs/Hub%20Results.md)는 가장 가까운 공통 상위 폴더인 02-Core의 docs에 둔다.
