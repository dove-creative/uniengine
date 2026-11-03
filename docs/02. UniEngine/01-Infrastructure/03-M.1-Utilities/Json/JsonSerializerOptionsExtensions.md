# JsonSerializerOptionsExtensions

UniEngine 값 형식의 JSON 변환기를 JsonSerializerOptions에 연결하는 정적 확장 기능이다.

## 목차

1. 역할과 구성
2. 동작 흐름
3. 설계 기준

## 역할과 구성

- 책임: 애플리케이션이 개별 변환기를 빠뜨리지 않고 공통 저장 형식을 사용할 수 있게 한다.
- 보유 정보: 지속 상태는 없다. 호출자가 전달한 options의 Converters 목록을 조회하고 변경한다.

| 제공 서비스 | 제공 이유 |
| --- | --- |
| AddUniEngineConverters | Vector2·3, Vector2Int·3Int, Quaternion, Color, Transform2D·3D 변환기를 한곳에서 등록하고 같은 options를 반환한다. |

## 동작 흐름

각 대상 형식을 처리할 수 있는 변환기가 이미 있는지 확인한다. 기존 변환기가 있으면 유지하고 없는 형식만 추가한 뒤, 호출자가 그 옵션을 JsonSerializer에 전달하여 읽기·쓰기를 수행한다.

## 설계 기준

- 기존 변환기를 우선하여 애플리케이션의 형식별 사용자 설정을 보존한다. null 옵션은 거부한다.
- Transform 변환기는 자신의 내부 성분 변환기를 사용하므로, 최상위 옵션에 등록한 별도 벡터 변환기를 내부에서도 자동 선택하지는 않는다.

[모듈 구성](README.md)
