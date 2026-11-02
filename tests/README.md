# Golden Hind 테스트

## 구성

`GoldenHind.Tests.csproj`는 본체를 ProjectReference로 참조하는 단일 .NET 10·C# 9 테스트 프로젝트이다. 로컬 `BlackThunder.UniTest` 0.4.0 패키지와 NUnit 3을 사용한다. 테스트 코드는 `src`의 계층·모듈 폴더에 대응하도록 배치하며, 모든 테스트를 하나의 `GoldenHind.Tests.dll`로 빌드한다.

```text
tests/
├─ GoldenHind.Tests.csproj
├─ Support/
├─ 01-Infrastructure/
│  ├─ 01-M-Configuration/
│  │  ├─ ConfigurationTests.cs
│  │  ├─ ConfigurationScenarios.cs
│  │  └─ UniTest/
│  │     ├─ ConfigurationStateProjectTests.cs
│  │     ├─ 01-Tables.md
│  │     ├─ 02-Plans.md
│  │     └─ 03-Results.md
│  ├─ 02-O-Objects/
│  ├─ 03-M.1-Utilities/
│  │  └─ Json/
│  ├─ 03-M.2-ExceptionHandler/
│  │  ├─ ExceptionHandlerTests.cs
│  │  └─ UniTest/
│  │     ├─ ExceptionHandlerStateProjectTests.cs
│  │     ├─ 01-Tables.md
│  │     ├─ 02-Plans.md
│  │     └─ 03-Results.md
│  └─ 03-M.3-LogSystem/
│     ├─ LogTests.cs
│     ├─ LogScenarios.cs
│     └─ UniTest/
│        ├─ LogStateProjectTests.cs
│        ├─ 01-Tables.md
│        ├─ 02-Plans.md
│        └─ 03-Results.md
└─ 02-Core/
   ├─ 01-O.3-Objects/
   │  └─ ValueEnumeratorTests.cs
   ├─ 02-E.2-AdaptiveCollections/
   │  ├─ AdaptiveCollectionTests.cs
   │  ├─ AdaptiveActionTests.cs
   │  ├─ AdaptiveActionAllocationTests.cs
   │  ├─ AdaptiveDictionaryTests.cs
   │  ├─ AdaptiveHashSetTests.cs
   │  ├─ EnumerationAllocationTests.cs
   │  └─ UniTest/
   │     ├─ CollectionStateProjectTests.cs
   │     ├─ 01-Tables.md
   │     ├─ 02-Plans.md
   │     └─ 03-Results.md
   └─ 02-E.3-EventStream/
      └─ EventStreamTests.cs
```

계층이나 모듈의 테스트를 추가할 때는 대응 폴더에 파일을 두며, 별도 프로젝트를 만들지 않는다. NUnit 직접 테스트는 모듈 폴더에 두고, UniTest 상태 전이 구현은 해당 모듈의 UniTest 폴더에 상태·동작 표, 테스트 계획, 결과 기록과 함께 둔다. Infrastructure의 JSON 변환 문서는 Utilities/Json/UniTest에서 소유한다. 모든 하위 폴더의 테스트는 같은 테스트 어셈블리에 포함하며 빌드 산출물은 공통 `tests/bin`·`tests/obj`에 생성한다.

프로젝트는 본체 `src/GoldenHind.csproj`와 테스트 `tests/GoldenHind.Tests.csproj` 두 개이다. 테스트만 본체를 참조하므로 본체 빌드·배포에 테스트 어셈블리가 포함되지 않는다. 솔루션 `src/GoldenHind.sln`에는 본체만 들어 있으므로 테스트 프로젝트는 별도로 실행한다.

