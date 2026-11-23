# PhysicalSpace DLL 이주 계획

Rapier·Parry의 외부 의존성 자료와 UniEngine 전용 구현을 분리하고, PhysicalSpace의 C# 객체 API를 UniEngine.dll에서 직접 제공하는 계획이다.

2026-11-23 사용자 선택은 'C# 객체 API를 UniEngine.dll에 포함'이다. 같은 날 공개 객체를 한 층으로 두고 네이티브 호출을 내부 Interop 계층에 모으는 구조를 확정했다. 현재 상태는 계획 작성이며 소스 이동, 프로젝트 설정 변경, 제품 DLL 채택과 C# API 구현은 수행하지 않았다.

## 목차

- 목표와 관리 경계
- 현재 상태와 목표 구조
- 이주 대상과 배치
- 빌드·출처·배포 기준
- 실행 순서
- 검증과 완료 기준
- 범위와 후속 구현

## 목표와 관리 경계

`packages/rapier`는 외부 의존성의 원본·패치·출처·라이선스·조사 문서를 관리하고, UniEngine 전용 연결 코드와 배포 DLL은 PhysicalSpace 모듈에서 관리한다. '패키지에 순수 Rapier만 남긴다'는 표현은 이 소유권 분리를 뜻한다. Parry의 버전·라이선스와 Rapier 로컬 패치 0.36.1-r1도 필요하므로 무수정 Rapier 파일만 남기는 것은 아니다.

| 대상 | 이주 후 책임 |
| --- | --- |
| packages/rapier | Rapier 원본 압축 파일, 로컬 패치·출처 기록, Rapier·Parry 라이선스와 의존성 조사 |
| UniEngine의 PhysicalSpace 모듈 | C# 공개 객체 API, P/Invoke 선언, Rust 연결 소스·빌드·검증, 제품 네이티브 DLL 채택 |
| UniEngine.dll | 사용자가 직접 호출하는 PhysicsWorld·RigidBody·Collider 등의 C# 타입 |
| 별도 네이티브 DLL | 2D/3D × f32/f64별 네 DLL. 자체 C ABI와 Rapier·Parry의 네이티브 구현을 담으며 UniEngine.dll과 함께 배포함 |

C# 객체 API는 별도 PhysicalSpace.dll로 분리하지 않는다. 기존 Transform2D·Transform3D, Vector2Int·Vector3Int와 Math 계약은 같은 UniEngine 어셈블리 안에서 사용하며, 이번 이주를 위해 공통 타입 어셈블리를 새로 만들거나 공개 타입을 복제하지 않는다.

## 현재 상태와 목표 구조

현재는 `packages/rapier/investigation`의 네 Rust 실험 크레이트가 DLL을 만들며 C 소비자가 이를 검증한다. C# 제품 API와 `dist`에 채택한 제품 DLL은 아직 없다. 따라서 이미 존재하는 C# 래퍼를 이동하는 작업과 달리, 실제 이주 대상은 현재 Rust 실험·연결 개발 환경이다.

C# 객체 API를 본체 어셈블리에 넣는 선택은 기존 PhysicalSpace 설계와 같다. 이번에 바뀌는 기준은 packages와 엔진 모듈 사이의 소스·빌드·DLL 관리 경계이다.

목표 호출 구조는 다음과 같다.

```text
사용자 코드
    ↓ world.Step(...)
UniEngine.dll
    PhysicalSpace 공개 객체   핸들 소유·수명 확인·이벤트 연결, IDisposable
    내부 Interop             P/Invoke 선언·ABI 검사·상태 코드 변환·콜백 연결
    ↓ C ABI
UniEngine 전용 네이티브 DLL (2D/3D × f32/f64별 4개)
    Rust 연결 코드 + Rapier·Parry
```

