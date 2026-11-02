# Adaptive Collections 검증 결과

## 목차

1. 최종 결과
2. 할당 측정
3. 실행 명령
4. 검증 중 조정
5. 확인된 범위

## 최종 결과

2026-11-02 기준 결과이다.

| 검증 | 결과 |
| --- | --- |
| 본체 .NET 9 Release 빌드 | 성공 |
| 최신 관련 범위 테스트: AdaptiveCollections·ValueEnumerator·EventStream | 146개 통과, 실패 0개, 건너뜀 0개 |
| 이전 전체 테스트 실행: 당시 NUnit·UniTest 142개 | 142개 통과, 실패 0개, 건너뜀 0개 |
| 공통 순회 구현·호출 목록 캐시의 C# 9·.NET Standard 2.1 호환성 빌드 | 경고 0개, 오류 0개 |
| 변경 파일 형식 | UTF-8·LF 확인 |

호환성 검증은 IEnumerationSource·ValueEnumerator·AdaptiveCollection·AdaptiveDictionary·AdaptiveHashSet·InvocationListCache 원본을 연결한 임시 프로젝트로 수행했다. 임시 프로젝트와 산출물은 `src/obj/ValueEnumeratorCompatibility`에 두며 버전 관리에 포함하지 않는다. AdaptiveAction과 기존 Infrastructure 의존성을 포함한 전체 본체의 .NET Standard 호환성을 검증한 결과는 아니다.

최초 전체 실행은 기존 53개 사례에 사전 18개, 집합 29개, Action 호출 목록 캐시 42개를 더한 142개였다. 이후 사전·집합의 구조체 순회 회귀 사례를 각각 2개 추가하여, 최신 관련 범위는 기존 53개·사전 20개·집합 31개·Action 호출 목록 캐시 42개로 총 146개이다. 여기에는 EventStream 4개와 UniTest 상태 전이 사례가 포함된다.

최신 실행은 아래 필터로 관련 범위만 재검증한 결과이며, 이후 추가된 Infrastructure 테스트를 포함한 저장소 전체 실행 결과는 아니다. 테스트 코드는 Core 모듈별 폴더에 배치하며 단일 테스트 어셈블리를 유지한다.

## 할당 측정

각 경로를 2,000회 워밍업한 뒤 10,000회 반복한 동기 구간에서 `GC.GetAllocatedBytesForCurrentThread()`의 차이를 측정했다. 컬렉션 생성·콜백 등록·단언·출력은 측정 구간 밖에 있다. Action의 첫 호출은 별도 인스턴스로 실행 경로를 워밍업한 뒤, 새 대리자를 새 컬렉션에 등록하여 측정했다.

| 경로 | 측정 결과 |
| --- | --- |
| 직접 foreach, 즉시 반영, 빈 목록 | 0바이트 |
| 직접 foreach, 즉시 반영, 16개 항목 | 0바이트 |
| 직접 foreach, 지연 반영, 빈 목록 | 0바이트 |
| 직접 foreach, 지연 반영, 16개 항목 | 0바이트 |
| ForEach, 지연 반영, 3개 항목, 미리 생성한 콜백 | 0바이트 |
| Dictionary·HashSet 직접 foreach, 두 모드·빈 저장소·16개 항목 | 모두 0바이트 |
| Dictionary·HashSet 반복 추가·삭제·후속 묶음 방문, 두 모드 | 모두 0바이트 |
| Dictionary·HashSet 제거 기록 이후 직접 foreach, 기본 동등성 구조체, 두 모드 | 모두 0바이트 |
| Action 인자 0~3개, 직접·기본 클래스·인터페이스·AddRange 등록, 두 모드 | 첫 호출 및 10,000회 반복 모두 0바이트 |
| Action 입력 열거 도중 실패·호출·Clear 후 새 등록 | 이미 저장된 항목의 첫 호출도 0바이트 |

