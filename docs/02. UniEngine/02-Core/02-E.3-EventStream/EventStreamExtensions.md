# 경로 연결과 병합

EventStream에서 Where·Take 등의 인스턴스 메서드로 경로를 만들고, 독립적으로 발행할 지점에서 Branch를 호출한다.

## Branch

Branch는 경로 끝에 자신의 EventSource를 가진 새 EventStream을 만든다. 첫 구독까지 부모 쪽 노드를 만들지 않는다. 부모에서 오는 값은 앞선 필터를 통과해야 하며, Branch에 직접 보낸 값은 그 분기의 구독자에게만 전달된다. 같은 경로에서 두 번 호출하면 서로 다른 분기이다. Branch의 Enabled와 부모의 Enabled는 각각의 공급원에 적용된다.

마지막 구독을 해제하면 Branch와 부모의 연결이 정리된다. 부모가 완료·오류로 종료하면 연결된 Branch에도 같은 신호가 전달되고, 명시적으로 폐기하면 Branch는 조용히 폐기된다.

## Merge

EventStream.Merge는 외부 IEventSource들을 하나로 연결하고 그 Merge를 공급원으로 소유한다. 상위 하나가 완료해도 나머지는 계속 발행한다. 모든 상위가 완료하면 결과도 완료하고, 상위의 오류는 즉시 전달한다. 결과를 폐기하면 상위 구독 핸들이 해제된다. 상위 자체의 수명은 각 상위의 TerminationMode를 따른다.

Merge는 각 공급원의 발행을 별개의 값으로 전달한다. 그래프 내부 경로 두 개를 다시 하나의 노드로 합치는 기능은 제공하지 않는다. 같은 발행을 두 경로로 보내고 하나로 합치는 경우의 중복 전달과 부모별 완료 정책은 [본 구현 보고서](Issues/EventStreamImplementation.md)에 기록했다.
