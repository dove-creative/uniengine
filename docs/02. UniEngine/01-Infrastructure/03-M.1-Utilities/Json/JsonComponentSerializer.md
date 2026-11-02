# JsonComponentSerializer

JSON 성분의 이름·필수 여부·숫자 범위를 검증하는 내부 공통 직렬화 도구이다.

## 목차

1. 역할과 구성
2. 동작 흐름
3. 설계 기준
4. 구현 연결

## 역할과 구성

- 책임: 값 형식별 변환기에 중복되는 입력 검증을 모아 같은 형식 오류를 같은 기준으로 처리한다.

보유 정보는 다음과 같다.

- 성분 이름 배열: 해당 값 형식이 요구하는 속성 목록이다.
- 호출별 Span과 읽음 표시: 숫자 성분과 중복·누락 여부를 처리한다. 지속적인 읽기 상태를 보관하지 않는다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| ReadSingleComponents·ReadInt32Components | 성분 객체를 검증하면서 읽어 개별 변환기가 값 구성에 집중할 수 있게 한다. |
| ReadSingle·WriteSingle | float 성분의 유한성 검사를 공유하여 숫자 읽기·쓰기의 기준을 맞춘다. |

## 동작 흐름

변환기가 성분 이름과 임시 값 공간을 전달한다. JsonComponentSerializer가 속성명을 대소문자 구분 없이 대응시키고 각 숫자를 채운다. 객체 끝에서 필수 성분을 확인한 뒤 변환기가 그 값들로 벡터·색상 등을 구성한다.

## 설계 기준

- 일부 값만 읽힌 객체를 정상 값으로 취급하지 않도록 누락·중복·알 수 없는 이름을 거부한다.
- float의 NaN·무한대는 허용하지 않으며 정수는 Int32 범위를 따른다. 이 도구는 내부 부품이고 공개 등록 경로는 JsonSerializerOptionsExtensions이다.
- 변환기는 임시 값 공간으로 성분 수만큼의 작은 배열을 만들어 넘긴다. C# 9의 참조 안전 규칙에서는 stackalloc으로 만든 Span을 ref Utf8JsonReader와 같은 호출에 넘길 수 없기 때문이다.

## 구현 연결

[소스](../../../../../src/01-Infrastructure/03-M.1-Utilities/Json/JsonComponentSerializer.cs) · [모듈 구성](README.md)
