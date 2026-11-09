# EventHub 이슈 기록

2026-11-03에 .NET 10에서 확인한 항목이다. EH-02는 재현된 입력 검증 문제이고, EH-03은 반환된 콜백의 용도를 결정해야 하는 API 계약이다.

## 목차

- EH-02 null 필터 등록 후 리스너 연결 해제
- EH-03 onSleep이 반환하는 콜백의 용도

## EH-02 null 필터 등록 후 리스너 연결 해제

**재현 조건.** 한 리스너에 정상 갱신 콜백을 등록한 뒤 같은 Builder에서 `OnUpdated((Func<int, bool>)null, callback)`을 호출한다.

**관찰 결과.** 등록은 성공한다. 첫 `Update()`에서 정상 콜백이 한 번 실행된 뒤 null 필터를 호출하면서 `NullReferenceException`이 발생한다. 허브는 실패한 리스너의 연결 전체를 해제하므로 `HasListener(listener)`는 false가 되고 두 번째 갱신은 정상 콜백에도 도달하지 않는다. [필터 등록과 실행](../../../../src/02-Core/03-E.1-EventHub/EventHubBuilder.cs) · [리스너 예외 처리](../../../../src/02-Core/03-E.1-EventHub/EventHub.cs)

**검토 중.** 필터를 받는 `OnUpdated` 오버로드의 유지 여부는 결정 전이다. 유지할 경우에는 null 필터를 등록 시점에 `ArgumentNullException`으로 거부하고 기존 연결이 계속 갱신을 받는지 검증한다.

## EH-03 onSleep이 반환하는 콜백의 용도

**관찰 결과.** 생성자의 `onSleep` 형식은 `Func<Action>`이다. `Sleep(onAwake)`로 재운 뒤 토큰을 폐기한 재현에서 `onSleep` 호출은 1회, 여기서 반환한 Action의 호출은 0회, `Sleep`에 직접 전달한 `onAwake` 호출은 1회였다. 현재 구현은 수면 진입 시 반환된 Action을 저장하지 않는다. [EventHub 생성자와 Sleep](../../../../src/02-Core/03-E.1-EventHub/EventHub.cs)

**결정할 사항.** `onSleep`이 깨울 때 실행할 동작을 제공하는 계약인지, 수면 진입을 알리는 계약인지 정해야 한다. 전자라면 명시적으로 전달한 `onAwake`와 함께 사용할 때의 실행 순서도 정한다. 후자라면 반환값 없는 Action으로 공개 API를 정리할 때 기존 호출부에 미치는 영향을 확인한다.
