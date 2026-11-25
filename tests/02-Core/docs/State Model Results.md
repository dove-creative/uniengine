# 사전·집합·LifecycleHub 상태 모델 검증 결과

## 실행 결과

2026-11-15, Windows·.NET SDK 10.0.404·Release 기준이다. 본체 netstandard2.1과 테스트 net10.0 빌드는 경고·오류 없이 통과했다.

| 범위 | 통과 / 실행 | 결과 |
| --- | ---: | --- |
| Dictionary·HashSet UniTest 고정 경로 | 4 / 4 | 각 모드에서 모든 동작을 생성 포함 깊이 17까지 직접 실행했다. |
| Dictionary·HashSet UniTest 랜덤 경로 | 12 / 12 | 객체·모드별 seed 0·1·2가 모두 깊이 100에 도달했다. |
| LifecycleHub UniTest 고정 경로 | 1 / 1 | 등록·해제·폐기·실패·재진입·새 허브 수명을 깊이 34까지 실행했다. |
| LifecycleHub UniTest 랜덤 경로 | 3 / 3 | seed 0·1·2가 모두 깊이 100에 도달했다. |
| Dictionary 삭제 후 조회 직접 회귀 | 4 / 4 | 두 모드·교체 취소 여부 조합에서 false와 기본 out 값, 남은 키 보존을 확인했다. |
| 추가 범위 선택 실행 | 24 / 24 | 약 1분 6초, 실패·건너뜀 0개이다. |
| 전체 일반 테스트 | 1,302 / 1,302 | 약 2분 17초, 실패·건너뜀 0개이다. |

범위별 수는 서로 겹치므로 합산하지 않는다. 전체 실행에서도 새 랜덤 경로 15개가 각각 깊이 100에 도달했다. 대상·모드별 세 seed 합계는 모두 60초 이내이며, 가장 긴 기존 Log의 합계는 약 48.235초이다.

상세 계약·seed별 시간·한계는 [AdaptiveCollections 결과](../02-E.2-AdaptiveCollections/docs/03.%20Results.md)와 [LifecycleHub 결과](../03-E.1-LifecycleHub/docs/03.%20Results.md)에 있다.

## 실패와 재검증

최초 샌드박스 실행은 VSTest가 testhost에 90초 동안 연결하지 못해 테스트 본문 시작 전에 중단됐다. 동일한 빌드로 샌드박스 밖에서 실행하자 20개 중 12개 통과·8개 실패였다. 실패는 모두 Dictionary가 순회 중 삭제한 키의 TryGetValue 실패 결과에 이전 out 값을 남기는 결함을 드러냈다.

실패 시 out 값을 기본값으로 확정하고 직접 회귀 사례 4개를 추가한 뒤 Release 빌드, 선택 실행 24개, 전체 일반 실행 1,302개가 통과했다. 최초 중단·실패는 최종 통과 수에 포함하지 않았다.

## 실행 근거

```powershell
dotnet build tests/UniEngine.Tests.csproj -c Release --no-restore
dotnet test tests/UniEngine.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~DictionaryStateProjectTests|FullyQualifiedName~HashSetStateProjectTests|FullyQualifiedName~LifecycleHubStateProjectTests|Name~RemovedKeysReturnDefaultValuesDuringIteration'
dotnet test tests/UniEngine.Tests.csproj -c Release --no-build --no-restore
```

Unity 실행과 별도 벤치마크·Extended 검증은 포함하지 않았다.

변경한 C# 파일 5개의 LF·후행 공백과 새 상태 모델의 줄 길이를 확인했다. 관련 문서 10개의 로컬 링크 99개가 유효했다. 기존 수정 파일 19개는 작업 전후 SHA-256이 일치했으며 수학 문서와 사람이 관리하는 다이어그램도 포함된다. 테스트 안내는 기존 내용을 유지하면서 새 테스트 항목을 추가했다.
