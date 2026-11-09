# Services

UniEngine의 공통 기능을 조합하여 특정 응용 문제를 해결하는 서비스 계층이다.

## 목차

- 소개
- 정의된 범위

## 소개

- 개요: 상태 기계·Mesh 편집·그래프 모델링을 다루는 설계 영역이다.
- 목적: 특정 응용 문제를 해결하는 기능을 UniEngine의 공통 기반 위에서 구성한다.
- 구현 상태: 현재 src에 대응 구현이 없으며 각 항목에서 정의된 역할과 미정 범위를 구분한다.

## 정의된 범위

| 분류 | 설계 항목 | 정의된 역할 |
| --- | --- | --- |
| StateMachines | [Usm](StateMachines/Usm.md) | Unified State Machine, FSM과 BT |
| StateMachines | [Scp](StateMachines/Scp.md) | Sequence Clip Player, 페트리넷형 상태 기계 |
| Mesh 편집 | [MEP](MEP.md) | Mesh Extension Package |
| 그래프 모델링 | [GeoSystem](GeoSystem.md) | 그래프 모델링 시스템 |

현재 src에는 이 서비스들의 구현이 없다. 각 문서는 Hierarchy에 정의된 정체성과 구현 범위를 구분하며, 존재하지 않는 API나 보유 상태를 구현 사실로 설명하지 않는다.

[전체 계층](../README.md)