공개 객체는 네이티브 핸들을 직접 소유하는 한 층으로 둔다. PhysicalSpace 객체가 사용자의 진입점이며, 같은 객체를 다시 감싸는 엔진용 전달 클래스나 별도 어셈블리의 바인딩 객체를 두지 않는다. 핸들 식별, 수명 확인, 제거에 따른 무효화와 이벤트 연결의 기준을 한 객체에 두기 위해서이다. 객체 층이 둘이면 안쪽 핸들 객체와 바깥 객체의 폐기 상태를 양쪽에서 맞춰야 하고, 사용자에게 보이는 타입과 그 사이의 변환도 늘어난다.

네이티브 호출은 공개 객체와 분리한 내부 Interop 계층에 모은다. 이 계층은 P/Invoke 선언, ABI 검사, 상태 코드의 예외 변환과 콜백 연결을 담당하며 객체별 수명 상태를 소유하지 않는다. ABI가 바뀌면 공개 객체를 다시 감싸지 않고 이 계층에서 대응한다. 내부 계층의 단위 검증은 본체의 기존 InternalsVisibleTo 연결을 사용한다. 호출 단계를 줄이기 위해 수명 확인·예외 변환·콜백 참조 유지 계약을 제거하지 않는다.

별도 어셈블리의 Rapier C# 바인딩을 엔진이 다시 감싸는 구조는 채택하지 않는다. 이 바인딩은 공식 C 바인딩이 아닌 UniEngine 전용 C ABI를 전달하므로, 다른 프로젝트가 재사용할 범용 바인딩이 되지 않는다. UniEngine 밖에서 같은 바인딩을 재사용할 요구가 생기면 그때 분리 여부를 다시 판단한다.

여기서 'DLL을 UniEngine으로 옮긴다'는 것은 구현·빌드·배포의 소유 위치를 엔진 모듈로 옮긴다는 뜻이다. C# 관리 어셈블리와 Rust 네이티브 DLL을 단일 UniEngine.dll 파일로 병합하는 계획은 아니다. Rust `cdylib`는 다른 언어에서 불러올 네이티브 라이브러리를 생성하고, C#은 P/Invoke로 해당 함수를 호출한다. [Rust 링크 형식](https://doc.rust-lang.org/reference/linkage.html) · [Microsoft P/Invoke](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)

파일 위치만 바꾸면 기존 호출 메서드는 그대로 남는다. 공개 객체를 한 층으로 두는 결정의 근거는 위의 구조이며, 이번 이주나 층 통합의 성능 효과를 '호출 스택 한 단계 감소'로 주장하지 않는다. 1단계 ABI 설계에서는 관리·네이티브 경계를 넘는 호출 횟수와 네이티브에서 관리 코드로의 역호출 횟수를 줄이는 방식을 검토하고, 성능을 주장할 때는 같은 작업량의 Release 실행으로 측정한다.

## 이주 대상과 배치

다음은 저장소 루트 기준의 목표 경로이다. 아직 생성하거나 이동한 경로가 아니다.

```text
packages/rapier/
    upstream/                     원본 Rapier 배포 압축 파일
    revisions/                    로컬 패치와 출처·기존 검증 lock
    licenses/                     Rapier·Parry 라이선스 원문
    docs/                         패키지 안내·조사·검증·수정 기록

src/01-Infrastructure/03-M.4-PhysicalSpace/
    ...                           UniEngine.dll에 포함할 C# 객체 API
    Interop/                      내부 P/Invoke·ABI·오류·콜백 연결
    Native/
        rust-toolchain.toml       Rust 도구 버전
        investigation/            현재 실험 크레이트·공용 Rust·C 검증
        scripts/                  준비·빌드·출처·DLL 검증
        src/                      후속 제품용 Rust 연결 코드
        dist/<연결 버전>/win-x64/  채택한 제품 DLL·헤더·manifest·라이선스
        obj/                      임시 도구·캐시·생성 소스·빌드 결과
```

