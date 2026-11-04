# EventStream 검증 결과

## 목차

1. Release 검증
2. 이전 사례와의 대응
3. UniTest 상태 전이
4. 실행 명령

## Release 검증

2026-11-04에 .NET SDK 10.0.403으로 검증했다. Release 빌드는 경고와 오류 없이 성공했다.

| 실행 범위 | 통과 / 실행 |
| --- | ---: |
| EventSource·EventStream 모듈 | 119 / 119 |
| 일반 전체 | 673 / 673 |
| 별도 명시적 Benchmark | 1 / 1 |

## 이전 사례와의 대응

교체 전 일반 전체 688개에서 이번 검증의 673개로 15개 줄었다. 이전에는 공급원 계약 C1–C5를 기본 EventStream, OrderedEventStream, EventRouter에서 반복했다. 같은 입력의 세 묶음을 EventSource 계약 한 묶음으로 통합했다. 마스크 변환기가 없는 EventRouter, 빈 대상 배열 전용 오버로드, 기존 Merge 확장 메서드의 생성 경로는 제거된 API여서 별도 사례로 세지 않는다.

| 기존 검증 | 본 구현에서 검증하는 동작 |
| --- | --- |
| 기본 스트림과 라우터·순서 스트림의 중복 계약 | EventSource의 구독, 핸들 인계 실패, 명시적 폐기, 완료·오류·비활성 동작과 원래 콜백의 참조 회수를 검증한다. 그래프에서는 같은 대리자를 등록해도 각 구독을 독립적으로 해제할 수 있다. |
| 대상 마스크, 라우터 연결, 순서 그룹 | 마스크의 합집합 판정과 불일치·빈 마스크를 Filter로 검증한다. 그룹 간 생성 순서, 전파 중 그룹 추가·제거, 같은 그룹 안의 조건 공유와 그룹 간 조건 분리를 확인한다. |
| 별도 Where·Take·Skip, Append | Where 노드의 참조 공유·가지치기, 등록별 상태, Branch의 첫 연결·마지막 해제·직접 발행과 부모 종료를 검증한다. |
| 외부 스트림 병합과 예외 | Merge 생성 중 부분 연결 실패 회수, 동기·중복 완료, 오류, All·Any 완료를 확인한다. 그래프 실패가 생기면 정상 형제와 공급원 구독도 순회를 마치고 첫 미처리 예외가 발행자에게 돌아온다. |
| 첫 전파·반복 전파 할당 | 값·참조 형식 공급원, 그룹, 마스크, 필터 체인, Merge, 비활성 경로와 Take의 마지막 값에 Branch가 완료·폐기되는 경로를 측정한다. |

## UniTest 상태 전이

U1과 U2는 NUnit 사례 하나에서 도달한 서로 다른 종료 이력이다. 첫 구독을 해제한 뒤 남은 구독만 4를 받으며, Branch가 비활성인 동안에는 짝수 조건의 누적 평가 횟수가 3으로 유지된다.

- U1에서는 재활성화 뒤 8을 전달하고 마지막 구독을 해제했다. 부모의 노드가 모두 제거되고 Branch가 폐기됐다. 이후 부모의 새 구독은 10을 받았고 필터 평가 횟수는 4에 머물렀다.
- U2에서는 Branch가 비활성일 때 부모를 완료했다. 남은 구독자만 완료를 한 번 받고 부모와 Branch가 폐기됐다.

## 실행 명령

```powershell
dotnet test tests/GoldenHind.Tests.csproj -c Release --filter 'FullyQualifiedName~BlackThunder.UniEngine.Tests.EventStream|FullyQualifiedName~BlackThunder.UniEngine.Tests.EventSource'
dotnet test tests/GoldenHind.Tests.csproj -c Release
```

[상태·동작 표](01-Tables.md) · [테스트 계획](02-Plans.md)
