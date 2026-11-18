# Scp

Scp(Sequence Clip Player)는 앞선 클립의 종료 결과를 조건으로 후속 클립을 재생하는 서비스의 초안이다.

## 목차

- 소개
- 클립과 공유 데이터
- 토큰으로 분기하고 합류하기
- 시퀀스의 갱신과 완료
- 이관 범위와 후속 설계

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 클립은 자기 작업의 종료를 알리고, 시퀀스는 그 결과를 기다리던 클립을 시작한다. |
| 목적 | 공격 패턴이나 연출에서 여러 작업이 갈라졌다가 다시 모이는 흐름을 구성한다. |
| 구현 | 시퀀스에 시작 클립과 후속 클립의 조건을 등록하고, 호스트가 경과 시간(deltaTime)을 전달해 진행한다. |

계층표에서는 이러한 분기와 합류를 페트리넷형 상태 기계로 분류한다. 현재 시퀀스는 한 실행 동안 종료 토큰을 축적하며 한 토큰을 여러 후속 조건에서 참조할 수 있다.

## 클립과 공유 데이터

| 구성 요소 | 책임과 데이터 |
| --- | --- |
| IClip | 시퀀스가 개별 클립의 Start·Update·Stop을 호출하고 TryGetToken으로 종료 결과를 회수하는 계약이다. |
| `Clip<TBlackboard>` | IsPlaying과 해당 실행의 Blackboard를 보유한다. 시작할 때 선행 토큰, 갱신할 때 deltaTime을 받는다. 작업이 끝나면 Stop(payload)으로 종료 결과를 남긴다. |
| ClipToken | 결과를 낸 Clip, 작업의 출력인 Payload, 완료 표시(Completed)와 버전(Version)을 담는다. |
| `Sequence<TBlackboard>` | 시작 클립, 후속 조건, 실행 중인 클립과 이번 실행의 누적 토큰을 관리한다. Start마다 새 Blackboard를 생성해 그 실행의 모든 클립에 전달한다. |
| Delay | 시작할 때 대기 시간을 다시 설정하고, 갱신마다 deltaTime을 빼서 남은 시간이 0 이하가 되면 끝나는 클립이다. |

같은 실행 내내 공유할 데이터는 Blackboard에, 특정 선행 작업의 판단 결과는 ClipToken.Payload에 둔다. 시작 클립은 빈 선행 토큰 목록을 받고, 후속 클립은 자신의 조건을 만족시킨 토큰을 받는다. `Clip<TBlackboard>`는 전달된 Blackboard가 지정한 참조 형식과 호환되어야 한다. 비제네릭 Clip·Sequence·Delay는 object를 사용한다.

## 토큰으로 분기하고 합류하기

클립이 Stop을 호출하면 기본 Clip은 토큰 하나를 보관한다. TryGetToken이 성공하면 클립에서는 그 토큰을 비우고, Sequence는 결과를 이번 실행의 누적 토큰에 보관한다. 새 토큰이 들어올 때마다 그 클립을 선행 대상으로 갖는 조건을 평가한다.

| 표현 | 재생 조건 |
| --- | --- |
| AddAfter(before, next) | before에서 나온 토큰이 있으면 next를 시작한다. 같은 before에 후속 클립을 여러 개 등록하면 흐름이 갈라진다. |
| CTC | 지정한 클립 인스턴스의 토큰과 일치한다. WithPayload·WithVersion·CompletedOnly로 일치 조건을 좁힌다. |
| PlayCondition과 CheckType.All | 등록한 조건마다 일치하는 누적 토큰이 하나씩 있어야 한다. 후속 클립에 조건 순서대로 일치 토큰을 전달한다. 기본 방식이다. |
| PlayCondition과 CheckType.Any | 조건 순서대로 검사해 처음 일치한 토큰 하나를 후속 클립에 전달한다. |

토큰은 한 조건이 사용한 뒤에도 같은 실행의 다른 조건에서 일치할 수 있다. 한 번의 Update 중 같은 후속 클립의 시작은 한 번으로 제한하며 재생 중인 클립도 다시 시작하지 않는다. 이미 종료된 후속 클립은 이후에 관련 토큰이 새로 들어오면 누적 토큰으로 조건을 다시 만족할 수 있다.

## 시퀀스의 갱신과 완료

1. 호스트가 Add로 시작 클립을, AddAfter나 조건을 받는 Add로 후속 클립을 등록한다. 활성 시퀀스에 등록을 시도하면 InvalidOperationException이 발생한다.
2. Start는 새 Blackboard를 만들고 모든 시작 클립을 시작한다. 비활성 시퀀스에 바로 Update를 호출해도 시작한다. 이미 활성인 시퀀스의 Start는 false를 반환한다.
3. Update는 실행 중인 클립에 deltaTime을 전달하고 종료 토큰을 회수한다. 조건을 만족한 후속 클립의 Start는 이 호출 안에서 일어나며, 후속 클립이 첫 deltaTime을 받는 것은 다음 Update이다.
4. 마지막 클립의 토큰을 회수한 다음, 실행 중인 클립이 없는 상태로 Update에 진입하면 성공으로 종료한다. 호스트는 이때 갱신을 멈춘다. 비활성 시퀀스에 다시 Update를 호출하면 다음 실행이 시작된다.

| Update(deltaTime, out succeeded) 반환값 | succeeded | 의미 |
| --- | --- | --- |
| true | false | 이번 갱신을 수행했다. 마지막 클립이 끝났을 수 있으므로 다음 갱신에서 완료 여부를 확인한다. |
| false | true | 갱신에 진입했을 때 남은 클립이 없어 시퀀스를 정상 종료했다. |
| false | false | 시작이나 갱신이 실패했다. |

정상 완료의 기준은 실행 중인 클립이 모두 끝났는지이다. 분기 조건이 맞지 않아 시작하지 않은 클립이 있어도 시퀀스는 완료될 수 있다. 활성 시퀀스에 명시적으로 Stop을 호출하면 실행 중인 클립을 정지하고 목록과 누적 토큰을 비우며, Stopped에 false를 전달한다. 정상 완료 시에는 Stopped에 true를 전달한다. 갱신 중 클립이 예외를 던지면 시퀀스 정지와 ExceptionHandler를 통한 보고를 시도한다.

## 이관 범위와 후속 설계

공개 네임스페이스는 `BlackThunder.UniEngine.StateMachines.Scp`이다. Project-Index·Project H에서 사용한 재생기를 UniEngine MVP에서 옮긴 초안으로, Clip·토큰·조건·Sequence와 Project H의 Delay, MVP의 init 호환 형식을 포함한다. 로그와 예외 보고에는 UniEngine의 Log·ExceptionHandler를 사용한다.

다음 항목은 정식 설계에서 확정할 범위이다.

- 중단 후 재시작 시 각 클립에 남은 미회수 종료 토큰을 처리하는 방법
- 토큰의 Version·Completed에 부여할 의미. 현재 Sequence가 축적하는 값은 각각 null·true이다.
- 반복 실행에서 누적 토큰을 언제 소비하거나 다시 사용할 수 있는지에 대한 계약
- 시작·정지 콜백이나 정리 자체가 실패할 때의 상태 복구와 재진입 계약

[Scp 사용 예제](Usage.md) · [StateMachines](../README.md)
