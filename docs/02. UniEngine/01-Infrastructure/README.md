# Infrastructure

상위 계층이 공통으로 사용하는 설정·수학·값 표현·진단 기능을 제공하는 기반 계층이다.

## 목차

- 소개
- 구성과 역할
- 대표 흐름
- 상세 문서

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 애플리케이션 경로, 수학 계산, 공간 값, 표시·직렬화, 예외 처리와 로그를 제공한다. |
| 목적 | 각 도메인 기능이 실행 기반과 진단 도구를 반복해서 구성하지 않게 한다. |
| 구현 | 값 표현과 공통 서비스를 분리하고 필요한 모듈이 서로의 결과를 사용하게 한다. |

## 구성과 역할

Configuration·Math·VectorFunctions는 같은 레벨이다. Configuration은 데이터 저장 위치를 결정하고 Math는 비교·보정, VectorFunctions는 매개변수에 따른 위치 계산을 제공한다. 세 모듈은 서로 의존하지 않으며 수학 타입은 BlackThunder.UniEngine.Mathematics 네임스페이스에 속한다.

다음 레벨의 Objects는 좌표·자세를 값으로 표현하고 Transform의 Is가 Math를 사용하여 위치와 회전을 비교한다. Utilities는 값의 표시와 직렬화를 맡는다. 이 기반 위에서 Log가 기록을 저장하며 ExceptionHandler는 애플리케이션의 예외 처리 방침을 연결한다.

데이터의 의미와 객체 수명은 소비자가 정한다. Infrastructure는 위치·표현·진단을 제공하며 도메인 객체의 활동을 소유하지 않는다.

## 대표 흐름

1. 애플리케이션이 Configuration으로 저장 위치를 준비하고 Log의 출력기를 설정한다.
2. 각 모듈은 값 형식으로 데이터를 전달하고 Utilities로 표시·저장 표현을 만든다.
3. 진단 메시지는 Log로 기록하고 처리하지 못한 예외는 ExceptionHandler에 보고한다.

숫자·시간 표시는 문화권에 영향받지 않는 형식을 사용한다. 각도 기준은 [수학 기준](../../01.%20Concepts/05.%20수학.md)의 라디안이다. 근사 비교와 예외 처리도 같은 기준을 따른다.

## 상세 문서

| 모듈 | 설명 대상 |
| --- | --- |
| [Configuration](01-M.1-Configuration/README.md) | 애플리케이션 데이터 경로 |
| [Math](01-MT.1-Math/README.md) | 수치·벡터·회전의 비교와 보정 |
| [VectorFunctions](01-MT.2-VectorFunctions/README.md) | 직선·이차 베지어 위치 함수 |
| [Objects](02-O.1-Objects/README.md) | Vector2Int·Vector3Int·Transform2D·Transform3D |
| [Utilities](03-M.1-Utilities/README.md) | 표시와 JSON 변환 |
| [Exception Handler](03-M.2-ExceptionHandler/README.md) | 공통 예외 보고 |
| [Log System](03-M.3-LogSystem/README.md) | 메시지 출력과 파일 기록 |

[전체 계층](../README.md)
