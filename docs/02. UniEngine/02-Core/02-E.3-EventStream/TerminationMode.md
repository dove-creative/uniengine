# TerminationMode

TerminationMode는 EventSource가 마지막 구독을 잃을 때 자신의 수명을 정하는 정책이다. 네임스페이스는 Blackthunder.UniEngine.Core.EventStreamCore이다.

| 값 | 마지막 구독이 해제되었을 때 |
| --- | --- |
| Complete | 공급원은 계속 살아 있고 나중에 다시 구독할 수 있다. 기본 EventSource에 적용된다. |
| NoSubscribers | 공급원과 공급원이 보관한 상위 연결을 폐기한다. Merge 공급원의 기본값이다. |

두 정책 모두 OnCompleted·OnError는 통지 후 폐기하고, 명시적 Dispose는 통지 없이 정리한다. 구독이 아직 없는 공급원을 만들거나 속성을 설정하는 것만으로는 자동 폐기되지 않는다.

EventStream 그래프의 노드와 Branch에는 별도의 수명 규칙이 있다. 빈 경로는 즉시 제거하고 사용자가 만든 루트는 유지한다. Branch는 첫 구독 시 부모에 연결하고 마지막 구독이 떠나면 폐기한다. [경로 연결과 병합](EventStreamExtensions.md)
