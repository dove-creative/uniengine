# ColorJsonConverter

System.Drawing.Color를 정규화된 RGBA JSON 성분으로 저장하는 변환기이다.

## 목차

1. 역할과 구성
2. 동작 흐름
3. 설계 기준
4. 구현 연결

## 역할과 구성

- 책임: byte 기반 색상과 0~1 범위의 저장 성분을 연결하여 색상 표현의 범위를 명확하게 한다.
- 보유 정보: R·G·B·A: 색상과 알파에 대응하는 성분 이름이다. 변환 중인 색상은 호출자가 전달한다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| Read | 0~1 범위의 유한 숫자를 byte 성분으로 환산하여 Color 값을 복원한다. |
| Write | byte 성분을 255로 나눈 값으로 기록하여 정규화된 저장 표현을 제공한다. |

## 동작 흐름

[JsonComponentSerializer](JsonComponentSerializer.md)가 네 성분을 읽고 검증한다. ColorJsonConverter는 추가로 0~1 범위를 확인하고 반올림하여 Color.FromArgb로 값을 구성한다. 쓰기에서는 각 byte 성분을 정규화하여 같은 이름으로 기록한다.

## 설계 기준

- 범위 검사는 색상 변환기가 담당하고 공통 도구는 성분 존재 여부와 숫자 형식만 담당한다. 이를 통해 다른 숫자 기반 변환기에 색상 범위를 강제하지 않는다.
- 네 성분이 모두 필요하다. 임의 실수 입력은 byte 정밀도로 양자화되므로 같은 실수값으로 완전히 왕복하지 않을 수 있다.

## 구현 연결

[소스](../../../../../src/01-Infrastructure/03-M.1-Utilities/Json/ColorJsonConverter.cs) · [모듈 구성](README.md)
