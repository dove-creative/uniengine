# EventHub 이슈 기록

2026-11-03에 .NET 10에서 확인한 항목이다. EH-02는 2026-11-10에 해결한 입력 검증 문제이며 2026-11-12의 메서드별 등록 API에서도 같은 보장을 유지한다. EH-03은 반환된 콜백의 용도를 결정해야 하는 API 계약이다.

## 목차

- EH-02 null 필터 등록 후 리스너 연결 해제
- EH-03 onSleep이 반환하는 콜백의 용도

## EH-02 null 필터 등록 후 리스너 연결 해제

**재현 조건.** 한 리스너에 정상 갱신 콜백을 등록한 뒤 같은 Builder에 null 조건 대리자를 등록한다. 이전 API의 `OnUpdated((Func<int, bool>)null, callback)`에 해당하며, 현재 이름은 OnUpdatedWhere이다.

**이전 동작.** 등록은 성공하고 첫 Update에서 null 필터를 호출하면서 NullReferenceException이 발생했다. 실패한 리스너의 연결 전체가 해제되어 다음 갱신은 정상 콜백에도 도달하지 않았다.

**해결.** OnUpdatedWhere는 null 조건 대리자를 등록 시점에 ArgumentNullException으로 거부한다. object Builder도 원본 조건을 변환 람다로 감싸기 전에 검증한다. 기존 리스너와 콜백은 유지되므로 이후 Update는 정상 등록에 계속 전달된다.

등록 검사는 연결 수명 → 조건 인자 → null 콜백 순서이다. 해제된 연결은 InvalidOperationException이고 허브 폐기 후에는 ObjectDisposedException이 조건 오류보다 우선한다. null 콜백을 넘겨도 잘못된 조건을 무시하지 않는다. OnUpdatedWhen의 null target은 유효한 값 비교이며 OnUpdatedWhere의 null 조건과 메서드 이름으로 구분한다. 두 인자를 구분하기 위한 형변환은 필요하지 않다. [Builder 계약](IEventHubBuilder.md)

## EH-03 onSleep이 반환하는 콜백의 용도

**관찰 결과.** 생성자의 `onSleep` 형식은 `Func<Action>`이다. `Sleep(onAwake)`로 재운 뒤 토큰을 폐기한 재현에서 `onSleep` 호출은 1회, 여기서 반환한 Action의 호출은 0회, `Sleep`에 직접 전달한 `onAwake` 호출은 1회였다. 현재 구현은 수면 진입 시 반환된 Action을 저장하지 않는다. [EventHub 생성자와 Sleep](../../../../src/02-Core/03-E.2-EventHub/EventHub.cs)

**결정할 사항.** `onSleep`이 깨울 때 실행할 동작을 제공하는 계약인지, 수면 진입을 알리는 계약인지 정해야 한다. 전자라면 명시적으로 전달한 `onAwake`와 함께 사용할 때의 실행 순서도 정한다. 후자라면 반환값 없는 Action으로 공개 API를 정리할 때 기존 호출부에 미치는 영향을 확인한다.
