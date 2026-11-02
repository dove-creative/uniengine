# UniTest

Golden Hind에서 상태와 동작을 구성하여 재현·검증하는 외부 테스트 프레임워크이다.

## 목차

1. 소개
2. 검증 구성과 흐름
3. 참조

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 테스트 대상의 상태 전이와 기대 결과를 실행 가능한 검증으로 구성한다. |
| 목적 | 같은 조건과 동작을 재현하여 변경 이후에도 기대하는 결과가 유지되는지 확인한다. |
| 구현 | 단일 테스트 프로젝트가 UniTest 프로젝트와 NUnit 실행기를 연결한다. |

## 검증 구성과 흐름

UniTest 자체는 독립 저장소에서 개발하고 Golden Hind의 검증 사례는 tests 폴더에서 관리한다. GoldenHind.Tests는 BlackThunder.UniTest 0.4.0과 NUnit 3을 사용하며 본체에 테스트 의존성을 추가하지 않는다. NUnit 직접 테스트는 각 모듈 폴더에 두고, UniTest 상태 전이 구현은 모듈의 UniTest 폴더에서 테스트 표·계획·결과 문서와 함께 관리한다. 모두 같은 테스트 어셈블리에 포함한다.

상태 전이 사례는 CompactProject의 Inline 방식으로 실행하며 전체 성공·취소 여부와 최종 실행 횟수를 검증한다.

| 대상 | 상태 전이 | 구현 |
| --- | --- | --- |
| AdaptiveCollection | 생성 → 추가하며 순회 → 삭제하며 순회 | [CollectionStateProjectTests](../../../tests/02-Core/02-E.2-AdaptiveCollections/UniTest/CollectionStateProjectTests.cs) |
| Configuration | 초기화 실패 → 미초기화 유지 → 정상 초기화 → 재초기화 거부 | [ConfigurationStateProjectTests](../../../tests/01-Infrastructure/01-M-Configuration/UniTest/ConfigurationStateProjectTests.cs) |
| ExceptionHandler | 처리기 등록 → 교체 → 해제 | [ExceptionHandlerStateProjectTests](../../../tests/01-Infrastructure/03-M.2-ExceptionHandler/UniTest/ExceptionHandlerStateProjectTests.cs) |
| Log | 기록·저장·재초기화 및 저장 실패 → 복구 → 보존한 버퍼 저장 | [LogStateProjectTests](../../../tests/01-Infrastructure/03-M.3-LogSystem/UniTest/LogStateProjectTests.cs) |

UniTest는 이전 Lab을 재실행하므로 Configuration·Log의 각 Lab은 해당 단계까지의 전체 흐름을 새 프로세스에서 재현한다. 공통 실행 보조 코드는 tests/Support에서 관리한다. ExceptionHandler는 병렬 실행을 막고 각 사례 전후에 정적 처리기를 해제한다.

## 참조

[테스트 작성 워크플로](https://github.com/dove-creative/unitest/blob/c7c5907155a1003cb0433359694a9f7ab53861f0/docs/Workflow.ko/01-Test-Authoring-Workflow.md)의 폴더 이름 기준을 사용한다. 런타임 패키지는 검증된 0.4.0을 유지하며 지침 확인 이력은 패키지 문서에 기록한다.

[테스트 구성과 실행](../../../tests/README.md) · [Infrastructure 결과](../../../tests/01-Infrastructure/README.md) · [패키지 문서](../../../packages/README.md) · [보조 프레임워크](README.md)
