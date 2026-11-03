# Transform2DJsonConverter

Transform2D의 위치와 회전을 명시적인 JSON 구조로 변환하는 변환기이다.

## 목차

1. 역할과 구성
2. 동작 흐름
3. 설계 기준

## 역할과 구성

- 책임: 복합 자세의 구조를 유지하면서 위치 성분 검증과 회전 숫자 검증을 각각 알맞은 도구에 위임한다.

보유 정보는 다음과 같다.

- Vector2JsonConverter: Position의 읽기·쓰기를 담당하는 내부 변환기이다.
- Position·Rotation 읽음 상태: 호출 중 필수 속성의 중복과 누락을 확인한다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| Read | Position 객체와 라디안 float Rotation을 읽어 완전한 Transform2D를 복원한다. |
| Write | 위치와 회전을 구분하여 기록함으로써 복합 값의 저장 구조를 명시한다. |

## 동작 흐름

Position은 [Vector2JsonConverter](Vector2JsonConverter.md)에, Rotation은 [JsonComponentSerializer](JsonComponentSerializer.md)의 숫자 검증에 위임한다. 두 속성이 모두 확인되면 Transform2D를 구성한다. 쓰기에서도 같은 분담으로 속성을 출력한다.

## 설계 기준

- 자세 구조의 검증은 이 변환기가, 벡터 성분 검증은 내부 변환기가 담당한다.
- 두 속성이 모두 필요하며 중복·알 수 없는 속성은 거부한다. 내부 성분 변환은 options에 등록된 다른 변환기를 다시 선택하지 않는다.

[모듈 구성](README.md)
