# Adaptive Collections 테스트 계획

## 목차

1. 실행 구성
2. 검증 기준

## 실행 구성

테스트는 단일 프로젝트 `tests/GoldenHind.Tests.csproj`에서 소유하고, 본체를 ProjectReference로 연결한다. 코드는 `tests/02-Core` 아래에서 `src/02-Core`의 모듈 폴더에 대응하도록 배치한다. 로컬 UniTest 0.4.0과 NUnit 3 실행기를 사용하며 .NET 10 SDK로 실행한다. NUnit의 개별 사례를 주 검증으로 사용하고 UniTest 프로젝트로 상태 전이 재실행도 확인한다.

| 테스트 파일 (tests/02-Core 기준) | 표의 범위 | 구현 방향 |
| --- | --- | --- |
| 01-O.3-Objects/ValueEnumeratorTests.cs | V1–V3 | 컬렉션과 독립된 소스 및 실패 주입으로 지연 시작·정리 책임을 검증한다. |
| 02-E.2-AdaptiveCollections/AdaptiveCollectionTests.cs | C1–C8 | 두 반영 모드, 변경 중 관찰값, 종료 경로, 복사본 수명을 검증한다. |
| 02-E.2-AdaptiveCollections/EnumerationAllocationTests.cs | A1 | 빈 목록·비어 있지 않은 목록의 직접 foreach를 워밍업하고 동기 구간의 할당량을 측정한다. |
| 02-E.2-AdaptiveCollections/AdaptiveActionTests.cs | R1 | 실제 AdaptiveAction 공개 API를 통해 순회 소비자의 회귀 동작을 검증한다. |
| 02-E.2-AdaptiveCollections/AdaptiveDictionaryTests.cs | D1·B1·A2 | 교체·삭제·후속 묶음·수명과 직접 순회, 제거 기록 이후 순회 및 반복 변경 할당을 검증한다. |
| 02-E.2-AdaptiveCollections/AdaptiveHashSetTests.cs | H1·B1·A2 | 유일성·재추가·후속 묶음·수명·정리 실패와 제거 기록 이후 순회 및 반복 할당을 검증한다. |
| 02-E.2-AdaptiveCollections/AdaptiveActionAllocationTests.cs | A3·A4 | 별도 인스턴스로 실행 경로를 워밍업하고 새 대리자 등록 후 첫 호출 및 반복 호출의 할당과 캐시 수명을 검증한다. |
| 02-E.3-EventStream/EventStreamTests.cs | R2 | 실제 EventStream 공개 API를 통해 전파 경로의 회귀 동작을 검증한다. |
| 02-E.2-AdaptiveCollections/UniTest/CollectionStateProjectTests.cs | U1 | UniTest Inline 프로젝트로 세 단계 상태 전이를 두 반영 모드에서 실행한다. |

R2는 순회 소비자로서의 EventStream 회귀만 다룬다. EventStream 자체의 계약·연산자·할당 검증은 [EventStream 테스트 계획](../../02-E.3-EventStream/UniTest/02-Plans.md)에서 관리한다.

## 검증 기준

- 공개 API 변경이므로 추가한 테스트 전체를 Release로 실행한다.
- 전역 ExceptionHandler를 사용하는 사례는 병렬 실행하지 않고 각 사례 뒤 초기화한다.
- 할당 측정 구간에는 컬렉션 생성, 테스트 단언, 출력, 대리자 생성이 들어가지 않는다.
- 인터페이스 경로는 기능을 확인한다. JIT의 박싱 제거 여부에 좌우되는 양수 할당량 단언은 하지 않는다.
- 본체가 C# 9·.NET Standard 2.1을 대상으로 하므로, 공통 계약·구조체·목록·사전·집합·호출 목록 캐시의 호환성은 본체 Release 빌드로 함께 확인한다.
