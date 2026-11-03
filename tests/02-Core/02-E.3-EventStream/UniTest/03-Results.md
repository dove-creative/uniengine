# EventStream 검증 결과

## 목차

1. 실행 결과
2. 상태 전이 결과
3. 실행 명령

## 실행 결과

2026-11-03에 .NET SDK 10.0.403으로 GoldenHind.Tests의 EventStream 모듈을 Release 구성에서 실행했다. NUnit 어댑터가 발견한 101개 사례가 모두 통과했다. 실패와 건너뜀은 각각 0개이다.

| 테스트 클래스 | 구분 | 통과 |
| --- | --- | ---: |
| EventStreamTests | 기존 기본·연산자 동작 | 21 |
| EventStreamPropagationTests | 기존 전파 동작 | 26 |
| EventStreamAllocationTests | 기존 전파 할당량 | 25 |
| EventStreamContractTests | 신규 공통 계약 C1–C5 | 21 |
| EventStreamOperatorTests | 신규 연산자 O1–O4 | 6 |
| EventStreamCompositionTests | 신규 라우터와 순서 그룹 조합 I1 | 1 |
| EventStreamStateProjectTests | 신규 UniTest 상태 전이 U1·U2 | 1 |

## 상태 전이 결과

U1과 U2는 NUnit에서 실행한 UniTest 프로젝트 하나의 두 종료 경로이다. 두 경로 모두 첫 구독자를 해제한 뒤 남은 구독자만 4를 받았고, Where를 비활성화한 동안에는 조건 평가 횟수가 3으로 유지되었다.

- U1에서는 재활성화 후 두 번째 구독자가 8을 받았고, 마지막 구독 해제로 Where가 폐기되었다. 이후 source에 직접 등록한 새 수신자는 10을 받았으며 Where 조건은 누적 4회에서 멈췄다.
- U2에서는 비활성 상태에서 source를 완료하자 남은 구독자가 완료를 한 번 받고 source와 Where가 모두 폐기되었다. 조건 평가 횟수는 3이었다.

## 실행 명령

저장소 루트에서 실행한다. 테스트 어댑터가 작성한 사례별 결과는 `tests/bin/TestResults/EventStream.ContractsAndState.trx`에 있다.

```powershell
dotnet test tests/GoldenHind.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~BlackThunder.UniEngine.Tests.EventStream' --logger 'trx;LogFileName=EventStream.ContractsAndState.trx' --results-directory tests/bin/TestResults
```

[상태·동작 표](01-Tables.md) · [테스트 계획](02-Plans.md)
