# Network Creator

Network Creator는 외부에서 감지한 위치·노드·구간과 Factory를 사용하여 노드와 구간을 단계적으로 생성하는 도구이다.

## 목차

- 계약과 책임
- 감지와 Factory
- Linear 생성 흐름
- 실패와 수명

## 계약과 책임

공통 `INetworkCreator<P>`는 감지 공급자, Node·Segment Factory, 생성 시작·확정·취소·종료를 제공한다. Geometry의 `IGeometricNetworkCreator<P>` 아래에 Linear와 Bezier 계약을 두고, RouteSystem의 `IRouteNetworkCreator<P>`는 공통 계약을 직접 확장한다. 위치 타입만 필요하며 회전 타입과 특정 입력 장치에 의존하지 않는다.

현재 구체 구현은 `LinearGeometricNetworkCreator<P>`이다. Linear는 직선 형상을 뜻하며 Segment Factory의 결과는 ILinearSegment여야 한다. Bezier와 Route는 분류 인터페이스만 제공한다. 제어점 편집과 원본 그래프에서의 Route 생성 흐름은 이번 구현에 포함하지 않는다.

입력 장치·히트 테스트·스냅·화면 표시와 그룹 등록은 호출자가 담당한다. Creator는 Update에서 그래프를 변경하지 않으며, TryConfirm으로 명시적으로 확정할 때만 Factory를 호출한다. 내부 USM은 Idle → Create → Anchor·Connect → Put·Select 계층에서 현재 동작을 선택한다.

## 감지와 Factory

`Detection<P>`는 Position·Node·Segment를 가진 불변 값이다. GetCurrentDetections 공급자는 한 시점의 감지 결과 하나를 반환한다. 여러 후보의 우선순위와 스냅은 공급자가 결정하며 Position에는 최종 배치 위치를 넣는다. Node와 Segment가 함께 있으면 Node 선택을 우선한다. Node가 없으면 감지 결과 전체를 Node Factory에 전달한다.

Detection은 Location을 저장하지 않는다. 구간 위의 앵커가 필요한 Factory는 Segment.TryGetLocation(Position, ...)으로 구간 내 위치를 얻는다. 구간 선택만으로 원본 구간을 분할하거나 원본 그래프에 새 연결을 추가하지 않는다. 이런 편집은 Factory와 호출자의 도메인 정책이다.

Node Factory는 감지 결과로 새 노드를 반환하고, Segment Factory는 전달받은 from·to 순서로 연결을 완료한 새 구간을 반환한다. 두 Factory의 bool은 도메인 조건에 따른 생성 가능 여부이다. 성공 시에는 살아 있는 객체가 필요하고 Linear 구간은 요청한 두 노드를 정확히 양끝으로 가져야 한다. 기존 노드의 선택은 Node Factory를 호출하지 않는다.

Factory의 out 인자로 전달한 객체는 이번 시도에서 새로 소유한 객체여야 한다. false 또는 예외를 반환하더라도 out에 전달한 임시 객체의 정리는 Creator가 맡는다. out에 전달하지 못한 할당과 기존 그래프에 가한 별도 변경의 복구는 Factory가 담당한다. 기존 객체를 Factory 결과로 재사용하거나 확정 전에 외부 그룹에 등록하지 않는다.

## Linear 생성 흐름

Begin은 감지를 한 번 읽고 Anchor 단계로 진입한다. 이미 생성 중이면 아무 변화가 없다. Update와 TryConfirm은 생성 중일 때마다 감지를 한 번 새로 읽는다. Idle에서 Update는 아무 작업을 하지 않고 TryConfirm은 false이다.

| 단계 | 감지 | 현재 상태 | 확정 결과 |
| --- | --- | --- | --- |
| Idle | 무관 | Idle | 생성 없음 |
| Anchor | Node 없음 | PutFirstNode | 새 노드를 만들고 연결 기준으로 지정 |
| Anchor | Node 있음 | SelectFirstNode | 기존 노드를 연결 기준으로 지정 |
| Connect | Node 없음 | PutNextNode | 새 노드와 직선 구간 생성 후 기준 노드 이동 |
| Connect | Node 있음 | SelectNextNode | 기존 노드에 직선 구간 생성 후 기준 노드 이동 |

TryConfirm은 성공한 결과를 `NetworkCreation<P>`로 반환한다. Node는 이번에 선택·생성한 노드이고, Segment는 이번에 생성한 구간이다. 첫 노드 확정에는 Segment가 없다. IsNewNode로 새 노드와 기존 노드 선택을 구분한다. 호출자는 이 결과를 사용하여 그룹에 등록하거나 표시한다.

Cancel은 Connect에서 현재 연결 기준을 비우고 Anchor로 돌아간다. Anchor에서 다시 취소하면 Idle이 된다. Finish는 어느 단계에서든 Idle로 돌아간다. 두 동작 모두 이미 확정한 생성 결과를 제거하지 않는다. 따라서 첫 노드를 확정한 직후 취소해도 그 노드는 호출자의 결과로 남는다.

같은 노드를 다음 노드로 선택하면 false이며 Factory를 호출하지 않는다. 연결 기준이 외부에서 폐기되면 다음 Update·TryConfirm의 감지 갱신에서 Anchor로 돌아간다. 폐기된 감지 대상의 확정은 false이다. 좌표의 유효성·위치 일치에 따른 중복 제거·겹치는 구간 허용 여부는 공간 구현과 Factory 정책을 따른다.

## 실패와 수명

Factory가 false를 반환하면 결과는 기본값이고 현재 연결 기준은 유지한다. 실패한 시도에서 반환받은 구간을 먼저 폐기하고 새 후보 노드를 정리한다. 선택한 기존 노드와 앞서 확정한 노드는 폐기하지 않는다. Factory 예외와 계약 위반은 호출자에게 전달하며 같은 임시 객체 정리를 수행한다. 정리 중 예외도 숨기지 않으며 구간 정리가 실패해도 새 노드 정리를 시도한다.

감지 공급자의 예외는 마지막 감지와 상태를 유지한다. Factory와 감지 콜백에서 동일 Creator의 편집·폐기를 다시 호출하면 InvalidOperationException이다. 여러 스레드에서 동시에 편집하는 사용은 지원하지 않는다.

성공한 생성 결과의 소유권은 호출자에게 있다. Creator의 Dispose는 자신의 생성 상태와 참조만 비우며 확정한 객체와 감지 대상은 폐기하지 않는다. 중복 Dispose는 아무 변화가 없고 폐기 이후 편집은 ObjectDisposedException이다.

[사용 예시](Usage.md) · [Geometry](Geometry.md) · [상위 구성](README.md)
