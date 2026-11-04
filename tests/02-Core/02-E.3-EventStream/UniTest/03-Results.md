# EventStream 검증 결과

## 목차

1. Release 검증
2. 이전 사례와의 대응
3. 성능 비교
4. UniTest 상태 전이
5. 실행 명령

## Release 검증

2026-11-04에 .NET SDK 10.0.403으로 검증했다. Release 빌드는 경고와 오류 없이 성공했다.

| 실행 범위 | 통과 / 실행 |
| --- | ---: |
| EventStream 모듈 | 127 / 127 |
| 일반 전체 | 681 / 681 |
| 별도 명시적 Benchmark | 1 / 1 |

## 이전 사례와의 대응

이전 검증의 일반 전체 673개에서 681개로 늘었다. EventSource 공개 형식과 Chain·Connect·TerminationMode를 없애면서 그 계약 사례를 빼고, 상태 공유·직접 입력·입력별 Merge·수신자 사례를 추가했다.

| 이전 검증 | 현재 검증하는 동작 |
| --- | --- |
| EventSource의 Chain·Connect, 핸들 인계 | 공개 API에서 제거했다. 새 스트림의 연결 수명과 폐기는 C1·C5로 검증한다. |
| Dispatch 호환성 | 파생 지점을 제거하여 사례를 뺐다. |
| 구독마다 독립적인 Take·Skip 상태 | 같은 스트림의 구독자가 상태를 공유하고, 연산자를 다시 호출하면 새로 시작한다. |
| 외부 공급원의 직접 발행 거부 | 모든 스트림이 값을 직접 받는다. 횟수·조건 반영과 재진입 거부를 C6으로 검증한다. |
| Merge의 All·Any 완료, 공급원 입력 | EventStream 입력의 All 완료, 입력 폐기, 입력별 전달을 M1·M2로 검증한다. Any 완료는 제거했다. |

## 성능 비교

같은 벤치마크 코드를 이전 커밋 b80b018과 현재 구현에서 차례로 실행한 중앙값이다. 각 시나리오는 1.5초 이상 예열한 뒤 200,000회씩 세 라운드를 측정했다. 고정 구독의 할당은 양쪽 모두 0 B이다. 벤치마크는 실행마다 차이가 있어 작은 차이는 구조의 차이로 해석하지 않는다.

| 시나리오 | b80b018 | 현재 | 단위 |
| --- | ---: | ---: | --- |
| E 공유 Filter, 4/64 수신 | 0.1390 | 0.0789 | µs/전파 |
| F 개별 Filter, 4/64 수신 | 0.2630 | 0.2359 | µs/전파 |
| G 공통 조건 통과 후 공유 Filter | 0.1388 | 0.0748 | µs/전파 |
| I 60/64 수신 | 0.4322 | 0.3362 | µs/전파 |
| J 필터 없이 64명 수신 | 0.3451 | 0.2861 | µs/전파 |
| K 공통 조건 탈락 | 0.0615 | 0.0076 | µs/전파 |
| L 구독자마다 Take | 0.1913 | 0.1705 | µs/전파 |
| H 공유 Filter 구독 교체 | 1.8443 | 2.2305 | µs/프레임 |
| H 프레임당 할당 | 5888 | 5888 | B/프레임 |
| M 일회성 Take(1) 16개 교체 | 2.5973 | 7.1458 | µs/프레임 |
| M 프레임당 할당 | 8704 | 23040 | B/프레임 |

Take가 노드에서 스트림으로 바뀌면서 M의 비용이 늘었다. 매 프레임 Take(1)을 많이 만드는 코드는 이 비용을 고려해야 한다.

## UniTest 상태 전이

U1과 U2는 NUnit 사례 하나에서 도달한 서로 다른 종료 이력이다. 첫 구독을 해제한 뒤 남은 구독만 4를 받으며, Branch가 비활성인 동안에는 짝수 조건의 누적 평가 횟수가 3으로 유지된다.

- U1에서는 재활성화 뒤 8을 전달하고 마지막 구독을 해제했다. 부모의 노드가 모두 제거되고 Branch가 폐기됐다. 이후 부모의 새 구독은 10을 받았고 필터 평가 횟수는 4에 머물렀다.
- U2에서는 Branch가 비활성일 때 부모를 완료했다. 남은 구독자만 완료를 한 번 받고 부모와 Branch가 폐기됐다.

## 실행 명령

```powershell
dotnet test tests/GoldenHind.Tests.csproj -c Release --filter 'FullyQualifiedName~BlackThunder.UniEngine.Tests.EventStream&TestCategory!=Benchmark'
dotnet test tests/GoldenHind.Tests.csproj -c Release
dotnet test tests/GoldenHind.Tests.csproj -c Release --filter 'FullyQualifiedName=BlackThunder.UniEngine.Tests.EventStreamBenchmarks.ComparePropagationAndSubscriptionChurn' --logger 'console;verbosity=detailed'
```

[상태·동작 표](01-Tables.md) · [테스트 계획](02-Plans.md)
