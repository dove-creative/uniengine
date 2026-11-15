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
| 버전 | `0.4.2` |
| 파일 | [BlackThunder.UniTest.0.4.2.nupkg](BlackThunder.UniTest.0.4.2.nupkg) |
| 원본 저장소 | [dove-creative/unitest](https://github.com/dove-creative/unitest) |
| 원본 커밋 | [e53ef8b012f58dc2e851f44498985cb8df772ecd](https://github.com/dove-creative/unitest/commit/e53ef8b012f58dc2e851f44498985cb8df772ecd) |
| SHA-256 | `C9B57885397F4FFADF96010776C600A3E41E9052C8E763C4F22ECA51C0A4AAE4` |
| 원본 검증 | C# 전체 테스트 660개와 샘플 CLI 검증 통과 |
| 소비 검증 | 빈 캐시 복원 및 UniEngine 커밋 e002925의 별도 사본에 새 패키지를 적용한 Release 빌드 통과. 소비 테스트와 이후 추가 검증은 사용자 요청으로 실행하지 않음 |

`netstandard2.1`의 `BlackThunder.UniTest.dll`을 제공하며 런타임 패키지 의존성은 없다. `tests/UniEngine.Tests.csproj`가 이 로컬 패키지를 정확한 버전 `[0.4.2]`으로 참조한다. 실행 방법은 [테스트 안내](../tests/README.md)를 따른다.

각 패키지는 현재 채택한 버전만 보관한다. 새 버전으로 참조를 갱신하고 검증을 마치면, 더 이상 참조하지 않는 이전 버전의 `.nupkg`를 삭제한다.

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
