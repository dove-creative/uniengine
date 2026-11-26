# UniEngine

UniEngine은 객체의 상태·수명·이벤트 흐름을 조합하여 시뮬레이션 프로그램을 만드는 C# 프레임워크이다.

호스트 프로그램이 실행 루프와 시간을 제공하고, UniEngine은 객체 간 연결, 이벤트 전달 순서, 상태 전이를 구성하는 공통 기능을 제공한다. 본체는 .NET Standard 2.1 라이브러리이며 Unity API에 의존하지 않는다.

## 개발 상태

현재 개발 중이며 공개 API와 구조가 변경될 수 있다. 객체·이벤트 기반 기능과 USM 상태 머신을 구현하고 있으며, Scp 클립 재생기는 초안 단계이다. GeoSystem은 노드와 구간의 연결, 평면 지오메트리, 경로 탐색·편집·추적을 제공한다. MEP는 설계 문서로 다루는 후속 구현 범위이다.

Unity 연동을 고려하여 C# 9와 .NET Standard 2.1을 사용한다. 현재 저장소의 실행 예제와 테스트는 .NET 환경을 대상으로 하며, Unity·Mono·IL2CPP에서의 실행 검증을 대신하지 않는다.

## 요구 사항과 빌드

저장소의 빌드·예제·테스트 명령에는 .NET 10 SDK를 사용한다. 본체의 대상은 .NET Standard 2.1이며, 예제와 테스트는 .NET 10에서 실행된다.

```powershell
git clone https://github.com/dove-creative/uniengine.git
cd uniengine
dotnet build src/UniEngine.csproj -c Release
```

명령은 저장소 최상위에서 실행한다. NuGet 복원은 빌드할 때 자동으로 수행되며, 일반 의존성은 nuget.org에서 받는다. Blackbox System·UniTest는 저장소의 packages 폴더에 포함된 배포본을 사용하므로 별도 개발 저장소가 필요하지 않다.

현재는 저장소를 받아 빌드하는 흐름을 안내한다. 본체 출력은 src/bin/Release/netstandard2.1/UniEngine.dll이다. 다른 프로젝트와 연동할 때는 DLL과 필요한 의존성을 함께 전달한다. 패키지 구성과 출처는 [패키지 안내](packages/README.md)를 참고한다.

개발에 참여할 때는 공통 작업 지침 서브모듈을 초기화한다. 본체·예제·테스트를 빌드하는 데는 필요하지 않다.

```powershell
git submodule update --init guidelines/blackthunder-conventions
```

## 예제

사용 코드는 samples 폴더에서 모듈별로 제공한다. [예제 안내](samples/README.md)에서 전체 구성과 명령 목록을 확인할 수 있다.

| 주제 | 예제 폴더 | 내용 |
| --- | --- | --- |
| 이벤트 흐름 | [EventStream](samples/02-Core/02-E.3-EventStream/) | 필터·순서·분기·병합·병렬 구독 |
| 객체 알림 | [LifecycleHub](samples/02-Core/03-E.1-LifecycleHub/) · [EventHub](samples/02-Core/03-E.2-EventHub/) | 폐기 알림, 갱신·조건 갱신·수면 |
| 연결 수명 | [HandleToken](samples/02-Core/02-E.1-HandleToken/) | 1회 정리와 실패한 정리 동작의 보고 |
| 순회 중 변경 | [AdaptiveCollections](samples/02-Core/02-E.2-AdaptiveCollections/) | 순회 중 컬렉션·콜백 목록 변경 |
| 외부 페이로드 | [Property](samples/02-Core/02-O.1-Property/) | 도메인 객체에 외부 정보 연결 |
| 상태 머신 | [Usm](samples/03-Services/StateMachines/Usm/) | FSM·행동 트리·병렬 노드 실행 |
| 호스트 초기화 | [Bootstrapper](samples/02-Core/04-Bootstrapper/) | 공통 환경·기본 틱·종료 스니펫 |
| 수학과 값 형식 | [Math](samples/01-Infrastructure/01-MT.1-Math/) · [VectorFunctions](samples/01-Infrastructure/01-MT.2-VectorFunctions/) · [Objects](samples/01-Infrastructure/02-O.1-Objects/) | 근사 비교·회전·곡선·변환 스니펫 |
| 설정과 진단 | [Configuration](samples/01-Infrastructure/01-M.1-Configuration/) · [ExceptionHandler](samples/01-Infrastructure/03-M.2-ExceptionHandler/) · [LogSystem](samples/01-Infrastructure/03-M.3-LogSystem/) | 설정·예외·로그 스니펫 |
| 공통 유틸리티 | [Infrastructure Utilities](samples/01-Infrastructure/03-M.1-Utilities/) · [Core Utilities](samples/02-Core/01-O.1-Utilities/) · [Core Objects](samples/02-Core/01-O.3-Objects/) | JSON·표시 문자열·비트 연산·순회 스니펫 |

대표 이벤트 흐름을 한 번 실행하거나 대화형 프롬프트를 열 수 있다.

```powershell
dotnet run --project samples/UniEngine.Samples.csproj -p:RestoreConfigFile=samples/NuGet.config -c Release -- event-stream
dotnet run --project samples/UniEngine.Samples.csproj -p:RestoreConfigFile=samples/NuGet.config -c Release
```

결과는 콘솔에 출력된다. 대화형 프롬프트에서 help로 명령을 확인하고 exit로 종료한다. 예제 초기화 시 문서 폴더 아래에 BlackThunder/UniEngine/Projects/UniEngine.Samples 디렉터리가 생성된다.

## 문서

- [개요와 개발 원칙](docs/01.%20Concepts/01.%20Overview.md): 목적과 설계 원칙
- [구현 문서](docs/02.%20UniEngine/README.md): Infrastructure·Core·Services의 구성과 상세 문서
- [실행 예제](samples/README.md): 모듈별 실행 명령과 코드 스니펫
- [작업 지침](AGENTS.md): 기여 시 확인할 문서와 검증 기준

## 테스트

.NET 10 SDK로 테스트 프로젝트를 실행한다.

```powershell
dotnet test tests/UniEngine.Tests.csproj -c Release
```

테스트 구성, 영향 범위에 따른 실행 기준과 검증 기록은 [테스트 안내](tests/README.md)를 참고한다.

## 의존 프로젝트

다음 패키지와 개발 지침을 참조한다.

| 프로젝트                                                                                              | 사용 범위 | 역할                                      |
| ------------------------------------------------------------------------------------------------- | ----- | --------------------------------------- |
| [BlackThunder Conventions](https://github.com/dove-creative/blackthunder-conventions)             | 개발 지침 | 코드·문서 컨벤션과 작업 절차를 제공한다.                 |
| [Blackbox System](https://github.com/dove-creative/blackbox-system) · BlackThunder.BlackboxSystem | 본체    | 실행 중 발생한 활동과 흐름을 기록·추적한다.               |
| [UniTest](https://github.com/dove-creative/unitest) · BlackThunder.UniTest                        | 테스트   | 상태 전이와 기대 결과를 구성하고, 여러 단계의 동작을 재현·검증한다. |

Blackbox System과 UniTest는 packages 폴더의 로컬 NuGet 피드에서 복원한다. 채택 버전과 배포본의 출처는 [패키지 안내](packages/README.md)에 정리되어 있다.

BlackThunder Conventions는 guidelines/blackthunder-conventions 경로의 Git 서브모듈로 참조한다.

## 라이선스

이 프로젝트는 MIT 라이선스로 배포된다. 자세한 내용은 [LICENSE.md](LICENSE.md)를 참고한다.
