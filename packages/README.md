# 안정 패키지

Golden Hind가 소비하는 검증된 자사 NuGet 배포본을 보관한다. 프레임워크 개발은 각각 독립 저장소에서 진행하며, Golden Hind는 외부 개발 소스 경로를 참조하지 않는다.

## 현재 버전

| 항목 | 값 |
| --- | --- |
| 패키지 | `BlackThunder.BlackboxSystem` |
| 버전 | `0.1.2` |
| 파일 | `BlackThunder.BlackboxSystem.0.1.2.nupkg` |
| 원본 저장소 | [dove-creative/blackbox-system](https://github.com/dove-creative/blackbox-system) |
| 원본 커밋 | [9f20c862171e5de6ab4c663c62f8a00070e9ca35](https://github.com/dove-creative/blackbox-system/commit/9f20c862171e5de6ab4c663c62f8a00070e9ca35) |
| SHA-256 | `6875246699F1C77FE2B93D8C4F255665F745A8081F67FC5D2257D6077B6AC2C7` |
| 검증 | Blackbox Release 솔루션 빌드 및 기존 테스트 159개 통과 |
| 소비 검증 | Golden Hind Release 빌드 통과 |

패키지는 `netstandard2.1`의 `BlackThunder.BlackboxSystem.dll`을 제공하며 런타임 패키지 의존성은 없다. Golden Hind 본체는 `[0.1.2]`로 버전을 고정한다. 현재 Golden Hind 전용 테스트 프로젝트는 없으며, 실제 테스트를 추가할 때 UniTest 안정 패키지를 테스트 프로젝트에 연결한다.

## 갱신 절차

1. 독립 Blackbox 저장소에서 변경과 버전·문서를 갱신하고 솔루션 빌드 및 기존 테스트 전체를 검증한다.
2. 통과한 변경을 커밋하고 해당 커밋에서 Release 패키지를 만든다. `dotnet pack`에 `-p:RepositoryCommit=<원본-SHA>`를 명시한다.
3. 새 버전의 `.nupkg`만 이 폴더로 복사한다. 같은 버전의 파일을 덮어쓰지 않는다.
4. 패키지의 ID·버전·대상 프레임워크·원본 커밋과 SHA-256을 확인하고 이 문서에 기록한다.
5. 본체 프로젝트의 정확한 버전을 갱신하고 Golden Hind 솔루션을 빌드한다.
6. 비어 있는 임시 패키지 캐시에서도 로컬 피드로 복원·빌드되는지 확인한 뒤 패키지·참조·출처 문서를 함께 커밋한다.

루트 `NuGet.config`가 자사 패키지를 이 로컬 피드에 연결하므로 프레임워크 개발 저장소가 없어도 복원할 수 있다. 검증된 자사 배포본만 버전 관리하며, 일반 NuGet 복원 캐시와 빌드 산출물은 추가하지 않는다. 패키지는 공개 NuGet 피드에 게시하지 않는다.
