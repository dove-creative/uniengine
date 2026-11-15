# Bootstrapper 스니펫

호스트가 Bootstrapper로 실행 환경을 초기화하고, EventStream.Main으로 기본 틱을 발행한 뒤 종료하는 스니펫이다.

## 코드

[BootstrapperSnippet.cs](BootstrapperSnippet.cs)에 코드가 있다. 결과는 각 줄의 주석에 적었다.

## 내용

- Initialize는 호스트 루프를 시작하기 전에 프로세스당 한 번 호출한다. Configuration·ExceptionHandler·Log·EventStream.Main을 함께 준비한다.
- 호스트는 프레임마다 경과 시간을 초 단위로 계산하여 EventStream.Main으로 발행하고, 종료할 때 Main을 폐기한다.
- 두 번째 Initialize는 첫 시도의 성공 여부와 관계없이 InvalidOperationException으로 거부한다.
- initialize 콜백과 설치 경로를 지정하는 초기화는 Bootstrapper 사용 예제에서 확인한다.
- 실행 가능한 샘플은 [SampleEnvironment](../../SampleEnvironment.cs)에서 같은 방식으로 초기화한다.

## 관련 문서

- [Bootstrapper 모듈](../../../docs/02.%20UniEngine/02-Core/04-Bootstrapper/README.md)
- [Bootstrapper 사용 예제](../../../docs/02.%20UniEngine/02-Core/04-Bootstrapper/Usage.md)
- [예시 목록](../../README.md)
