# Navigation

Navigation은 위치 형식에 의존하지 않고 노드와 구간의 연결을 관리한다.

## 목차

- 객체와 연결
- 변경과 알림
- 수명과 확장

## 객체와 연결

IReadOnlyNavNode는 연결 구간을 조회하며 IReadOnlyNavSegment는 From·To·Nodes와 Length를 조회한다. INavNode와 INavSegment는 각각 연결·해제·전체 해제를 제공한다. NavNode와 NavSegment가 기본 구현이다. NavSegment의 길이는 생성 시 지정하며, 계산 구간은 자신의 형상으로 길이를 결정한다.

하나의 구간은 From·To 각각에 최대 한 노드를 가진다. 양끝에 같은 노드를 붙이지 않는다. 이미 점유된 끝점을 다른 노드로 바꾸려면 먼저 해제해야 한다. 같은 끝점에 같은 노드를 다시 연결하는 요청은 상태를 유지한다. Node를 통한 중복 구간 등록은 ArgumentException이다. null·해제된 객체·잘못된 ConnectionSide의 연결 요청은 실패하며 기존 연결을 보존한다. 연결되지 않은 노드나 구간의 해제는 아무 변화가 없다.

Nodes는 현재 연결된 끝점만 열거한다. IsFromNode와 GetCounterNode는 해당 구간의 끝점만 받으며, 반대 끝점이 없으면 GetCounterNode는 null을 반환한다. 목록은 외부에서 직접 변경할 수 없다.

## 변경과 알림

한 번의 요청으로 양쪽 연결 상태를 함께 변경한 뒤 GeoEvents.Connect 또는 Disconnect를 발행한다. 상대 객체의 내부 상태 변경은 다시 공개 연결 API를 호출하지 않는다. 내부 연결 계약은 어셈블리 안에 숨기며, 공개 계약에 Internal 메서드나 재귀 방지 토큰을 노출하지 않는다.

| GeoEvents | 의미 |
| --- | --- |
| Connect | 연결 추가 |
| Disconnect | 연결 해제 |
| Shape | 형상·조회 결과 변경 |
| AnyConnection | Connect 또는 Disconnect |
| All | 연결과 형상 전체 |

플래그는 복합 값으로 전달될 수 있으므로 구독자는 비트 마스크로 검사한다. 외부 관찰자 예외가 발생해도 이미 완료된 연결 상태는 되돌리지 않으며 예외를 호출자에게 전달한다. Node의 전체 해제는 한 관찰자가 실패해도 나머지 연결을 정리한 뒤 예외를 모아 전달한다.

## 수명과 확장

Dispose는 자신의 연결을 해제하고 EventHub 수명을 끝낸다. 상대 노드나 구간의 소유권을 갖지 않으므로 상대 객체를 폐기하지 않는다. 중복 Dispose와 정리 요청은 안전하게 반복할 수 있다. 폐기 이후 연결 추가와 방향 조회는 ObjectDisposedException이다.

외부 구현에서 상호 연결 기능을 확장하려면 NavNode·NavSegment를 상속하고 제공된 연결 훅을 사용한다. INavNode만 별도로 구현한 객체는 어셈블리 내부 연결 계약을 구현할 수 없어 기본 NavSegment의 연결 대상이 될 수 없다. Planar 객체는 대응하는 Planar 노드·구간만 연결한다.

[상위 구성](README.md)