두 프로젝트는 [플랫폼 기준 - C#](../guidelines/blackthunder-conventions/instructions/플랫폼%20기준%20-%20CSharp.md)을 따른다.

| 프로젝트 | 대상 프레임워크 | 이유 |
| --- | --- | --- |
| 본체 | `netstandard2.1` 라이브러리 | Unity 등 .NET Standard 환경에서 DLL로 불러올 수 있게 한다. |
| 테스트 | `net10.0` 실행 프로젝트 | 테스트는 실행할 런타임이 필요하므로 현재 .NET LTS를 대상으로 한다. |

언어 버전 C# 9, nullable 해제, ImplicitUsings 해제는 저장소 루트의 `Directory.Build.props`가 두 프로젝트에 함께 적용한다. 프로젝트 파일에는 LangVersion을 따로 적지 않는다. netstandard2.1에는 System.Text.Json이 포함되어 있지 않으므로, 본체는 JSON 변환을 위해 System.Text.Json NuGet 패키지를 참조한다.

본체는 `InternalsVisibleTo("GoldenHind.Tests")`로 테스트 어셈블리에 internal 접근 권한을 허용한다. 이 선언은 역방향 프로젝트 참조를 만들지 않으며 private 접근은 허용하지 않는다. 현재 Infrastructure 테스트는 공개 API를 사용한다.

## Infrastructure 실행 구성

NUnit 직접 사례는 값·입력·실패 계약을 검증한다. UniTest는 Configuration·Log·ExceptionHandler의 연속 상태 전이를 검증하며, 구현은 각 모듈의 UniTest 폴더에 있다. [Infrastructure 결과와 모듈별 문서](01-Infrastructure/README.md)를 따른다.

Configuration과 Log는 사례마다 고유 애플리케이션 ID와 새 프로세스를 사용한다. Support의 테스트 전용 진입점은 `--infrastructure-scenario` 인자로 지정한 시나리오만 실행한다. NUnit 테스트 실행은 기존 어댑터로 수행하며 자동 진입점 생성은 끈다.

UniTest는 이전 Lab을 재실행하므로 Configuration·Log의 각 Lab은 해당 단계까지의 전체 흐름을 새 프로세스에서 재현한다. 실행기는 현재 테스트 런타임의 .NET 호스트를 사용하고 30초 제한·종료 코드·표준 출력·오류를 확인한다. 테스트 전용 경로는 실제 Configuration 규칙인 문서 폴더 아래에 생성하며, 부모가 검증한 GUID 기반 소유 경로만 정리한다.

## 실행

```powershell
dotnet build src/GoldenHind.sln -c Release
dotnet test tests/GoldenHind.Tests.csproj -c Release
```

본체는 netstandard2.1이므로 .NET 9 SDK로도 빌드할 수 있다. 테스트 명령에는 .NET 10 SDK가 필요하다. 기본 `dotnet`이 .NET 9이고 사용자 위치에 .NET 10이 설치되어 있다면 다음 명령을 사용한다.

```powershell
& "$env:LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/GoldenHind.Tests.csproj -c Release
```

## 범위

- 공통 ValueEnumerator의 순회 수명과 실패 처리
- AdaptiveCollection·AdaptiveDictionary·AdaptiveHashSet의 순회 중 변경·중단·복사본 처리
- AdaptiveAction과 EventStream의 순회 소비 회귀 검증
- 직접 순회·사전과 집합의 반복 변경·Action의 등록 후 첫 호출과 반복 호출 할당량 검증
- 호출 목록 캐시의 등록 경로와 약한 참조 수명 검증
- UniTest 상태 전이 실행과 재실행
- Infrastructure 값 형식·표시 유틸리티·JSON 변환과 등록 계약
- Configuration의 초기화·경로 생성 실패와 Log의 출력·저장·복구
- ExceptionHandler의 보고 정책과 SafeInvoke 전체 오버로드

Core 순회 범위의 상태와 동작 기준은 [테스트 표](02-Core/02-E.2-AdaptiveCollections/UniTest/01-Tables.md), 실행 구성은 [테스트 계획](02-Core/02-E.2-AdaptiveCollections/UniTest/02-Plans.md), 검증 결과는 [결과 기록](02-Core/02-E.2-AdaptiveCollections/UniTest/03-Results.md)을 따른다. 전체 실행 기록과 Infrastructure 모듈별 결과는 [Infrastructure 테스트](01-Infrastructure/README.md)를 따른다.
