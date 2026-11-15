# Configuration 스니펫

애플리케이션 ID로 Configuration을 초기화하고 설치 경로와 데이터 경로를 읽는 스니펫이다.

## 코드

[ConfigurationSnippet.cs](ConfigurationSnippet.cs)에 코드가 있다. 결과는 각 줄의 주석에 적었다.

## 내용

- 호스트는 보통 Bootstrapper.Initialize로 Configuration을 초기화한다. Bootstrapper를 쓰지 않을 때만 직접 호출한다.
- InstallationPath는 설치 경로를 지정하지 않으면 실행 파일의 기본 디렉터리이다. 지정한 경로는 존재하는 디렉터리여야 한다.
- DataPath는 문서 폴더 아래의 BlackThunder/UniEngine/Projects/애플리케이션 ID이며, 없으면 초기화할 때 만든다.
- Configuration은 프로세스당 한 번만 초기화하며, 두 번째 Initialize는 InvalidOperationException으로 거부한다.

## 관련 문서

- [Configuration 모듈](../../../docs/02.%20UniEngine/01-Infrastructure/01-M.1-Configuration/README.md)
- [예시 목록](../../README.md)
