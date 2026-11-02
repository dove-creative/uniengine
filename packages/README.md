# 안정 패키지

Golden Hind가 소비하는 검증된 자사 NuGet 배포본을 보관한다. 프레임워크 개발은 각각 독립 저장소에서 진행하며, Golden Hind는 외부 개발 소스 경로를 참조하지 않는다.

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
| 소비 검증 | 빈 패키지 캐시에서 로컬 피드 복원, Golden Hind Release 빌드, 기록·TXT 내보내기 실행 통과 |

C# 구현의 `netstandard2.1` DLL만 포함하며, TypeScript 구현과 런타임 패키지 의존성은 없다. Golden Hind 본체가 이 패키지를 참조한다.

### UniTest

| 항목 | 값 |
| --- | --- |
| 패키지 | `BlackThunder.UniTest` |
| 버전 | `0.4.0` |
| 파일 | [BlackThunder.UniTest.0.4.0.nupkg](BlackThunder.UniTest.0.4.0.nupkg) |
| 원본 저장소 | [dove-creative/unitest](https://github.com/dove-creative/unitest) |
| 원본 커밋 | [5b5e593f1a1018d0b359e55073426cb83be80590](https://github.com/dove-creative/unitest/commit/5b5e593f1a1018d0b359e55073426cb83be80590) |
| SHA-256 | `F978A9E221098D9F6FAA25A619FB30D710EEEA5499ED604BAF8891EFD8E79F8E` |
| 소비 검증 | 빈 패키지 캐시에서 로컬 피드 복원, C# 9 소비 예제의 Counter 실행·JSON 보고서 검증 통과 |

`netstandard2.1`의 `BlackThunder.UniTest.dll`을 제공하며 런타임 패키지 의존성은 없다. `tests/GoldenHind.Tests.csproj`가 이 로컬 패키지를 정확한 버전 `[0.4.0]`으로 참조한다. 실행 방법은 [테스트 안내](../tests/README.md)를 따른다.

각 패키지는 현재 채택한 버전만 보관한다. 새 버전으로 참조를 갱신하고 검증을 마치면, 더 이상 참조하지 않는 이전 버전의 `.nupkg`를 삭제한다.

## UniTest 작업 지침

2026-11-02에 원격 최신 커밋 [c7c5907](https://github.com/dove-creative/unitest/commit/c7c5907155a1003cb0433359694a9f7ab53861f0)의 소스 아카이브를 다운로드하여 [테스트 작성 워크플로](https://github.com/dove-creative/unitest/blob/c7c5907155a1003cb0433359694a9f7ab53861f0/docs/Workflow.ko/01-Test-Authoring-Workflow.md)를 확인했다. 이 지침에 따라 테스트 코드와 문서 폴더 이름을 `UniTest`로 통일한다.

원격 프로젝트의 패키지 버전은 여전히 0.4.0이며 GitHub 릴리스에 새 배포 패키지는 없다. 현재 안정 패키지와 비교한 런타임 소스 변경은 주석 정리뿐이므로 검증된 0.4.0 패키지·참조·원본 커밋과 SHA-256을 유지한다. 같은 버전의 패키지를 다시 포장하거나 덮어쓰지 않는다. 다운로드한 소스 아카이브는 비추적 검증 산출물인 `tests/obj/UniTestConventionUpdate`에 보관하며 프로젝트 참조에는 사용하지 않는다.

## 갱신 절차

1. 해당 프레임워크의 독립 저장소에서 변경과 버전·문서를 갱신하고 솔루션 빌드 및 기존 테스트 전체를 검증한다.
2. 통과한 변경을 커밋하고 해당 커밋에서 Release 패키지를 만든다. `dotnet pack`에 `-p:RepositoryCommit=<원본-SHA>`를 명시한다.
3. 새 버전의 `.nupkg`만 이 폴더로 복사한다. 같은 버전의 파일을 덮어쓰지 않는다.
4. 패키지의 ID·버전·대상 프레임워크·원본 커밋과 SHA-256을 확인하고 이 문서에 기록한다.
5. 사용하는 프로젝트의 패키지 참조를 갱신하고 Golden Hind 솔루션을 빌드한다.
6. 비어 있는 임시 패키지 캐시에서도 로컬 피드로 복원·빌드되는지 확인한다.
7. 모든 프로젝트가 새 버전을 참조하는지 확인하고, 같은 패키지의 이전 버전 `.nupkg`를 삭제한다. 삭제 후에도 로컬 피드 복원·빌드가 통과하는지 확인한다.
8. 새 패키지·이전 패키지 삭제·참조·출처 문서를 함께 커밋한다.

루트 `NuGet.config`가 자사 패키지를 이 로컬 피드에 연결하므로 프레임워크 개발 저장소가 없어도 복원할 수 있다. 검증된 자사 배포본만 버전 관리하며, 일반 NuGet 복원 캐시와 빌드 산출물은 추가하지 않는다. 패키지는 공개 NuGet 피드에 게시하지 않는다.
