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

기존 검증과 랜덤 연속 테스트를 포함한 전체 실행은 888개가 모두 통과했다. 제품 오류는 발견되지 않았다.

전체 실행에서 Log의 세 seed 합계는 73.635초였다. 대상별 실행시간은 완료 후에 확인할 수 있어 실행 중에는 60초 초과를 확인하지 못했다. seed별 시간은 [Log 검증 결과](../01-Infrastructure/03-M.3-LogSystem/docs/03.%20Results.md)에 기록했다.

최초 기준 실행은 샌드박스에서 VSTest의 testhost 연결이 90초 후 시간 초과되어 테스트를 시작하지 못했다. 샌드박스 밖에서 다시 실행해 기준 결과를 얻었다.

## 모듈별 기록

### 2026-11-11 When 추가 검증

When 값 동등성 경로의 공유·수명과 발행 중 변경 프로젝트를 추가했다. EventStream의 랜덤 연속 범위는 기존 다섯 프로젝트의 15개에서 일곱 프로젝트의 21개로 늘었다. 기존에 추가된 EventHub 3개를 포함한 전체 랜덤 연속 사례는 39개이다.

| 추가 대상 | seed 0 | seed 1 | seed 2 | 실제 도달 깊이 | 결과 |
| --- | ---: | ---: | ---: | --- | --- |
| When 공유·수명 | 216.840 ms | 219.416 ms | 229.716 ms | 각 100 / 100 | 3 / 3 통과 |
| When 발행 중 변경 | 127.160 ms | 116.749 ms | 117.800 ms | 각 100 / 100 | 3 / 3 통과 |

.NET SDK 10.0.403 Release 전체 테스트 1,028개가 통과했고 실패·건너뜀은 0개이다. 랜덤 연속 사례 39개도 모두 목표 깊이에 도달했다. 대상별 세 seed의 실제 실행시간 합계는 모두 60초 이내였으며 가장 긴 Log는 55.146초였다. 중간 확인 기준을 넘은 대상이나 중단한 실행은 없다.

실행 명령과 검증 범위는 [EventStream 계획](../02-Core/02-E.3-EventStream/docs/02.%20Plans.md)과 [EventHub 계획](../02-Core/03-E.2-EventHub/docs/02.%20Plans.md)에, 상세 결과와 미검증 항목은 [EventStream 결과](../02-Core/02-E.3-EventStream/docs/03.%20Results.md)와 [Hub 결과](../02-Core/docs/Hub%20Results.md)에 있다.

### 2026-11-14 Property 검증

Property의 직접 계약·경계값·소비 시나리오 43개, 모든 후보를 거치는 고정 경로 1개와 랜덤 연속 경로 3개를 실행했고 총 47개가 모두 통과했다. seed 0·1·2는 각각 깊이 100에 도달했으며 실제 연속 실행시간 합계는 3,853.643 ms이다. Property 범위만 실행했으며 다른 모듈 전체 테스트를 다시 실행한 결과는 아니다.

최초 샌드박스 실행은 testhost 연결 시간 초과로 테스트 시작 전에 중단됐고, 같은 빌드 결과를 샌드박스 밖에서 실행해 통과 결과를 얻었다. seed별 시간·실행 명령·한계는 [Property 검증 결과](../02-Core/02-O.1-Property/docs/03.%20Results.md)에 기록했다.

### 2026-11-15 사전·집합·LifecycleHub 검증

AdaptiveDictionary·AdaptiveHashSet의 두 반영 모드와 LifecycleHub에 UniTest 고정 경로 5개·랜덤 연속 경로 15개를 추가했다. seed 0·1·2가 모두 깊이 100에 도달했다. Dictionary의 삭제 후 TryGetValue out 값 결함을 발견하여 기본값 반환으로 수정하고 직접 회귀 사례 4개를 검증했다.

추가 범위 24개와 전체 일반 테스트 1,302개가 모두 통과했다. Release 빌드의 경고·오류, 테스트 실패·건너뜀은 0개이다. 선택 실행은 약 1분 6초, 전체 실행은 약 2분 17초였다. 새 대상·모드별 세 seed의 합계는 모두 60초 이내이다. 최초 중단·실패와 상세 범위는 [공통 결과](../02-Core/docs/State%20Model%20Results.md)에 있다.

### 2026-11-15 Handle·Token 검증

Handle·Token의 일반 직접 사례 23개, EventStream·EventHub 소비 사례 3개, UniTest 고정 경로 2개와 랜덤 연속 경로 6개를 선택 실행했고 총 34개가 모두 통과했다. 두 대상의 seed 0·1·2는 모두 깊이 100에 도달했으며 세 seed 합계는 Handle 835.548 ms, Token 2,610.740 ms이다.