| 현재 항목 | 처리 |
| --- | --- |
| packages/rapier/investigation | Native/investigation으로 이동. Cargo.toml·Cargo.lock·공용 소스·C 헤더·소비 테스트를 함께 유지함 |
| packages/rapier/scripts | Native/scripts로 이동하고 외부 패키지 루트와 연결 프로젝트 루트를 구분하도록 경로 계산을 수정함 |
| packages/rapier/rust-toolchain.toml | Native/rust-toolchain.toml로 이동함 |
| packages/rapier/upstream·revisions·licenses | 외부 의존성 자료로 유지함 |
| packages/rapier/revisions/0.36.1-r1/Cargo.lock | 최초 수정본의 역사적 기록으로 보존하고 이주 후 빌드 입력으로 사용하지 않음. 수정본 빌드 lock은 실험 workspace와 함께 Native/investigation에서 관리함 |
| packages/rapier/docs | 현재 위치를 유지함. 이 계획과 0단계 비교 결과·원본 기록의 링크를 보존함 |
| packages/rapier/obj의 과거 로그·DLL | 최초 검증 근거로 보존함. 버전 관리 대상이 아니어서 이 작업 트리에만 있으므로 옮기거나 정리하지 않음. 새 실행 로그·DLL·생성 소스는 Native/obj에 기록함 |
| packages/rapier/obj/tools | 도구 재사용·이동 시 체크섬과 새 경로 동작을 확인함. 전역 PATH 변경이나 새 도구 자동 설치는 하지 않음 |
| 기존 packages/rapier/dist 제안 | 제품 미채택 상태에서 폐기하고 Native/dist를 제품 채택 위치로 사용함 |

외부 패키지의 준비 책임과 엔진의 빌드 책임은 경로로 구분한다. 준비 스크립트는 packages의 원본·패치를 읽어 Native/obj에 수정 소스를 생성한다. Cargo 캐시의 원본이나 추적 중인 원본 압축 파일을 직접 수정하지 않는다.

테스트 계획·실행 기록은 사용자의 '관련 Markdown을 packages/rapier/docs에 모은다'는 지시에 따라 현재 위치를 유지한다. 이는 테스트 코드 아래 docs를 기본으로 하는 공통 문서 컨벤션의 프로젝트별 예외이다. 이주 후 기록도 기존 Results의 별도 절에 두고, Native/investigation의 테스트 코드와 Native/obj의 실행 근거를 연결한다. 같은 문서의 활성 복사본을 Native 아래에 만들지 않는다.

## 빌드·출처·배포 기준

### 경로와 재현성

현재 스크립트는 공통 `nativeRoot` 아래에 원본·실험·도구가 함께 있다고 가정한다. 이동 시 파일만 옮기지 않고 다음 기준을 적용한다.

