# Services

UniEngine의 공통 기능을 조합하여 특정 응용 문제를 해결하는 서비스 계층이다.

## 목차

- 소개
- 정의된 범위

## 소개

- 개요: 상태 기계·Mesh 편집·그래프 모델링을 다루는 설계 영역이다.
- 목적: 특정 응용 문제를 해결하는 기능을 UniEngine의 공통 기반 위에서 구성한다.
- 구현 상태: StateMachines.Usm은 FSM·BT 실행을 제공하며, StateMachines.Scp는 기존 클립 재생기를 이관한 초안이다. 다른 항목은 각 문서에서 정의된 역할과 미정 범위를 구분한다.

## 정의된 범위

| 분류 | 설계 항목 | 정의된 역할 |
| --- | --- | --- |
| StateMachines | [Usm](StateMachines/Usm.md) | Unified State Machine, FSM과 BT |
| StateMachines | [Scp](StateMachines/Scp.md) | Sequence Clip Player, 페트리넷형 상태 기계의 초안 구현 |
| Mesh 편집 | [MEP](MEP.md) | Mesh Extension Package |
| 그래프 모델링 | [GeoSystem](GeoSystem/README.md) | Navigation 연결, 평면 공간 계산과 경로 참조 |

StateMachines.Usm은 Node·Work·Transitioner를 직접 조립하여 FSM·BT·Parallel을 구성하고 외부 호출로 실행한다. EventStream과 독립적이다. StateMachines.Scp는 클립·조건·Sequence·Delay로 재생 흐름을 구성하는 기존 구현을 보존하며, 정식 설계는 후속 범위이다. GeoSystem은 Navigation·평면 직선·PolySegment·Pin·Clip·Route와 경로 탐색을 제공한다. PolySegment는 FreeHandle·Bezier2·Bezier3를 지원하고 Circular·Offset은 후속 범위이다. MEP는 후속 구현 범위이다.

[전체 계층](../README.md)
