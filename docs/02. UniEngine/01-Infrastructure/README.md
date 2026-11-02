# Infrastructure

상위 계층이 공통으로 사용하는 설정·값 표현·진단 기능을 제공하는 기반 계층이다.

## 목차

1. 소개
2. 구성과 역할
3. 대표 흐름
4. 상세 문서

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 애플리케이션 경로, 공간 값, 표시·직렬화, 예외 처리와 로그를 제공한다. |
| 목적 | 각 도메인 기능이 실행 기반과 진단 도구를 반복해서 구성하지 않게 한다. |
| 구현 | 값 표현과 공통 서비스를 분리하고 필요한 모듈이 서로의 결과를 사용하게 한다. |

## 구성과 역할

Configuration이 데이터 저장 위치를 결정하고 Objects는 좌표·자세를 값으로 표현한다. Utilities는 값의 표시와 직렬화를 맡는다. 이 기반 위에서 Log가 기록을 저장하며 ExceptionHandler는 애플리케이션의 예외 처리 방침을 연결한다.

데이터의 의미와 객체 수명은 소비자가 정한다. Infrastructure는 위치·표현·진단을 제공하며 도메인 객체의 활동을 소유하지 않는다.

## 대표 흐름

1. 애플리케이션이 Configuration으로 저장 위치를 준비하고 Log의 출력기를 설정한다.
2. 각 모듈은 값 형식으로 데이터를 전달하고 Utilities로 표시·저장 표현을 만든다.
3. 진단 메시지는 Log로 기록하고 처리하지 못한 예외는 ExceptionHandler에 보고한다.

숫자·시간 표시는 문화권에 영향받지 않는 형식을 사용한다. 각도 기준은 [기본 단위](../../01.%20Concepts/05.%20단위.md)의 라디안이다.

## 상세 문서

| 모듈 | 설명 대상 |
| --- | --- |
| [Configuration](01-M-Configuration/README.md) | 애플리케이션 데이터 경로 |
| [Objects](02-O-Objects/README.md) | Vector2Int·Vector3Int·Transform2D·Transform3D |
| [Utilities](03-M.1-Utilities/README.md) | 표시와 JSON 변환 |
| [Exception Handler](03-M.2-ExceptionHandler/README.md) | 공통 예외 보고 |
| [Log System](03-M.3-LogSystem/README.md) | 메시지 출력과 파일 기록 |

[전체 계층](../README.md)