- 패키지 루트, 연결 프로젝트 루트, 실행 출력 루트와 도구 위치를 각각 계산하고 검증한다. 실행한 셸의 현재 디렉터리만으로 경로를 추측하지 않는다.
- PrepareRevision은 packages의 원본·패치·provenance를 읽고, 이동한 실험 workspace와 Native/investigation의 수정본 lock으로 새 작업공간을 생성한다. 허용 출력 경로 검사와 Git 패치 적용 경로도 Native/obj 기준으로 갱신한다. 현재처럼 상위 폴더 단계 수로 저장소 루트를 계산하면 이동 후 다른 폴더를 가리키므로, 저장소 루트는 Git에서 확인한다.
- VerifyDependencies는 이동한 원본 비교 lock과 실제 Cargo 캐시를 검사한다. VerifyResolvedDependencies는 새로 생성한 Rapier 경로와 원본 Parry 경로를 대조한다.
- Investigate는 패키지 입력과 엔진 입력을 모두 fingerprint에 포함한다. 루트가 달라져도 경로 표기가 충돌하지 않도록 소유 루트를 명시한다. 이전 위치의 SkipBuild 기록을 새 실행의 성공 근거로 재사용하지 않는다.
- Rapier 0.36.1·Parry 0.31.1, 0.36.1-r1 패치, 기본 feature와 panic 경계는 유지한다. 원본 아카이브·패치·수정 파일 해시를 재검사한다. Cargo.lock에는 feature가 기록되지 않으므로 의존성 버전은 Cargo.lock으로, 활성 feature는 cargo metadata의 해석 결과로 이주 전과 비교한다.
- 과거 manifest와 로그를 새 경로로 덮어쓰지 않는다. 경로가 달라진 새 빌드의 입력·출력 해시는 별도 기록하며 DLL 바이트의 동일성까지 가정하지 않는다.
- 해시 검사 대상은 바이트 단위로 보존한다. 이 PC의 시스템 Git 설정은 core.autocrlf=true이고 .gitattributes에는 .patch·.lock·.json·.rs·.toml·.ps1·.c·.h 규칙이 없다. 현재 파일은 LF라서 provenance 해시와 일치하지만, 규칙 없이 커밋하면 새 checkout에서 CRLF로 바뀌어 PrepareRevision의 해시 검사가 실패한다. 첫 커밋 전에 이 형식들을 LF로, .crate·.dll을 binary로 지정한다.
- 이동 후의 경로 길이를 확인한다. 2026-11-23 측정에서 packages/rapier/obj의 최장 경로는 Debug incremental 산출물의 258자였다. 모듈 경로가 30자 길어서 같은 구조라면 최장 288자, 260자 이상 항목 2,373개가 된다. Windows 긴 경로 설정은 켜져 있으나 rustc·MSVC 링커의 처리는 확인하지 않았다. 기본 target 위치에서 Release·Debug 빌드로 확인하고, 실패하면 짧은 target 경로를 기본값으로 정한다.

### lock과 출처 기록

Cargo.lock은 workspace의 의존성 해석 결과이므로 workspace를 소유하는 엔진 모듈에서 관리한다. 원본 비교 lock과 수정본 빌드 lock은 모두 실험 크레이트 네 개가 루트 패키지인 실험 workspace의 lock이며, 이주 후에는 Native/investigation에 둔다. 두 lock은 네 Rapier 항목의 source·checksum 줄만 다르다. 1단계 제품 크레이트의 lock은 이와 별도로 관리한다.

packages의 provenance는 원본 압축 파일·패치·수정 파일의 해시만 검사 기준으로 삼는다. 현재 provenance의 lockSha256·originalLockSha256은 최초 수정본 당시의 값으로 보존하고, 이주 후에는 엔진 lock의 검사 기준으로 사용하지 않는다. 엔진이 실험 workspace의 의존성을 바꿀 때마다 패키지 기록을 고치지 않기 위한 기준이다. 대신 이주 후 스크립트는 다음을 검사한다.

- 원본 비교 lock의 네 Rapier checksum이 provenance의 압축 파일 SHA-256과 같다. 현재 두 값은 대소문자 표기만 다르다.
- 수정본 빌드 lock은 원본 비교 lock과 비교해 네 Rapier의 source·checksum만 없고, Parry와 나머지 전이 의존성의 버전·checksum은 같다.
- 실제 사용한 두 lock의 SHA-256을 실행 manifest에 기록한다.

### 관리 어셈블리와 네이티브 DLL

C# 구현은 기존 `src/UniEngine.csproj`에 포함하며 netstandard2.1·C# 9·현재 nullable 정책을 유지한다. Native 폴더 전체를 DefaultItemExcludes로 본체의 기본 항목 수집에서 제외한다. SDK의 기본 수집은 하위 폴더의 obj를 제외하지 않는다. 임시 프로젝트로 확인한 결과 하위 Native/obj의 일반 파일은 None, C# 파일은 Compile 항목에 들어갔다. 현재 packages/rapier/obj는 약 6.6GB, 1만 6천여 개 파일이므로 그대로 옮기면 본체를 평가할 때마다 이 파일들을 열거한다. 채택 DLL과 라이선스는 Native/dist에서 명시적으로 포함하며, 테스트 프로젝트의 Benchmarks 제외와 같은 방식을 따른다. C# 테스트는 기존 테스트 프로젝트의 PhysicalSpace 영역에서 관리한다.

