# TerminationMode

마지막 구독이 해제될 때 스트림을 폐기할지 정하는 종료 정책 열거형이다.

## 목차

1. 역할과 구성
2. 동작 흐름
3. 설계 기준과 고려사항
4. 구현 연결

## 역할과 구성

- 책임: 값 전달과 별개로, 구독 수의 변화에 따른 자동 폐기 여부를 정한다.
- 값: Complete와 NoSubscribers이며 EventStreamCore 네임스페이스에 속한다.

| 값 | 마지막 구독이 해제되면 | 맞는 대상 |
| --- | --- | --- |
| Complete | 스트림을 유지한다. 종료 입력이나 명시적인 Dispose 전까지 다시 구독할 수 있다. | 소스처럼 구독자가 바뀌어도 계속 쓰는 스트림 |
| NoSubscribers | 스트림을 폐기하고, 보관한 상위 연결도 해제한다. | 연산자처럼 받을 쪽이 없으면 남아 있을 이유가 없는 스트림 |

| 대상 | 기본값 |
| --- | --- |
| 기본 EventStream, Append가 만든 스트림 | Complete |
| Debug·Skip·SkipWhile·Where·Take·TakeWhile·Merge | NoSubscribers |

생성자 인자, TerminationMode 속성, With로 바꿀 수 있다.

## 동작 흐름

1. 구독 핸들이 해제되면 스트림은 남은 구독 수를 확인한다.
2. NoSubscribers이고 남은 구독이 없으면 스스로 Dispose한다.
3. Dispose가 상위 연결 핸들을 해제하므로 상위에서도 같은 판단이 일어난다. 상위가 NoSubscribers이고 다른 구독이 없으면 함께 폐기되고, Complete이면 연결만 끊기고 남는다.

이 판단이 소스 방향으로 이어지면서, 받을 쪽이 사라진 연산자 경로가 차례로 정리된다. 강한 구독 참조와 연결 소유 관계는 [모듈 개요](README.md)의 연결 수명 절을 따른다.

## 설계 기준과 고려사항

종료 정책을 별도 값으로 두어, 기본 스트림과 연산자가 서로 다른 유지 기간을 고르게 한다.

- 두 값 모두 OnCompleted·OnError와 명시적인 Dispose에서는 폐기한다. 차이는 마지막 구독 해제를 폐기 계기로 삼는지뿐이다.
- 폐기 판단은 구독 핸들이 해제될 때만 일어난다. 따라서 NoSubscribers 스트림도 만든 직후 구독이 없다는 이유로 폐기되지 않는다. 구독이 없는 스트림을 NoSubscribers로 바꿔도 바로 폐기되지 않는다.
- 값 자체는 상태를 가지거나 폐기를 실행하지 않는다. 판단과 실행은 EventStream이 한다.
- 폐기 후 새 작업이 예외인지 무시되는지는 정책과 관계없이 같다.

## 구현 연결

[사용 예제](Usage.md)의 구독과 해제·필터 조합 절은 두 정책의 차이를 보여준다.

[소스](../../../../src/02-Core/02-E.3-EventStream/TerminationMode.cs) · [모듈 구성](README.md)
