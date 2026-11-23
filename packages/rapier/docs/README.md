# Rapier 네이티브 연결

Rapier·Parry의 공개 Rust API를 UniEngine 소유의 C ABI로 제공하는 별도 Rust 프로젝트이다. `packages/rapier`에서 연결 소스·빌드 절차·검증 기록과 채택 배포본을 함께 관리한다. 현재는 Rapier 0.36.1에 로컬 수정본 `0.36.1-r1`을 적용한 0단계 실험이며, Parry는 수정하지 않는다. 본체와 연결한 제품용 PhysicalSpace API는 아직 없다.

후속 [DLL 이주 계획](investigation/DLL%20Migration%20Plan.md)은 외부 의존성 자료를 이 패키지에 남기고 UniEngine 전용 구현·DLL 관리를 엔진 모듈로 옮기는 방향이다. C# 객체 API는 UniEngine.dll에 포함한다. 이주는 아직 적용하지 않았으며 아래 실행 명령은 현재 위치를 기준으로 한다.

## 구조

패키지의 Markdown 문서는 `docs` 아래에 모아 관리한다. 아래 경로는 `packages/rapier`를 기준으로 한다.

| 경로 | 역할 |
| --- | --- |
| `rust-toolchain.toml` | 승인한 Rust 1.99.1 툴체인 고정 |
| `src` | 1단계에서 작성할 제품용 Rust 연결 코드. 현재는 미생성 |
| `investigation` | 2D/3D × f32/f64 네 개의 실험 크레이트와 Cargo.lock |
| `investigation/src` | 원본 공개 API 접근과 C ABI 경계 실험 |
| `investigation/include` | C 소비자가 사용하는 실험용 헤더 |
| `investigation/tests` | MSVC로 빌드하는 별도 C 소비 검증 |
| `scripts` | 환경 설정, 빌드·테스트·내보내기 및 원본 패키지 검증 |
| `docs` | 패키지 안내와 라이선스 고지 |
| `docs/investigation` | 검증 계획·결과, DLL 이주 계획과 외부 검토 요청 |
| `docs/revisions` | 문제·해결방안 요약, 수정본별 개조·검증 계획과 업스트림 이슈 기록 |
| `revisions` | 수정본별 공통 패치·출처 manifest·수정본 Cargo.lock |
| `upstream/0.36.1` | 수정하지 않은 네 Rapier 배포 소스 `.crate` 원본 |
| `licenses` | Rapier·Parry 공식 저장소에서 보존한 Apache-2.0 라이선스 원문 |
| `docs/Third Party Notices.md` | 외부 컴포넌트·버전·라이선스 출처와 배포·수정 고지 기준 |
| 제품 DLL 채택 위치 | 현재 미생성. 이주 후 PhysicalSpace 모듈의 `Native/dist/<연결 버전>/win-x64` 사용 예정 |
| `obj` | 설치 도구·Cargo 캐시·로그·실험 DLL. 버전 관리 대상이 아님 |

Rapier 0.36.1과 Parry 0.31.1을 정확한 버전 의존성으로 사용한다. 공식 C 바인딩에는 의존하지 않는다. `investigation/Cargo.lock`은 원본 비교용으로 유지하고, 수정본 빌드는 원본 압축 파일에서 생성한 별도 소스와 `[patch.crates-io]`를 사용한다. Cargo 패키지 버전은 0.36.1 그대로이며 `0.36.1-r1`은 내부 패치 식별자이다. 원본·패치·수정 파일 해시와 출판자의 Git 커밋·dirty 표시는 [출처 manifest](../revisions/0.36.1-r1/provenance.json)에 있다.

충돌 검사 이후 운동 갱신 문제의 무수정 대안도 초기 수면 전환에 실패하여 임시 패치를 적용했다. `PhysicsWorld::detect_collisions`는 복사한 설정의 시간 간격 0으로 PhysicsPipeline을 실행하고, 그 경로에서 persistent island 초기화까지 완료한다. 상세 설계는 [수정 기록](revisions/Revisions.md)과 [개조·검증 계획](revisions/0.36.1-r1/Plan.md), 원본·대안·수정본 비교는 [결과](investigation/03.%20Results.md)에 있다.

## 실행

Windows x64의 기존 Visual Studio 2022 C++ 도구와 승인된 Rust/Cargo 설치를 사용한다. 스크립트는 새 도구를 자동 설치하지 않고 현재 PowerShell 프로세스에만 도구 환경을 설정한다. 이 PC의 Rust는 `obj/tools` 아래에 최소 프로필로 설치되어 있다.

저장소 루트에서 실행한다.

```powershell
. ./packages/rapier/scripts/EnterBuildEnvironment.ps1
cargo fetch --manifest-path packages/rapier/investigation/Cargo.toml --locked --target x86_64-pc-windows-msvc
./packages/rapier/scripts/VerifyDependencies.ps1
./packages/rapier/scripts/Investigate.ps1
```

