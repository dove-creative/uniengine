# Transform3DJsonConverter

Transform3D의 위치와 회전을 명시적인 JSON 구조로 변환하는 변환기이다.

## 목차

- 역할과 구성
- 동작 흐름
- 설계 기준

## 역할과 구성

- 책임: 복합 자세의 구조와 개별 수학 값의 성분 변환을 분리하여 같은 검증 규칙을 재사용한다.

보유 정보는 다음과 같다.

- Vector3JsonConverter: Position의 성분을 처리한다.
- QuaternionJsonConverter: Rotation의 성분을 처리한다.
- Position·Rotation 읽음 상태: 호출 중 필수 속성의 중복과 누락을 확인한다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| Read | Position·Rotation 객체를 읽어 위치와 회전이 갖춰진 Transform3D를 복원한다. |
| Write | 두 값을 각각의 성분 객체로 기록하여 자세의 저장 구조를 명시한다. |

## 동작 흐름

Position은 [Vector3JsonConverter](Vector3JsonConverter.md)에, Rotation은 [QuaternionJsonConverter](QuaternionJsonConverter.md)에 위임한다. 두 속성이 확인되면 Transform3D를 구성한다. 쓰기에서도 같은 내부 변환기를 사용한다.

## 설계 기준

- 자세 구조는 이 변환기가 확인하고 개별 숫자와 성분의 검증은 내부 변환기가 담당한다.
- 두 속성이 모두 필요하며 중복·알 수 없는 속성은 거부한다. 내부 성분 변환은 options에 등록된 다른 변환기를 다시 선택하지 않으며 회전을 자동 정규화하지 않는다.

[모듈 구성](README.md)
