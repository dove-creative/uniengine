# UniTest 랜덤 연속 테스트 결과

## 적용 범위

2026-11-10에 각 모듈의 테스트 계획에 따라 UniTest 랜덤 연속 검증을 실행한 결과이다. 각 대상의 seed 0·1·2를 독립된 초기 상태에서 실행했으며, 기존 모듈·통합·실패 경로의 직접 검증도 함께 수행했다.

| 모듈 | 대상 | seed당 목표 깊이 | 통과 / 실행 |
| --- | --- | ---: | ---: |
| Configuration | 초기화 전 실패·복구와 초기화 이후 경로 유지 | 8 | 3 / 3 |
| LogSystem | 기록·저장·초기화·실패 복구 | 100 | 3 / 3 |
| ExceptionHandler | 처리기 수명과 예외 보고·호출 | 100 | 3 / 3 |
| AdaptiveCollections | 지연·즉시 반영 두 모드 | 100 | 6 / 6 |
| EventStream | Branch, Merge, 상태 연산자, ForFlags 공유·수명, ForFlags 발행 중 변경 | 100 | 15 / 15 |

Configuration은 [테스트 계획](../01-Infrastructure/01-M.1-Configuration/docs/02.%20Plans.md)의 초기화 전후 경로에 따라 깊이 8까지 실행했다.

## 실행 결과

.NET SDK 10.0.403 Release 빌드는 경고·오류 없이 성공했다. 변경 전 기본 검증은 858개 통과, 실패·건너뜀 0개였다. 모듈별 랜덤 연속 실행은 30개 모두 목표 깊이에 도달했으며 같은 대상의 세 seed 실행시간 합계는 모두 60초 이하였다.

기존 검증과 랜덤 연속 테스트를 포함한 전체 실행은 888개가 모두 통과했다. 제품 오류는 발견되지 않았다. [전체 실행 TRX](../obj/RandomContinuous/integration.trx)

전체 실행에서 Log의 세 seed 합계는 73.635초였다. 대상별 실행시간은 완료 후 생성된 TRX에서 확인할 수 있어 실행 중에는 60초 초과를 확인하지 못했다. seed별 시간은 [Log 검증 결과](../01-Infrastructure/03-M.3-LogSystem/docs/03.%20Results.md)에 기록했다.

최초 기준 실행은 샌드박스에서 VSTest의 testhost 연결이 90초 후 시간 초과되어 테스트를 시작하지 못했다. 샌드박스 밖에서 다시 실행해 기준 결과를 얻었다. [최초 중단 TRX](../obj/RandomContinuous/baseline-sandbox.trx) · [기준 실행 TRX](../obj/RandomContinuous/baseline.trx)

## 모듈별 기록

### 2026-11-11 When 추가 검증

When 값 동등성 경로의 공유·수명과 발행 중 변경 프로젝트를 추가했다. EventStream의 랜덤 연속 범위는 기존 다섯 프로젝트의 15개에서 일곱 프로젝트의 21개로 늘었다. 기존에 추가된 EventHub 3개를 포함한 전체 랜덤 연속 사례는 39개이다.

| 추가 대상 | seed 0 | seed 1 | seed 2 | 실제 도달 깊이 | 결과 |
| --- | ---: | ---: | ---: | --- | --- |
| When 공유·수명 | 216.840 ms | 219.416 ms | 229.716 ms | 각 100 / 100 | 3 / 3 통과 |
| When 발행 중 변경 | 127.160 ms | 116.749 ms | 117.800 ms | 각 100 / 100 | 3 / 3 통과 |

.NET SDK 10.0.403 Release 전체 테스트 1,028개가 통과했고 실패·건너뜀은 0개이다. 랜덤 연속 사례 39개도 모두 목표 깊이에 도달했다. 대상별 세 seed의 실제 실행시간 합계는 모두 60초 이내였으며 가장 긴 Log는 55.146초였다. 중간 확인 기준을 넘은 대상이나 중단한 실행은 없다.

