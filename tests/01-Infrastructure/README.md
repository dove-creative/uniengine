# Infrastructure 테스트

## 목차

- 구성과 결과
- 실행과 의존 관계
- 시간 표시 검증 이력

## 구성과 결과

공개 API의 개별 계약은 NUnit으로, 상태가 이어지는 주요 흐름은 UniTest로 검증한다. 2026-11-02와 2026-11-03의 .NET SDK 10.0.403·Release 실행에서 모듈별 최신 결과를 합산한 기록이다.

| 모듈 | 사례 | 통과 | 실패 | 결과 문서 |
| --- | --- | --- | --- | --- |
| Configuration | 7 | 7 | 0 | [결과](01-M.1-Configuration/docs/03.%20Results.md) |
| Objects | 10 | 10 | 0 | [결과](02-O.1-Objects/docs/03.%20Results.md) |
| 표시 유틸리티 | 42 | 42 | 0 | [결과](03-M.1-Utilities/docs/03.%20Results.md) |
| JSON 변환·등록 | 269 | 269 | 0 | [결과](03-M.1-Utilities/Json/docs/03.%20Results.md) |
| ExceptionHandler | 30 | 30 | 0 | [결과](03-M.2-ExceptionHandler/docs/03.%20Results.md) |
| Log | 13 | 13 | 0 | [결과](03-M.3-LogSystem/docs/03.%20Results.md) |
| 모듈별 결과 합계 | 371 | 371 | 0 | 건너뜀 0 |

변경 전 기존 사례 142개는 모두 통과했다. 최초 추가 후 전체 실행은 511개 중 509개 통과, 2개 실패였으며 정적 상태 관련 50개를 독립 재실행한 결과도 모두 통과했다. 이후 시간 표시 결함을 수정하고 회귀 사례 2개를 추가하여 표시 유틸리티 42개만 다시 실행했고 모두 통과했다. 수정 후 전체 테스트는 재실행하지 않았다. 각 모듈의 docs 폴더에 표·계획·결과를 기록한다.

이후 UniTest 지침 갱신으로 폴더 이름 7개를 변경한 뒤 전체 517개를 재실행했다. 당시 결과는 513개 통과, 4개 실패였으며, Infrastructure는 367개 통과, 4개 실패였다. 실행 시점의 시간 표시 소스가 이전 계산식이어서 기존 극값과 추가 시간 경계 사례가 다시 실패했다. 이후 틱 기반 수정을 다시 반영하여 표시 유틸리티 42개가 모두 통과했다. 표는 이 최신 모듈 결과를 반영하며 재반영 후 전체 테스트는 실행하지 않았다.

2026-11-03에는 본체를 netstandard2.1·C# 9 라이브러리로 전환한 뒤 전체 520개를 실행했다. 본체 Release 빌드는 경고·오류 없이 성공했고, 결과는 516개 통과, 4개 실패, 건너뜀 0개였다. 실패 4개는 시간 표시 소스에 틱 기반 수정이 다시 빠져 있었기 때문이다. 수정을 다시 반영한 뒤 표시 유틸리티 42개가 모두 통과했으며, 이후 전체 테스트는 다시 실행하지 않았다.

## 실행과 의존 관계

테스트가 본체를 ProjectReference로 참조한다. 본체는 InternalsVisibleTo로 UniEngine.Tests에 internal 접근 권한을 제공하며 테스트 프로젝트와 패키지를 참조하지 않는다. 본체 Release 빌드와 생성된 어셈블리 메타데이터·런타임 의존성을 확인했다.

Configuration·Log의 각 사례와 UniTest 단계별 재현은 새 프로세스와 고유 경로를 사용한다. 프로세스 격리·시간 제한·소유 경로 정리는 Support가 담당한다. ExceptionHandler는 각 사례 전후 처리기를 해제하고 병렬 실행을 막는다.

```powershell
dotnet build src/UniEngine.sln -c Release --no-restore
& 'C:/Users/user/AppData/Local/Microsoft/dotnet/dotnet.exe' test tests/UniEngine.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=Infrastructure-Full.trx' --results-directory tests/bin/TestResults
& 'C:/Users/user/AppData/Local/Microsoft/dotnet/dotnet.exe' test tests/UniEngine.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~ConfigurationTests|FullyQualifiedName~ConfigurationStateProjectTests|FullyQualifiedName~LogTests|FullyQualifiedName~LogStateProjectTests|FullyQualifiedName~ExceptionHandler' --logger 'trx;LogFileName=Infrastructure-Static-Isolation.trx' --results-directory tests/bin/TestResults
```

최초 전체 실행의 종료 코드는 당시 결함 두 사례 때문에 1이었다. 기존 기준 실행은 Infrastructure-Baseline.trx, 최초 전체 실행은 Infrastructure-Full.trx, 정적 상태 독립 재실행은 Infrastructure-Static-Isolation.trx에 기록한다. 수정 후 표시 유틸리티 실행은 Infrastructure-Formatting-Fix.trx이며 종료 코드는 0이다. 모두 tests/bin/TestResults 아래의 비추적 산출물이다. 최초 샌드박스 실행은 testhost 연결 제한으로 중단되어 샌드박스 밖에서 다시 실행했다.

폴더명 변경 후 전체 검증은 같은 전체 테스트 명령에서 결과 파일 이름을 UniTest-Convention-Update.trx로 지정했다. 당시 시간 표시 실패 4개로 종료 코드는 1이다.

netstandard2.1 전환 후 전체 검증은 시스템에 설치한 .NET SDK 10.0.403의 기본 dotnet으로 실행하여 NetStandardMigration.trx에 기록했다. 시간 표시 재수정 후 모듈 검증은 Infrastructure-Formatting-Ticks.trx이다.

## 시간 표시 검증 이력

TimeSpan.MaxValue·MinValue에서 48분을 7분으로 출력하던 결함을 수정했다. 총 시간은 틱 정수 연산으로 구하고 분·초는 범위가 제한된 성분을 사용하여 범위 초과와 큰 값의 반올림을 피한다. 기존 실패 테스트의 기대값을 유지했고 큰 시간 경계의 양·음수 회귀 사례를 추가했다.

위 수정 직후에는 42개가 모두 통과했다. 최신 폴더명 변경 검증 시점에는 해당 소스가 다시 이전 계산식인 상태였으며, 극값 2개와 시간 경계 2개가 실패했다. [표시 유틸리티 결과](03-M.1-Utilities/docs/03.%20Results.md)에 현재 실패와 이전 성공 기록을 구분하여 남겼다.

2026-11-02에 틱 기반 수정을 다시 반영하여 관련 42개가 모두 통과했지만(Infrastructure-Formatting-Reapply.trx), 이 수정은 커밋되지 않았다. 2026-11-03 전체 실행 시점에 소스가 다시 이전 계산식이어서 같은 4개가 실패했다. 같은 날 틱 기반 계산을 다시 반영하여 42개가 모두 통과했으며, 최신 모듈 검증 산출물은 tests/bin/TestResults/Infrastructure-Formatting-Ticks.trx이다.