Rust 빌드는 별도 명령으로 실행한다. 일반 `dotnet build`·`dotnet test`·게시 과정은 채택한 Native/dist만 소비하며 Cargo 실행, 네트워크 다운로드나 로컬 Rust 캐시에 의존하지 않는다. 최초 실험의 `ue_probe_*` DLL은 제품 ABI로 이름만 바꿔 채택하지 않는다.

제품 채택 시 차원·정밀도별 네 DLL, 헤더·내보내기 목록, 연결 ABI/버전, 원본 버전·패치·feature·도구·lock·SHA-256과 라이선스를 함께 보관한다. MSBuild 복사 항목은 검증한 연결 버전을 명시하고, 임의의 '가장 최신' 폴더나 이전 버전 DLL로 대체하지 않는다.

UniEngine 빌드·테스트·소비 프로젝트·게시 출력에서 네이티브 DLL과 필요한 라이선스가 함께 전달되는지 검증한다. 직접 파일 복사 소비자에게도 UniEngine.dll과 네이티브 DLL을 함께 배포하도록 안내한다. 관리 API의 어셈블리는 UniEngine.dll 하나이며 별도 PhysicalSpace 관리 DLL은 추가하지 않는다.

## 실행 순서

1. **기준 보존:** 현재 원본·패치·lock·실험 DLL·라이선스·실행 로그와 작업 중 변경을 확인한다. 기존 0단계 검증 결과를 이주 전 기준으로 고정한다. packages/rapier는 아직 커밋한 적이 없으므로, 줄 끝 규칙을 적용한 뒤 이동 전 상태를 커밋하여 복구 기준으로 삼는다. 추적하지 않는 obj의 로그·DLL은 제자리에 둔다.
2. **개발 위치 이주:** Native 아래로 실험 소스·스크립트·도구 선언을 옮긴다. packages에는 외부 의존성 자료와 문서를 남기고 동일 구현의 활성 복사본을 두지 않는다.
3. **경로 분리:** 패키지 입력과 엔진 입력을 구분하도록 스크립트를 수정한다. 허용 경로·SHA·lock·실제 Cargo 해석 경로 검사를 유지하고 새 작업 디렉터리에서 준비를 수행한다.
4. **0단계 재검증:** 이동한 프로젝트로 네 구성 Release·Debug·C 소비 검증과 전체 .NET 검증을 수행한다. 결과가 기준을 충족하기 전에는 새 위치를 검증 완료로 표시하지 않는다.
5. **문서 반영:** packages/README.md와 패키지 안내, Foundations/Rapier, PhysicalSpace 설계·조사 문서, 테스트 폴더의 네이티브 조사 안내, 라이선스 고지의 소유 범위를 실제 이주 상태에 맞춘다. packages/rapier 경로를 참조하는 Markdown을 모두 검색하여 링크와 경로 서술을 갱신한다. 원본 결과와 과거 로그의 역사적 경로는 보존한다.
6. **후속 제품 구현:** 0단계 이주 결과를 확인한 뒤 별도 1단계 작업에서 C# 객체 API와 제품 Rust ABI를 구현한다. 이 단계에서 Native/dist 채택과 MSBuild 배포 설정을 연결한다.

경로 이주가 실패하면 이동 전 커밋으로 복구한다. 미검증 DLL을 채택 버전으로 교체하거나 과거 성공 기록으로 새 실패를 덮지 않는다.

## 검증과 완료 기준

상세 0단계 사례와 기존 수치는 [검증 계획](02.%20Plans.md)·[결과](03.%20Results.md)를 기준으로 한다. 아래는 이주 후 수행할 검증이며 이번 문서 작성에서 실행한 결과가 아니다.

