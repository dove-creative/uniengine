# EventStream 테스트 계획

## 목차

1. NUnit 직접 사례
2. UniTest 실행 경로
3. 실행과 기록

## NUnit 직접 사례

[상태·동작 표](01-Tables.md)의 각 사례를 실제 스트림과 구독 핸들로 구성한다. 테스트 본문에서 수신 값, 종료 통지, IsDisposed와 반환 객체를 관찰한다.

| 범위 | 테스트 파일 | 검증 대상 |
| --- | --- | --- |
| C1–C5 | [공통 계약](../EventStreamContractTests.cs) | 세 스트림에 같은 계약을 적용하여 핸들 소유권, 인계 실패, 비활성 전달과 종료를 비교한다. |
| O1–O4 | [연산자](../EventStreamOperatorTests.cs) | 조건 평가 시점, Take의 잔여 횟수, TakeWhile의 완료와 Append 분기의 수명을 확인한다. |
| I1 | [라우터와 순서 그룹 조합](../EventStreamCompositionTests.cs) | 대상 마스크 결합, 실행 그룹 순서, 명시적 연결 해제를 한 전달 경로에서 검증한다. |

## UniTest 실행 경로

[상태 전이 프로젝트](EventStreamStateProjectTests.cs)는 BlackThunder.UniTest의 CompactProject와 CompactLab을 Inline 모드로 실행한다. NUnit에서 프로젝트를 실행하여 각 Lab의 성공과 두 종료 경로의 도달 여부를 확인한다.

두 경로 모두 처음부터 실제 source와 Where, 구독 핸들로 이력을 재현한다. 각 단계에서 두 구독자의 수신 값, 조건 평가 횟수, 완료 횟수와 스트림의 폐기 상태를 확인한다. U1에서는 마지막 구독 해제로 Where만 정리되고, U2에서는 source의 완료 통지가 비활성인 Where까지 전달된다.

| 구간 | Lab 순서 |
| --- | --- |
| 공통 | SubscribePair → ReleaseFirst → PublishWhileDisabled |
| U1 | ResumePublication → ReleaseLast |
| U2 | CompleteWhileDisabled |

## 실행과 기록

.NET 10 Release에서 EventStream 모듈의 기존 테스트와 신규 사례를 함께 실행한다. 실행 명령과 사례별 집계는 [검증 결과](03-Results.md)에 기록한다.
