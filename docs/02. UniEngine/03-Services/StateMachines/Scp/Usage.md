# Scp 사용 예제

문이 잠금 해제 결과를 받으면 열림 동작과 효과음을 함께 시작하고, 둘 다 끝난 뒤 통과를 허용하는 연출이다. 잠금 해제가 거부되면 안내 클립만 재생한다.

## 목차

- 클립과 조건 연결
- 시간 전달과 완료 확인

## 클립과 조건 연결

다음 두 코드 블록은 같은 C# 콘솔 프로그램에서 이어서 실행한다. accessGranted는 호스트가 내린 잠금 해제 판단이며, 이 예제에서는 true로 시작한다.

```csharp
using System;
using System.Linq;
using BlackThunder.UniEngine.StateMachines.Scp;

bool accessGranted = true;
var unlock = new Clip("unlock")
    .OnUpdated((clip, _) => clip.Stop(accessGranted ? "granted" : "denied"));

var motion = new Delay("open door", 0.5f);
var chime = new Clip("chime").OnUpdated((clip, _) => clip.Stop());
var enter = new Clip("enter")
    .OnStarted((_, tokens) => Console.WriteLine($"Door open ({tokens.Count()} clips finished)"))
    .OnUpdated((clip, _) => clip.Stop());
var denied = new Clip("denied")
    .OnStarted((_, _) => Console.WriteLine("Access denied"))
    .OnUpdated((clip, _) => clip.Stop());

var sequence = new Sequence()
    .Add(unlock)
    .Add(new CTC(unlock).WithPayload("granted"), motion)
    .Add(new CTC(unlock).WithPayload("granted"), chime)
    .Add(new PlayCondition(motion, chime), enter)
    .Add(new CTC(unlock).WithPayload("denied"), denied);
```

unlock만 시작 클립이다. 같은 'granted' 토큰을 두 조건이 참조하므로 motion과 chime이 함께 시작된다. enter의 PlayCondition은 기본값 All을 사용하며, 두 클립의 토큰이 모두 모인 시점에 시작 콜백이 호출된다.

먼저 끝난 쪽의 토큰 하나로도 시작하려면 `new PlayCondition(motion, chime).SetCheckType(CheckType.Any)`를 사용한다. 이때 enter까지 끝난 뒤 나머지 선행 클립이 끝나면 조건이 다시 충족되어 enter가 한 번 더 시작될 수 있다.

## 시간 전달과 완료 확인

호스트가 일정한 0.25초 간격으로 갱신한다고 가정한다. 게임 루프에서는 각 갱신에 실제 경과 시간을 넘긴다.

```csharp
bool succeeded;
while (sequence.Update(0.25f, out succeeded))
{
    // Advance one host tick per call.
}
Console.WriteLine($"Completed = {succeeded}");
```

| Update 호출 | accessGranted가 true일 때 일어나는 일 |
| --- | --- |
| 1 | 시퀀스가 자동으로 시작된다. unlock이 'granted'를 반환하고 motion과 chime이 시작된다. |
| 2 | motion은 0.25초를 더 기다려야 하고 chime은 끝난다. enter는 아직 시작하지 않는다. |
| 3 | motion이 끝나 두 토큰이 모인다. enter가 시작되며 'Door open (2 clips finished)'를 출력한다. |
| 4 | enter가 첫 갱신에서 끝난다. 이 호출은 true를 반환한다. |
| 5 | 실행 중인 클립이 없어 false를 반환한다. 반복문을 빠져나오며 'Completed = True'를 출력한다. |

accessGranted를 false로 바꿔 처음부터 실행하면 첫 호출에서 'Access denied'를 출력한다. 둘째 호출에서 denied가 끝나고 셋째 호출에서 시퀀스가 정상 완료된다. 여기서 succeeded는 **시퀀스의 재생 완료**를 뜻한다. 잠금 해제 성공 여부는 unlock의 Payload에 담긴 별개의 결과이다.

[Scp](README.md) · [StateMachines](../README.md)
