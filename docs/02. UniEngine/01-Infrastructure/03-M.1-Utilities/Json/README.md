# JSON 변환

기반 값 형식을 명시적인 성분 객체로 읽고 쓰는 변환 모듈이다.

## 목차

- 소개
- 구성과 역할
- 대표 흐름
- 상세 문서

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 기반 값 형식의 JSON 성분 변환이다. |
| 목적 | 벡터·자세를 일관된 구조로 저장하고 누락되거나 잘못된 입력을 변환 경계에서 드러낸다. |
| 구현 | 형식별 변환기를 등록하고 숫자 성분의 공통 검증을 공유한다. |

## 구성과 역할

등록 도구가 JsonSerializerOptions와 변환기를 연결한다. 벡터·색상·쿼터니언 변환기는 성분을 처리하고, Transform 변환기는 위치·회전의 변환기를 조합한다. JsonComponentSerializer는 이름·필수 여부·숫자 범위 검증을 맡는다.

변환기는 System.Text.Json을 사용한다. 본체는 netstandard2.1을 대상으로 하며 이 환경에는 System.Text.Json이 기본으로 포함되어 있지 않으므로, System.Text.Json NuGet 패키지를 참조한다. Unity처럼 NuGet을 기본으로 쓰지 않는 환경에서는 이 패키지와 그 의존 DLL을 본체 DLL과 함께 배치해야 한다.

## 대표 흐름

1. 옵션 등록 단계에서 처리할 형식의 변환기를 준비한다.
2. 읽기에서는 성분을 검증한 뒤 값을 구성하고, 쓰기에서는 정의된 성분 이름으로 출력한다.
3. 복합 자세 값은 위치와 회전의 변환을 조합하여 같은 규칙을 적용한다.

## 상세 문서

| 객체·계약 | 역할 |
| --- | --- |
| [JsonSerializerOptionsExtensions](JsonSerializerOptionsExtensions.md) | UniEngine의 값 형식 변환기를 JsonSerializerOptions에 등록하는 정적 확장 기능이다. |
| [JsonComponentSerializer](JsonComponentSerializer.md) | JSON 성분의 이름·필수 여부·숫자 범위를 검증하는 내부 공통 직렬화 도구이다. |
| [Vector2JsonConverter](Vector2JsonConverter.md) | System.Numerics.Vector2와 성분별 JSON 객체 사이를 변환하는 변환기이다. |
| [Vector3JsonConverter](Vector3JsonConverter.md) | System.Numerics.Vector3와 성분별 JSON 객체 사이를 변환하는 변환기이다. |
| [Vector2IntJsonConverter](Vector2IntJsonConverter.md) | Vector2Int와 성분별 JSON 객체 사이를 변환하는 변환기이다. |
| [Vector3IntJsonConverter](Vector3IntJsonConverter.md) | Vector3Int와 성분별 JSON 객체 사이를 변환하는 변환기이다. |
| [QuaternionJsonConverter](QuaternionJsonConverter.md) | System.Numerics.Quaternion과 성분별 JSON 객체 사이를 변환하는 변환기이다. |
| [ColorJsonConverter](ColorJsonConverter.md) | System.Drawing.Color를 정규화된 RGBA JSON 성분으로 저장하는 변환기이다. |
| [Transform2DJsonConverter](Transform2DJsonConverter.md) | Transform2D의 위치와 회전을 명시적인 JSON 구조로 변환하는 변환기이다. |
| [Transform3DJsonConverter](Transform3DJsonConverter.md) | Transform3D의 위치와 회전을 명시적인 JSON 구조로 변환하는 변환기이다. |

[Infrastructure 구성](../../README.md)