인터페이스 순회의 박싱, 저장소의 최초 용량 확보·확장, 사용자 콜백·비교·해시 코드·예외의 할당은 이 결과의 범위에 포함하지 않는다. AdaptiveAction의 GetInvocationList 배열은 등록 시 캐시에 준비하므로 등록 비용은 남는다. 순회 중 새로운 대리자를 등록하는 비용도 별도이다.

## 실행 명령

```powershell
dotnet build src/GoldenHind.sln -c Release --no-restore
& 'C:/Users/user/AppData/Local/Microsoft/dotnet/dotnet.exe' test tests/GoldenHind.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~Adaptive|FullyQualifiedName~ValueEnumerator|FullyQualifiedName~EnumerationAllocation|FullyQualifiedName~CollectionStateProject|FullyQualifiedName~EventStream' --logger 'trx;LogFileName=AdaptiveCollections.Review.trx' --results-directory tests/bin/TestResults
dotnet build src/obj/ValueEnumeratorCompatibility/ValueEnumeratorCompatibility.csproj -c Release --no-restore
```

본체와 호환성 빌드는 .NET 9 SDK, 테스트는 사용자 위치의 .NET 10 SDK를 사용했다. 최신 관련 범위 146개의 TRX는 `tests/bin/TestResults/AdaptiveCollections.Review.trx`이다. 이전 전체 실행 142개는 필터 없이 실행하여 `tests/bin/TestResults/AdaptiveCollections.Allocation.trx`에 기록했다. 두 결과 파일은 버전 관리에 포함하지 않는다. 최초 실행 또는 프로젝트 이동 후에는 테스트 명령에서 `--no-restore`를 제외하여 의존성을 복원한다.

## 검증 중 조정

- 제한된 실행 환경에서 testhost 연결이 90초 뒤 실패했다. 샌드박스 밖에서 같은 전체 테스트 명령을 실행하여 142개 통과를 확인했다.
- 최초 본체 빌드에 다른 빌드와의 파일 접근 경합으로 복사 재시도 경고가 있었으며 빌드는 성공했다. 최종 전체 테스트 빌드에는 해당 경고가 없었다.
- 순회 중 제거가 한 번 있었던 사전·집합은 비운 제거 표시 집합에 내부 배열이 남아, 이후 순회에서도 방문 항목마다 해시 코드를 계산했다. 기본 동등성을 쓰는 구조체는 이때 항목마다 24바이트씩 박싱되었으므로, 제거 표시가 있을 때만 조회하도록 바꾸고 회귀 사례 4개를 추가했다. 추가 후 AdaptiveCollections·ValueEnumerator 범위 142개를 통과했고, 최신 재검증에서는 EventStream 4개까지 포함한 관련 범위 146개가 모두 통과했다. 저장소 전체 테스트는 이 재검증에 포함하지 않았다.
- AddRange의 컬렉션 입력은 기존 일괄 복사와 자기 추가 동작을 유지한다. 스트리밍 입력은 항목별로 호출 목록을 준비하여 입력 코드가 중간에 Invoke하거나 실패해도 저장된 콜백의 캐시가 준비되게 했다.

## 확인된 범위

지연·즉시 반영, 삭제 위치와 중복 항목, 사전 값 교체·집합 재추가, Clear 이후 추가, 재사용하는 후속 방문 버퍼, 정상·조기·예외 종료, 중첩 순회, 구조체 복사본과 오래된 식별자, 표준 인터페이스 사용을 검증했다. 집합의 정리 중 해시 코드 실패 후 순회 복구도 확인했다.

Action은 등록 경로별 첫 호출, 멀티캐스트 자기 제거·이전 중복 제거·예외 정책, 자기 AddRange와 부분 실패, 제거·Clear 후 캐시가 대리자와 대상 객체를 붙잡지 않는지 검증했다. 기존 EventStream 전파와 UniTest 상태 전이도 통과했다.
