# Vector2JsonConverter

System.Numerics.Vector2와 성분별 JSON 객체 사이를 변환하는 변환기이다.

## 목차

- 역할과 구성
- 동작 흐름
- 설계 기준

## 역할과 구성

- 책임: 값 형식의 내부 표현과 저장 표현을 분리하여 성분 이름과 숫자 형식을 명확하게 유지한다.
- 보유 정보: X·Y: 읽기·쓰기에 사용할 성분 이름이다. 변환 대상 값 자체나 호출 사이의 진행 상태는 보관하지 않는다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| Read | 유한 float 성분을 검증한 뒤 값을 구성하여 불완전한 입력이 정상 값으로 들어오지 않게 한다. |
| Write | 정해진 성분 이름으로 값을 출력하여 읽기 경로와 같은 저장 형식을 사용한다. |

## 동작 흐름

[JsonSerializerOptionsExtensions](JsonSerializerOptionsExtensions.md)가 변환기를 등록한다. 읽을 때는 [JsonComponentSerializer](JsonComponentSerializer.md)에 공통 검증을 맡기고 반환된 성분으로 값을 구성한다. 쓸 때는 같은 성분 이름으로 JSON 객체를 만든다.

## 설계 기준

- 성분 누락·중복·알 수 없는 속성은 JsonException이다. 읽는 이름의 대소문자는 구분하지 않지만 출력에는 정의된 성분 이름을 사용한다.
- 저장할 값의 수학적 의미와 좌표계는 소비자가 결정한다. 이 변환기는 성분별 표현만 담당한다.

[모듈 구성](README.md)