의존성 다운로드 후 빌드와 테스트는 `--locked --offline`으로 실행한다. `Investigate.ps1`은 원본·패치 체크섬 검사, 새 소스 생성, 실제 Cargo 경로 검사, 네 Release DLL, Rust 전체 Release·충돌 회귀 Debug 테스트, MSVC C 소비 테스트, 내보내기·의존 DLL·SHA-256 기록을 순서대로 생성한다. 기본 출력은 `obj/collision-update/runs/<실행 시각>`이며 기존 출력 디렉터리를 덮어쓰지 않는다. C 소비 테스트에는 의도적 panic 주입이 있어 진단이 출력되지만 종료 코드와 상태 코드로 성공 여부를 판정한다.

`-OutputDirectory`와 `-TargetDirectory`로 별도 기록·빌드 위치를 지정할 수 있다. `-SkipBuild -OutputDirectory <기존 출력>`은 입력 fingerprint·패치·기존 DLL 해시·이전 테스트 성공을 검사한 뒤 C 소비 검증을 다시 수행한다. 원본 비교는 `investigation/Cargo.toml`을 직접 사용하고, `UE_PROBE_TEST_COLLISION_PATH=world` 또는 `zero-step`으로 검사 호출을 선택한다. `Investigate.ps1`의 기본 경로는 수정본의 `world`이며, 수정본 준비에 실패해도 원본으로 자동 대체하지 않는다.

일반 `dotnet build`와 `dotnet test`에는 이 스크립트를 연결하지 않는다. 1단계에서는 검증된 제품용 연결 DLL만 엔진 모듈의 `Native/dist`에 채택하고 MSBuild가 해당 DLL을 복사하도록 구성한다. 현재의 `ue_probe_*` 심볼과 ABI 버전은 실험용이며 제품 ABI로 승격하지 않는다.

## 1단계 배치 제안

제품용 연결 계층에는 원본 버전과 별개의 버전을 부여하고 `src/01-Infrastructure/03-M.4-PhysicalSpace/Native/dist/<연결 버전>/win-x64`에 네 DLL과 해당 헤더·내보내기 목록·빌드 manifest·라이선스를 함께 보관한다. 이는 DLL 이주 후의 목표 경로이며 아직 생성하지 않았다. C# 공개 객체 API와 내부 DllImport 선언은 UniEngine.dll에 포함하고 첫 진입에서 자체 ABI를 검사한다. 별도 PhysicalSpace 관리 어셈블리와 중복 전달 객체는 추가하지 않는다. C# 관리 구현과 제품 ABI 선언은 1단계의 범위이다.

MSBuild의 `CopyToOutputDirectory`·`CopyToPublishDirectory`로 채택 DLL을 출력에 복사하며, 테스트의 ProjectReference와 별도 소비 프로젝트에서도 복사되는지 검증한다. UniEngine.dll만 수동 복사하는 소비자는 네이티브 DLL도 배포해야 한다. 현재는 이 복사 설정이나 제품 DLL을 추가하지 않았다.

콜백은 정적 함수와 사용자 데이터 포인터로 연결하고 관리 객체는 GCHandle로 유지한다. 함수·대리자·GCHandle 수명과 IL2CPP 표시를 제품 구현에서 검증한다. Rust는 상태 코드·스레드별 진단을 제공하고 C#은 실패를 예외로 변환하며, 양쪽 예외가 C 경계를 넘지 않게 한다. 이번 C 콜백 검증은 실제 관리 콜백이나 IL2CPP 검증을 대신하지 않는다.

## 라이선스와 고지

Rapier·Parry는 Apache-2.0이며 UniEngine 자체 코드의 MIT 라이선스와 구분한다. [라이선스 고지](Third%20Party%20Notices.md)에 원문 출처와 배포 기준을 기록한다. 제품 DLL을 채택할 때 라이선스 사본과 필요한 고지도 함께 배포하며, 실제 DLL에 포함되는 다른 의존성의 라이선스는 별도로 확인한다.

수정한 두 파일에 UniEngine·수정 날짜·`0.36.1-r1`을 명시했다. [Revisions](revisions/Revisions.md)는 문제와 해결방안을 요약하고, 수정본별 계획에는 개조·검증 절차를, 기존 결과 문서에는 실행 결과를 기록한다. 원본 압축 파일과 Cargo 캐시의 소스는 보존한다.

## 검증 문서

[계획](investigation/02.%20Plans.md) · [결과](investigation/03.%20Results.md) · [DLL 이주 계획](investigation/DLL%20Migration%20Plan.md) · [독립 검토 요청](investigation/Independent%20Review.md) · [업스트림 이슈 기록](revisions/Upstream%20Issue.md) · [Foundations 설계](../../../docs/02.%20UniEngine/00-Foundations/Rapier.md)
