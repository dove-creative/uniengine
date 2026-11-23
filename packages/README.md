# 패키지와 연결 모듈

UniEngine이 소비하는 패키지와 외부 네이티브 의존성의 연결 프로젝트를 관리한다. 검증된 배포본, 연결 코드, 빌드 절차와 출처 기록을 패키지별로 함께 보관한다.

자사 NuGet 프레임워크 개발은 각각 독립 저장소에서 진행하며 이 폴더에는 검증된 `.nupkg`를 보관한다. Rapier처럼 UniEngine 전용 연결 코드가 필요한 경우에는 해당 패키지 폴더에서 연결 프로젝트를 관리한다. UniEngine 본체는 컴파일된 배포본을 소비하며 외부 개발 소스 경로나 Rust 빌드에 의존하지 않는다.

## 구성과 관리 기준

| 경로 | 역할 |
| --- | --- |
| 루트의 `.nupkg` | 검증된 자사 NuGet 배포본. 기존 로컬 NuGet 피드 경로를 유지함 |
| [rapier](rapier/docs/README.md) | Rapier·Parry를 사용하는 별도 Rust 연결 프로젝트, 원본·로컬 패치와 검증 문서 |
| Rapier 연결 제품 DLL | 현재 미채택. 이주 후 PhysicalSpace 모듈의 `Native/dist/<연결 버전>/win-x64`에서 관리할 계획 |
| `rapier/obj` | 도구·Cargo 캐시·로그·실험 빌드 산출물. Git 추적과 제품 배포에서 제외함 |

연결 소스와 빌드·검증 스크립트는 버전 관리한다. 네이티브 제품 DLL은 검증 후 `dist`에 명시적으로 채택하며, 일반 `dotnet build`·`dotnet test`는 채택 DLL을 사용하고 Cargo를 실행하지 않는다. Rust 의존성은 정확한 버전과 Cargo.lock으로 고정한다.

[DLL 이주 계획](rapier/docs/investigation/DLL%20Migration%20Plan.md)은 Rapier·Parry의 원본·패치·출처·라이선스·조사 문서를 packages에 남기고, UniEngine 전용 연결 소스·빌드와 네이티브 DLL 관리를 PhysicalSpace 모듈로 옮기는 방향이다. C# 객체 API는 UniEngine.dll에 포함하며 네이티브 DLL은 함께 배포한다. 현재는 계획 상태이고 연결 프로젝트는 아직 이 패키지 폴더에 있다.

외부 컴포넌트의 라이선스는 UniEngine 자체 코드의 MIT 라이선스와 구분한다. [Rapier·Parry 고지](rapier/docs/Third%20Party%20Notices.md)와 라이선스 원문을 보관하며, 배포본에는 해당 바이너리에 필요한 라이선스 사본과 고지를 포함한다.

Rapier 0.36.1의 충돌 검사 이후 운동 갱신 문제에 로컬 수정본 `0.36.1-r1`을 적용하고 0단계 회귀·연결 검증을 통과했다. [수정 기록](rapier/docs/revisions/Revisions.md), [개조·검증 계획](rapier/docs/revisions/0.36.1-r1/Plan.md), [비교 결과](rapier/docs/investigation/03.%20Results.md)에 원본·무수정 대안·수정본을 구분한다. 아래 최초 원본 실험의 버전·해시·실패 결과는 역사적 기록으로 유지하며 수정본 DLL과 혼동하지 않는다.

## 현재 버전

### Blackbox System

