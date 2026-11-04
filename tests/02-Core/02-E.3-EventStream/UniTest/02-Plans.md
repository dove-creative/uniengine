# EventStream 테스트 계획

## 목차

1. NUnit 직접 사례
2. UniTest 실행 경로
3. 실행과 기록

## NUnit 직접 사례

[상태·동작 표](01-Tables.md)의 스트림과 그래프를 공개 API로 구성한다. 경로 공유·정리 여부는 internal NodeCount와 관찰자의 수신 이력을 함께 확인한다.

| 범위 | 테스트 파일 | 검증 대상 |
| --- | --- | --- |
| C1–C7 | [스트림 연결](../EventStreamConnectionTests.cs) · [그래프 수명](../EventStreamLifecycleTests.cs) | 새 스트림의 연결과 해제, 종료 전달, 외부 공급원 비소유, 직접 입력, 수신자로서의 동작을 검증한다. |
| O1–O5 | [연산자](../EventStreamOperatorTests.cs) · [그래프](../EventStreamGraphTests.cs) | 조건 참조 공유, 거짓 조건 아래 가지치기, 상태 연산자의 상태 공유와 수명, Debug 전달을 확인한다. |
| I1–I2 | [순서 그룹과 마스크](../EventStreamCompositionTests.cs) | 그룹마다 같은 조건을 따로 평가하고, Branch 경로를 독립적으로 해제한다. |
| F1–F2와 전파 | [스트림과 그룹](../EventStreamPropagationTests.cs) · [그래프 전파](../EventStreamGraphPropagationTests.cs) | 추가는 다음 발행부터, 제거는 즉시 반영한다. 멀티캐스트·재진입·예외·참조 회수와 실패 뒤의 전파를 확인한다. |
| M1–M2 | [기본 동작과 Merge](../EventStreamTests.cs) · [그래프 수명](../EventStreamLifecycleTests.cs) | 폐기된 입력 거부, 입력별 완료·폐기, 입력별 전달을 검증한다. |
| 할당 | [스트림·순서 그룹](../EventStreamAllocationTests.cs) · [그래프·Branch](../EventStreamGraphAllocationTests.cs) | 별도 인스턴스를 예열한 뒤 첫·반복 전파의 할당량을 측정한다. 마지막 Take(1)가 Branch를 완료·폐기하는 경우도 포함한다. |

## UniTest 실행 경로

[상태 전이 프로젝트](EventStreamStateProjectTests.cs)는 CompactProject와 CompactLab을 Inline 모드에서 실행하고 두 종료 경로의 이력을 확인한다. 매 단계에서 부모와 Branch를 새로 구성해 이전 이력을 재현한다.

| 구간 | Lab 순서 |
| --- | --- |
| 공통 | SubscribePair → ReleaseFirst → PublishWhileDisabled |
| U1 | ResumePublication → ReleaseLast |
| U2 | CompleteWhileDisabled |

## 실행과 기록

.NET 10 Release에서 본 모듈과 전체 테스트를 실행한다. 명시적 Benchmark는 일반 검증에서 제외한다. [검증 결과](03-Results.md)에 실행 범위와 결과를 기록한다.
