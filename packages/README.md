# 안정 패키지

UniEngine이 소비하는 검증된 자사 NuGet 배포본을 보관한다. 프레임워크 개발은 각각 독립 저장소에서 진행하며, UniEngine은 외부 개발 소스 경로를 참조하지 않는다.

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
| 버전 | `0.4.1` |
| 파일 | [BlackThunder.UniTest.0.4.1.nupkg](BlackThunder.UniTest.0.4.1.nupkg) |
| 원본 저장소 | [dove-creative/unitest](https://github.com/dove-creative/unitest) |
| 원본 커밋 | [3df16ae4785f73f1b15569e99a198b20a7be7e94](https://github.com/dove-creative/unitest/commit/3df16ae4785f73f1b15569e99a198b20a7be7e94) |
| SHA-256 | `2669B8806791D68E96FA34DCCA3709C46C8EBE1D97C286F5D3CFE60443C92DA0` |
| 원본 검증 | 전체 테스트 641개와 샘플 CLI 검증 통과 |
| 소비 검증 | 빈 캐시에서 복원, Release 빌드, UniEngine 전체 테스트 686개 통과. 이전 패키지 삭제 후 새 빈 캐시에서 복원·빌드 재확인 |

`netstandard2.1`의 `BlackThunder.UniTest.dll`을 제공하며 런타임 패키지 의존성은 없다. `tests/UniEngine.Tests.csproj`가 이 로컬 패키지를 정확한 버전 `[0.4.1]`으로 참조한다. 실행 방법은 [테스트 안내](../tests/README.md)를 따른다.

각 패키지는 현재 채택한 버전만 보관한다. 새 버전으로 참조를 갱신하고 검증을 마치면, 더 이상 참조하지 않는 이전 버전의 `.nupkg`를 삭제한다.

## UniTest 작업 지침

2026-11-07에 원격 main의 최신 커밋 [3df16ae](https://github.com/dove-creative/unitest/commit/3df16ae4785f73f1b15569e99a198b20a7be7e94)과 [테스트 작성 워크플로](https://github.com/dove-creative/unitest/blob/3df16ae4785f73f1b15569e99a198b20a7be7e94/docs/Wiki.ko/05.%20Test%20Authoring%20Workflow.md)를 확인했다. 최신 지침은 Wiki 아래로 통합되어 있으며, 이 커밋의 CompositeLab 예상 예외 판정 수정이 새 소비 패키지에 포함된다.

기존 0.4.0 패키지를 덮어쓰지 않기 위해 소비용 패키지 버전을 0.4.1로 부여했다. 원본 프로젝트의 Version은 0.4.0으로 유지하고, 패키징 시 PackageVersion=0.4.1과 RepositoryCommit을 명시했다. UniEngine은 소스 복제나 외부 ProjectReference 없이 로컬 피드의 컴파일된 패키지만 사용한다.

## 2026-11-07 갱신 검증

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

## 갱신 절차

1. 해당 프레임워크의 독립 저장소에서 변경과 버전·문서를 갱신하고 솔루션 빌드 및 기존 테스트 전체를 검증한다.
2. 통과한 변경을 커밋하고 해당 커밋에서 Release 패키지를 만든다. `dotnet pack`에 `-p:RepositoryCommit=<원본-SHA>`를 명시한다.
3. 새 버전의 `.nupkg`만 이 폴더로 복사한다. 같은 버전의 파일을 덮어쓰지 않는다.
4. 패키지의 ID·버전·대상 프레임워크·원본 커밋과 SHA-256을 확인하고 이 문서에 기록한다.
5. 사용하는 프로젝트의 패키지 참조를 갱신하고 UniEngine 솔루션을 빌드한다.
6. 비어 있는 임시 패키지 캐시에서도 로컬 피드로 복원·빌드되는지 확인한다.
7. 모든 프로젝트가 새 버전을 참조하는지 확인하고, 같은 패키지의 이전 버전 `.nupkg`를 삭제한다. 삭제 후에도 로컬 피드 복원·빌드가 통과하는지 확인한다.
8. 새 패키지·이전 패키지 삭제·참조·출처 문서를 함께 커밋한다.

루트 `NuGet.config`가 자사 패키지를 이 로컬 피드에 연결하므로 프레임워크 개발 저장소가 없어도 복원할 수 있다. 검증된 자사 배포본만 버전 관리하며, 일반 NuGet 복원 캐시와 빌드 산출물은 추가하지 않는다. 패키지는 공개 NuGet 피드에 게시하지 않는다.