| 항목 | 값 |
| --- | --- |
| 패키지 | `BlackThunder.BlackboxSystem` |
| 버전 | `0.2.0` |
| 파일 | [BlackThunder.BlackboxSystem.0.2.0.nupkg](BlackThunder.BlackboxSystem.0.2.0.nupkg) |
| 원본 저장소 | [dove-creative/blackbox-system](https://github.com/dove-creative/blackbox-system) |
| 원본 커밋 | [635c20e2ece4ce0c504ea03dda2c40679eac00e1](https://github.com/dove-creative/blackbox-system/commit/635c20e2ece4ce0c504ea03dda2c40679eac00e1) |
| SHA-256 | `9CF8315FAC441A5B52B167C2B02CC0B1DFB973779C34FD4712F348BD69010F0C` |
| 검증 | C# 기본 테스트 308개 및 C# 10 테스트 47개 통과 |
| 소비 검증 | 빈 패키지 캐시에서 로컬 피드 복원, UniEngine Release 빌드, 기록·TXT 내보내기 실행 통과 |

C# 구현의 `netstandard2.1` DLL만 포함하며, TypeScript 구현과 런타임 패키지 의존성은 없다. UniEngine 본체가 이 패키지를 참조한다.

### UniTest

| 항목 | 값 |
| --- | --- |
| 패키지 | `BlackThunder.UniTest` |
| 버전 | `0.4.2` |
| 파일 | [BlackThunder.UniTest.0.4.2.nupkg](BlackThunder.UniTest.0.4.2.nupkg) |
| 원본 저장소 | [dove-creative/unitest](https://github.com/dove-creative/unitest) |
| 원본 커밋 | [e53ef8b012f58dc2e851f44498985cb8df772ecd](https://github.com/dove-creative/unitest/commit/e53ef8b012f58dc2e851f44498985cb8df772ecd) |
| SHA-256 | `C9B57885397F4FFADF96010776C600A3E41E9052C8E763C4F22ECA51C0A4AAE4` |
| 원본 검증 | C# 전체 테스트 660개와 샘플 CLI 검증 통과 |
| 소비 검증 | 빈 캐시 복원 및 UniEngine 커밋 e002925의 별도 사본에 새 패키지를 적용한 Release 빌드 통과. 소비 테스트와 이후 추가 검증은 사용자 요청으로 실행하지 않음 |

`netstandard2.1`의 `BlackThunder.UniTest.dll`을 제공하며 런타임 패키지 의존성은 없다. `tests/UniEngine.Tests.csproj`가 이 로컬 패키지를 정확한 버전 `[0.4.2]`으로 참조한다. 실행 방법은 [테스트 안내](../tests/README.md)를 따른다.

각 자사 NuGet 패키지는 현재 채택한 버전만 보관한다. 새 버전으로 참조를 갱신하고 검증을 마치면, 더 이상 참조하지 않는 이전 버전의 `.nupkg`를 삭제한다.

## 네이티브 연결 DLL — 0단계 실험

PhysicalSpace는 Rapier·Parry를 의존하는 UniEngine 전용 Rust 연결 DLL 구조이다. 현재는 실험 버전 `0.0.1`이며 본체에 채택한 네이티브 패키지는 없다. 아래 DLL은 최초 원본 검증의 `packages/rapier/obj/stage0/target/x86_64-pc-windows-msvc/release` 산출물이다. 로컬 수정본의 DLL·해시는 별도 [manifest](rapier/obj/collision-update/verified-r1-lf/binaries.json)에 있으며, 어느 쪽도 제품 배포용 `dist`나 C# 호출 대상으로 등록하지 않았다.

| 항목 | 값 |
| --- | --- |
| 원본 패키지 | Rapier `=0.36.1`, Parry `=0.31.1`; 2D/3D × f32/f64 네 조합 |
| 원본 고정 | 별도 Cargo 프로젝트의 정확한 의존성과 Cargo.lock의 배포 파일 체크섬 |
| Rapier 출판 커밋 | `475caa3ff4646fd9a796b67d14116256f4962bc7`, 출판 메타데이터 `dirty: true` |
| Parry 출판 커밋 | `3383f51cbbe9af70565427e7a66c605e1557c1fc`, 출판 메타데이터 `dirty: true` |
| Cargo.lock SHA-256 | `3BBE34905093B9FFDDF862E033B2A88B36C1CB7FF2BE4398CB838B4E82096992` |
| 빌드 | Rust/Cargo 1.99.1, 기존 MSVC, Windows x64, Release, `panic=unwind`, `--locked --offline` |
| feature | 원본 기본 구성. 2D의 기본 block-solver 포함. parallel·FEM·simd8·강화 결정론 등은 별도 활성화하지 않음 |
| 원본 무수정 검증 | 8개 .crate의 SHA-256이 lock과 일치하고, 압축된 원본 파일 2,631개가 빌드 입력과 일치함 |
| DLL·ABI 검증 | 네 DLL 빌드·6개 심볼 내보내기 대조 통과. C 소비 검증은 구성별 51개 assertion 통과 |
| 원본 동작 검증 | Rust 사례 총 24개 통과·4개 실패. 모두 `DetectCollisions` 후 `Step`의 운동 갱신 문제이며 보정 미채택 |
| 실행·출처 근거 | [0단계 결과](rapier/docs/investigation/03.%20Results.md), [패키지 검증 manifest](rapier/obj/stage0/dependencies.json), [DLL manifest](rapier/obj/stage0/binaries.json) |

| 실험 DLL | SHA-256 |
| --- | --- |
| `uniengine_rapier2d_f32_probe.dll` | `F788D290C0A7C833794C7257871C43334DE4D3B4DC52079F7A8C92E270058432` |
| `uniengine_rapier2d_f64_probe.dll` | `93A7FBC6FFEDA9CAA88F3C87B2C0B09EBC304F84B9A539BD2A87DF88D1E1361F` |
| `uniengine_rapier3d_f32_probe.dll` | `6AE9A7E32F0C8869895F1D667E66076F5A273F32EFDCC4A6E5393C6A724F94BC` |
| `uniengine_rapier3d_f64_probe.dll` | `B8C87DC6561B412136D59C5AF415FA6D6E181B12EC273E9438BE5D392099C71B` |

출판 메타데이터의 dirty 표시는 배포자가 기록한 상태이며 UniEngine이 패키지를 수정했다는 뜻이 아니다. Rapier의 비교 기준 태그 `v0.36.1` 커밋 `3406750a38286a0d3c7d0f4be2f9d53153e81d90`과 배포 crate의 출판 커밋을 동일하게 기록하지 않는다. 재현 기준은 실제 .crate의 체크섬이다.

1단계에서 검증된 제품 DLL을 채택할 때 연결 계층 버전을 별도로 부여하고, DLL 이주 계획에 따라 PhysicalSpace 모듈의 `Native/dist/<연결 버전>/win-x64`에 헤더·출처·빌드 옵션·해시·검증 기록·라이선스를 함께 보관한다. MSBuild는 채택한 DLL을 빌드·테스트·게시 출력으로 복사하며 Rust 빌드를 실행하지 않는다. 아래 NuGet 갱신 절차는 기존 자사 NuGet 패키지에 대한 절차이다.

## UniTest 작업 지침

2026-11-14에 `dove/harden-lab-public-api` 브랜치에 푸시한 커밋 [e53ef8b](https://github.com/dove-creative/unitest/commit/e53ef8b012f58dc2e851f44498985cb8df772ecd)과 [테스트 작성 워크플로](https://github.com/dove-creative/unitest/blob/e53ef8b012f58dc2e851f44498985cb8df772ecd/docs/Wiki.ko/05.%20Test%20Authoring%20Workflow.md)를 반영했다. 워크플로 6절은 동작 변화 없는 코드 변경의 빌드 검증, 국소적인 동작 변경의 영향 범위 테스트, 여러 모듈·공개 API·빌드 설정·의존성·릴리즈 변경의 전체 테스트를 구분한다. 통합 테스트도 같은 영향 범위 기준을 따른다.

기존 배포본을 덮어쓰지 않기 위해 소비용 패키지 버전을 0.4.2로 부여했다. 원본 프로젝트의 Version은 0.4.0이며, 패키징 시 PackageVersion=0.4.2와 RepositoryCommit을 명시했다. UniEngine은 소스 복제나 외부 ProjectReference 없이 로컬 피드의 컴파일된 패키지만 사용한다.

이번 패키지에는 Lab의 생성 전용 속성, 읽기 전용 메타데이터·후속 노드 공개와 예외 스택 보존 변경이 포함된다. 연속 경로의 후보 정렬과 seed 해시 규칙도 변경되어, 기존과 같은 seed라도 선택 경로는 달라질 수 있다.

## 2026-11-14 갱신 검증

푸시한 UniTest 커밋을 별도 폴더에 추출하여 C# Release 빌드, 일반 테스트 609개, 자체 테스트 51개와 샘플 CLI 검증을 수행했다. 빌드는 경고·오류가 없었고 모든 테스트와 샘플 검증이 통과했다. 다음 명령으로 소비용 패키지를 생성했다.

```powershell
dotnet pack csharp/src/BlackThunder.UniTest/BlackThunder.UniTest.csproj --configuration Release --no-build --no-restore --output artifacts/packages/uniengine-e53ef8b -p:PackageVersion=0.4.2 -p:RepositoryCommit=e53ef8b012f58dc2e851f44498985cb8df772ecd
```

패키지의 ID·버전·원본 커밋, netstandard2.1 DLL과 런타임 의존성 없음을 확인했다. 패키지 안의 DLL SHA-256은 `A171E4FF363E4618B5B80CB70ED65E6D299BE60F46E7618BC4332F8E68DA2AF1`이다.

| 단계 | 결과 |
| --- | --- |
| 현재 작업 폴더의 빈 캐시 복원 | UniTest 0.4.2와 기존 의존성 복원 성공 |
| 현재 작업 폴더의 Release 빌드 | 본체 빌드 성공. 테스트 빌드는 병행 Utilities 네임스페이스 변경과 기존 테스트 using의 불일치로 CS1061 오류 19개 발생 |
| UniEngine e002925의 별도 사본에 0.4.2만 적용 | 별도 빈 캐시 복원과 본체·테스트 Release 빌드 성공, 경고·오류 0개 |
| UniEngine 소비 테스트 | 사용자 요청으로 실행하지 않음 |
| 이전 0.4.1 패키지 제거 후 재검증 | 사용자 요청으로 실행하지 않음 |

빌드한 별도 사본의 기준은 이력 정리 전 커밋 e002925194cc1713d0a34e7e2f277575bbb0556a이며, 소스가 보존된 [현재 대응 커밋](https://github.com/dove-creative/uniengine/commit/639e608fa37ddd2cd2f25e4af92484f0539d8837)으로 연결한다. 이 결과는 현재 작업 폴더에 있는 다른 변경의 검증 결과가 아니다. 사용자가 추가 검증 중단을 요청하여 소비 테스트와 삭제 후 복원·빌드는 수행하지 않았다.

원본 기록은 [현재 폴더 복원 로그](../tests/obj/UniTestUpdate-e53ef8b/restore-before-cleanup.log), [현재 폴더 빌드 로그](../tests/obj/UniTestUpdate-e53ef8b/build-before-cleanup.log), [별도 사본 복원 로그](../tests/obj/UniTestUpdate-e53ef8b/restore-committed-consumer.log), [별도 사본 빌드 로그](../tests/obj/UniTestUpdate-e53ef8b/build-committed-consumer.log)에 있다. 로그와 검증용 사본은 비추적 산출물이다.

## 2026-11-07 갱신 검증

당시에는 원격 main의 3df16ae 커밋으로 소비용 0.4.1 패키지를 만들었다. 아래 수치는 그 버전의 과거 검증 결과이다.

UniTest의 깨끗한 커밋에서 다음 명령으로 패키지를 생성했다.

```powershell
dotnet pack src/BlackThunder.UniTest/BlackThunder.UniTest.csproj --configuration Release --no-restore --output artifacts/packages/uniengine-3df16ae-verified -p:PackageVersion=0.4.1 -p:RepositoryCommit=3df16ae4785f73f1b15569e99a198b20a7be7e94
```

패키지의 ID·버전·원본 커밋, netstandard2.1 DLL과 런타임 의존성 없음을 확인했다. UniEngine 테스트 출력에 복사된 DLL의 SHA-256도 패키지 안의 DLL과 동일한 `08904DA3F0E281E14D3C5CF807A5B0C43A772B8B305D9E4D187A6ABB4BD82D6F`이다.

| 단계 | 결과 |
| --- | --- |
| 첫 번째 빈 캐시에서 복원 | 로컬 피드의 UniTest 0.4.1과 기존 의존성 복원 성공 |
| 본체 Release 빌드 | 경고 0개·오류 0개 |
| 전체 테스트 | 686개 통과, 실패 0개·건너뜀 0개 |
| 이전 0.4.0 패키지 제거 후 두 번째 빈 캐시에서 복원 | 0.4.1만으로 복원 성공 |
| 제거 후 테스트 프로젝트 Release 빌드 | 본체·테스트 모두 성공, 경고 0개·오류 0개 |

UniEngine 저장소 루트에서 다음 명령을 수행했다. 두 패키지 캐시는 각각 처음 생성한 빈 경로이다.

```powershell
dotnet restore tests/UniEngine.Tests.csproj --packages tests/obj/UniTestUpdate-3df16ae/packages-before-cleanup --no-http-cache
dotnet build src/UniEngine.sln --configuration Release --no-restore -m:1
dotnet test tests/UniEngine.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=UniTest-0.4.1.trx' --results-directory tests/obj/UniTestUpdate-3df16ae
dotnet restore tests/UniEngine.Tests.csproj --packages tests/obj/UniTestUpdate-3df16ae/packages-after-cleanup --no-http-cache
dotnet build tests/UniEngine.Tests.csproj --configuration Release --no-restore -m:1
```

원본은 [테스트 TRX](../tests/obj/UniTestUpdate-3df16ae/UniTest-0.4.1.trx), [테스트 로그](../tests/obj/UniTestUpdate-3df16ae/test.log), [최종 복원 로그](../tests/obj/UniTestUpdate-3df16ae/restore-after-cleanup.log), [최종 빌드 로그](../tests/obj/UniTestUpdate-3df16ae/build-after-cleanup.log)이며 비추적 산출물이다. 이번 갱신에서는 UniEngine 본체와 테스트의 C# 소스를 변경하지 않았다.

## 자사 NuGet 갱신 절차

1. 해당 프레임워크의 독립 저장소에서 변경과 버전·문서를 갱신하고 솔루션 빌드 및 기존 테스트 전체를 검증한다.
2. 통과한 변경을 커밋하고 해당 커밋에서 Release 패키지를 만든다. `dotnet pack`에 `-p:RepositoryCommit=<원본-SHA>`를 명시한다.
3. 새 버전의 `.nupkg`만 이 폴더로 복사한다. 같은 버전의 파일을 덮어쓰지 않는다.
4. 패키지의 ID·버전·대상 프레임워크·원본 커밋과 SHA-256을 확인하고 이 문서에 기록한다.
5. 사용하는 프로젝트의 패키지 참조를 갱신하고 UniEngine 솔루션을 빌드한다.
6. 비어 있는 임시 패키지 캐시에서도 로컬 피드로 복원·빌드되는지 확인한다.
7. 모든 프로젝트가 새 버전을 참조하는지 확인하고, 같은 패키지의 이전 버전 `.nupkg`를 삭제한다. 삭제 후에도 로컬 피드 복원·빌드가 통과하는지 확인한다.
8. 새 패키지·이전 패키지 삭제·참조·출처 문서를 함께 커밋한다.

루트 `NuGet.config`가 자사 NuGet 패키지를 이 로컬 피드에 연결하므로 프레임워크 개발 저장소가 없어도 복원할 수 있다. NuGet 배포본은 검증된 자사 패키지만 버전 관리하며, 일반 NuGet 복원 캐시와 임시 빌드 산출물은 추가하지 않는다. 자사 NuGet 패키지는 공개 피드에 게시하지 않는다.