Release 빌드의 경고·오류와 테스트 실패·건너뜀은 0개이다. 본체 변경 없이 테스트를 보강했으며 전체 일반 테스트를 다시 실행한 결과는 아니다. 상태·동작 범위와 실행 명령은 [Handle·Token 검증 결과](../02-Core/02-E.1-HandleToken/docs/03.%20Results.md)에 있다.

### 2026-11-26 GeoSystem 그룹·제네릭 Route 검증

제네릭 Route System 직접 사례 7개, 그룹 직접 사례 10개, GroupProject 고정 경로 1개와 seed 0·1·2의 깊이 100 연속 경로 3개를 검증했다. 집중 실행의 세 seed는 각각 3,093.018·2,852.487·2,734.611 ms로 모두 통과했다. 기존 사례를 포함한 GeoSystem 204개와 샘플의 기본 장면 17개·Tracer/Wheel 9개 조건이 통과했다.

본체·테스트·샘플 Release 빌드는 경고·오류 없이 성공했다. 사용자의 장시간 실행 승인 후 수행한 전체 회귀는 1,837개 통과, 실패·건너뜀 0개이며 239.881초가 걸렸다. 전체 회귀에서도 GroupProject의 세 seed가 모두 깊이 100에 도달했다. 최초 testhost 연결 중단과 NUnit 검증식 수정 후 재실행, 명령·사례별 집계·한계는 [GeoSystem 공통 결과](../03-Services/GeoSystem/Common/docs/03.%20Results.md)에 기록했다.

### 2026-11-26 GeoSystem 위치 역조회 검증

SceneProject와 PolyProject의 고정 경로·연속 상태 검증에 TryGetLocation을 추가했다. 각 프로젝트의 seed 0·1·2가 모두 깊이 100/100에 도달했으며 세 seed 합계는 각각 3,345.758 ms와 5,480.380 ms이다. 새 직접 사례 23개를 포함한 GeoSystem 227개와 샘플 verify 26개 조건이 통과했고 본체·테스트·샘플 Release 빌드에 경고·오류가 없었다. 전체 회귀는 사용자 요청으로 생략했다. [집중 검증 결과와 실행 이력](../03-Services/GeoSystem/Planar/docs/03.%20Results.md)에 실제 검증 범위와 중간 실패를 기록했다.

### 모듈 문서

각 결과 문서에 seed, 실제 도달 깊이, 실행시간, 결과를 기록했다.

- [Configuration](../01-Infrastructure/01-M.1-Configuration/docs/03.%20Results.md)
- [LogSystem](../01-Infrastructure/03-M.3-LogSystem/docs/03.%20Results.md)
- [ExceptionHandler](../01-Infrastructure/03-M.2-ExceptionHandler/docs/03.%20Results.md)
- [Handle·Token](../02-Core/02-E.1-HandleToken/docs/03.%20Results.md)
- [AdaptiveCollections](../02-Core/02-E.2-AdaptiveCollections/docs/03.%20Results.md)
- [LifecycleHub](../02-Core/03-E.1-LifecycleHub/docs/03.%20Results.md)
- [Property](../02-Core/02-O.1-Property/docs/03.%20Results.md)
- [EventStream](../02-Core/02-E.3-EventStream/docs/03.%20Results.md)

- [GeoSystem 공통·그룹](../03-Services/GeoSystem/Common/docs/03.%20Results.md)

## 2026-11-26 Network Creator 검증

Linear Creator의 직접 사례 17개·고정 경로 1개·seed 0·1·2 연속 경로 3개, 총 21개가 통과했다. 각 seed는 독립 초기 상태에서 깊이 100/100을 완료했다. 집중 실행시간은 각각 428.331 ms·348.563 ms·301.240 ms이다.

최종 Release 전체 회귀 1,881개가 통과했고 실패·건너뜀은 0개이다. TRX 실행 구간은 227.930초이다. 전체 실행에서 Creator seed 0·1·2는 각각 466.584 ms·391.521 ms·372.630 ms였으며 모두 깊이 100/100에 도달했다. 상태 전이·연결·임시 객체 정리와 확정 결과의 비소유 수명을 검증했다.

[모듈 결과](../03-Services/GeoSystem/Common/docs/03.%20Results.md) · [전체 회귀 기록](../03-Services/GeoSystem/Common/docs/Runs/NetworkCreators-FullSuite-20261126.log)