시간과 이력은 [전체 실행 TRX](../bin/TestResults/EventStreamWhen/All.trx)의 각 사례 출력에 있다. 실행 명령과 검증 범위는 [EventStream 계획](../02-Core/02-E.3-EventStream/docs/02.%20Plans.md)과 [EventHub 계획](../02-Core/03-E.2-EventHub/docs/02.%20Plans.md)에, 상세 결과와 미검증 항목은 [EventStream 결과](../02-Core/02-E.3-EventStream/docs/03.%20Results.md)와 [Hub 결과](../02-Core/docs/Hub%20Results.md)에 있다.

### 2026-11-14 Property 검증

Property의 직접 계약·경계값·소비 시나리오 43개, 모든 후보를 거치는 고정 경로 1개와 랜덤 연속 경로 3개를 실행했고 총 47개가 모두 통과했다. seed 0·1·2는 각각 깊이 100에 도달했으며 실제 연속 실행시간 합계는 3,853.643 ms이다. Property 범위만 실행했으며 다른 모듈 전체 테스트를 다시 실행한 결과는 아니다.

최초 샌드박스 실행은 testhost 연결 시간 초과로 테스트 시작 전에 중단됐고, 같은 빌드 결과를 샌드박스 밖에서 실행해 통과 결과를 얻었다. seed별 시간·이력·실행 명령·한계는 [Property 검증 결과](../02-Core/02-O.1-Property/docs/03.%20Results.md)에 기록했다.

### 2026-11-15 사전·집합·LifecycleHub 검증

AdaptiveDictionary·AdaptiveHashSet의 두 반영 모드와 LifecycleHub에 UniTest 고정 경로 5개·랜덤 연속 경로 15개를 추가했다. seed 0·1·2가 모두 깊이 100에 도달했다. Dictionary의 삭제 후 TryGetValue out 값 결함을 발견하여 기본값 반환으로 수정하고 직접 회귀 사례 4개를 검증했다.

추가 범위 24개와 전체 일반 테스트 1,302개가 모두 통과했다. Release 빌드의 경고·오류, 테스트 실패·건너뜀은 0개이다. 선택 실행은 약 1분 6초, 전체 실행은 약 2분 17초였다. 새 대상·모드별 세 seed의 합계는 모두 60초 이내이다. 최초 중단·실패 기록, 상세 범위와 원본 TRX는 [공통 결과](../02-Core/docs/State%20Model%20Results.md)에 있다.

### 2026-11-15 Handle·Token 검증

Handle·Token의 일반 직접 사례 23개, EventStream·EventHub 소비 사례 3개, UniTest 고정 경로 2개와 랜덤 연속 경로 6개를 선택 실행했고 총 34개가 모두 통과했다. 두 대상의 seed 0·1·2는 모두 깊이 100에 도달했으며 세 seed 합계는 Handle 835.548 ms, Token 2,610.740 ms이다.

Release 빌드의 경고·오류와 테스트 실패·건너뜀은 0개이다. 본체 변경 없이 테스트를 보강했으며 전체 일반 테스트를 다시 실행한 결과는 아니다. 상태·동작 범위, 실행 명령과 원본 TRX는 [Handle·Token 검증 결과](../02-Core/02-E.1-HandleToken/docs/03.%20Results.md)에 있다.

### 모듈 문서

각 결과 문서에 seed, 실제 도달 깊이, 실행시간, 결과를 기록했다. 연결된 TRX의 각 NUnit 사례 출력에 동작 이력이 있다.

- [Configuration](../01-Infrastructure/01-M.1-Configuration/docs/03.%20Results.md)
- [LogSystem](../01-Infrastructure/03-M.3-LogSystem/docs/03.%20Results.md)
- [ExceptionHandler](../01-Infrastructure/03-M.2-ExceptionHandler/docs/03.%20Results.md)
- [Handle·Token](../02-Core/02-E.1-HandleToken/docs/03.%20Results.md)
- [AdaptiveCollections](../02-Core/02-E.2-AdaptiveCollections/docs/03.%20Results.md)
- [LifecycleHub](../02-Core/03-E.1-LifecycleHub/docs/03.%20Results.md)
- [Property](../02-Core/02-O.1-Property/docs/03.%20Results.md)
- [EventStream](../02-Core/02-E.3-EventStream/docs/03.%20Results.md)
