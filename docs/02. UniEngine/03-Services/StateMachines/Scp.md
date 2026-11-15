# Scp

클립의 종료 결과와 실행 조건을 연결하여 재생 흐름을 구성하는 Sequence Clip Player 서비스의 초안 구현이다.

## 목차

- 소개
- 구성과 실행 흐름
- 이관 범위
- 후속 설계

## 소개

- 개요: 클립과 조건을 조합하여 순차 실행·분기·합류를 표현한다.
- 목적: 공격 패턴처럼 앞선 작업의 결과에 따라 다음 작업을 실행하는 흐름을 구성한다.
- 구현: Project-Index·Project H에서 사용한 코드를 이관한 초안이며, 정식 API와 실행 계약은 후속 설계에서 확정한다.

계층표에서는 페트리넷형 상태 기계로 분류한다. 현재 구현이 일반적인 페트리넷의 토큰 소비 규칙을 제공한다는 뜻은 아니다.

## 구성과 실행 흐름

| 구성 요소 | 역할 |
| --- | --- |
| IClip·Clip | 개별 작업의 시작·갱신·종료와 공유 Blackboard 접근을 제공한다. |
| ClipToken | 클립의 종료 결과와 Payload를 전달한다. |
| CTC·PlayCondition·CheckType | 대상 클립과 토큰의 조건을 지정하고 All·Any 방식으로 후속 작업의 실행 여부를 판단한다. |
| Sequence | 시작 클립과 후속 조건을 보유하고 호스트가 전달한 시간으로 실행을 진행한다. |
| Delay | 전달받은 deltaTime을 누적하여 지정 시간이 지난 뒤 종료하는 클립이다. |

호스트가 클립을 등록하고 AddAfter 또는 PlayCondition으로 연결한다. Start 또는 비활성 상태의 Update가 시퀀스를 시작하며, 실행마다 새 Blackboard를 생성한다. 이후 호스트가 Update에 deltaTime을 전달하면 클립이 실행되고, 종료 토큰을 조건으로 후속 클립이 시작된다. 실행 중인 클립이 없으면 다음 Update에서 완료를 보고하며, Stop으로 실행을 중단할 수 있다.

## 이관 범위

공개 네임스페이스는 BlackThunder.UniEngine.StateMachines.Scp이다. Project-Index와 Project H의 공통 구현을 Unity 없이 사용할 수 있도록 옮겨 둔 UniEngine MVP의 Clip·토큰·조건·Sequence와 기존 테스트를 기반으로 한다. Project H의 Delay와 MVP의 init 호환 형식도 함께 이관하였다.

기존 API 형태와 동작을 유지하며 로그·예외 처리는 UniEngine의 Log·ExceptionHandler를 사용한다. Unity 컴포넌트나 보스 공격 코드는 포함하지 않는다.

## 후속 설계

다음 항목은 이번 이관에서 변경하지 않은 초안의 제약이며, 정식 구현 시 검토할 범위이다.

- 중단 후 재시작 시 클립에 남은 종료 토큰의 처리.
- 토큰의 Version·Completed 의미. 현재 Sequence가 축적하는 토큰은 각각 null·true를 사용한다.
- 누적 토큰의 재사용·소비 규칙과 반복 실행의 의미.
- 시작·종료 콜백이나 정리 도중 예외가 발생했을 때의 상태 정리와 재진입 계약.

기존 테스트의 통과는 이 항목들의 정식 계약이나 안정성을 보증하지 않는다.

[상위 구성](../README.md)