| 검증 | 통과 기준 |
| --- | --- |
| 파일·문서 경계 | 외부 의존성 자료가 packages에 남고 활성 연결 소스·빌드·새 DLL은 Native에 있음. Markdown은 packages/rapier/docs에서 유지하며 관련 링크가 유효함 |
| 원본·패치 | 기존 아카이브·패치 해시 일치. 의도한 파일만 수정되고 Parry 원본 유지. 잘못된 체크섬·적용 불가 패치를 빌드 전에 거부함 |
| Cargo 해석 | 네 Rapier가 새로 생성한 수정 소스에 연결되고 Parry·전이 의존성 버전과 활성 feature 유지. 원본 비교 lock의 Rapier checksum이 압축 파일 해시와 같음. 이전 위치로의 무음 대체를 거부함 |
| 네이티브 회귀 | 기존 Rust Release 76개, Debug 회귀 52개, C assertion 204개와 DLL별 6개 실험 내보내기 통과. 테스트 추가 시 기존 사례를 포함한 실제 건수를 기록함 |
| 새 작업공간 | 새 소스·target·출력에서 준비·Release 빌드·C 소비 성공. 기본 target 위치에서 Debug 빌드 성공. 다른 현재 디렉터리에서 호출해도 동일 입력을 선택함 |
| 새 checkout | 커밋한 상태를 별도 위치에 새로 checkout하고, 시스템 Git 설정 그대로 준비 단계의 해시 검사와 패치 적용을 통과함 |
| 일반 .NET | Rust를 실행하지 않고 Release 빌드·전체 테스트 통과. 본체의 평가 항목에 Native 아래 파일이 없고 dist에서 명시한 파일만 포함됨. 현재 기준은 1,660개이며 실제 실행 결과를 별도 기록함 |
| 제품 API·배포 — 후속 1단계 | 사용자가 UniEngine.dll의 객체 API를 직접 사용함. 중복 전달 객체·별도 PhysicalSpace 관리 어셈블리가 없음. 채택 DLL이 ProjectReference 소비와 게시 출력에 전달되고 ABI 불일치·누락·콜백·Dispose 계약을 검증함 |

이주는 경로 계산·빌드 설정을 바꾸므로 구현 시 전체 검증을 수행한다. 장시간 실행은 저장소 AGENTS의 사전 확인 규칙을 따르며, 현재 문서 작성은 실행 승인을 새로 요청하거나 테스트를 시작하는 단계가 아니다. 결과는 기존 Results에 별도 이주 절로 기록하고 제품 구현의 시험 결과와 구분한다.

0단계 이주 완료는 엔진 소유 위치에서 동일 기능과 재현성이 확인된 상태이다. 제품용 C# API·ABI와 DLL 배포의 완료는 후속 1단계 기준을 만족한 뒤 별도로 판정한다.

## 범위와 후속 구현

이번 계획은 C# API의 어셈블리 위치와 객체 층 구조, 외부 의존성과 연결 코드의 관리 경계, 이주·검증 순서를 정한다. 제품 API의 세부 구현, 여러 객체 상태의 묶음 전달과 사후 이벤트를 네이티브에서 넘겨받는 방식, 네이티브 DLL 이름·연결 버전, f64 공개 수학 타입, 선택 feature·다른 플랫폼·IL2CPP 실기 검증은 후속 구현에서 확정한다. 이주를 이유로 기존 PhysicalSpace 객체·수명·오류 계약을 축소하지 않는다.

[PhysicalSpace 설계](../../../../docs/02.%20UniEngine/01-Infrastructure/03-M.4-PhysicalSpace/README.md) · [네이티브 대응](../../../../docs/02.%20UniEngine/01-Infrastructure/03-M.4-PhysicalSpace/Native%20Coverage.md) · [패키지 안내](../README.md) · [수정 기록](../revisions/Revisions.md)
