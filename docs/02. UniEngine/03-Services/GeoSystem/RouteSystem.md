# RouteSystem

RouteSystem은 원본 그래프의 특정 위치와 부분 구간을 참조하고, 방향이 있는 구간 목록을 하나의 경로로 조회한다.

## 목차

- Pin과 Hold
- Clip
- SegmentView와 Route
- 통신과 수명

## Pin과 Hold

NodePin은 원본 노드의 위치를, SegmentPin은 원본 구간의 Location 위치를 따른다. 둘 다 노드이므로 별도 구간을 연결할 수 있다. Pin을 원본에 붙이는 Connect·Disconnect는 그래프 끝점의 ConnectNode·DisconnectNode와 구분한다.

Hold는 자신이 소유한 Pin을 통해 위치를 따른다. 노드와 구간 사이를 옮길 때 필요한 Pin으로 교체하며 이전 Pin은 폐기한다. 같은 종류의 앵커 이동에서는 Pin을 재사용한다. Hold는 Pin을 소유하지만 Pin이 가리키는 원본은 소유하지 않는다.

IsValid는 현재 앵커에서 유효한 위치를 얻을 수 있는지를 나타낸다. 앵커가 없거나 Location이 범위를 벗어나면 유효하지 않으며, Position에는 마지막으로 계산한 값이 남는다. 소비자는 IsValid를 함께 확인해야 한다. 앵커 또는 형상이 다시 유효해지면 갱신 알림으로 위치를 회복한다.

## Clip

Clip은 두 SegmentPin 사이를 하나의 구간으로 제공한다. 양끝 Pin이 같은 원본 구간을 가리킬 때 SegmentSource가 결정된다. Clip에 원본을 별도로 연결하는 API는 없다. 두 핀의 실제 From 기준 거리로 Clip.IsForward와 길이를 계산한다. 핀의 IsForward 값이 같거나 다른지만으로 Clip 방향을 결정하지 않는다.

Clip을 정방향으로 조회하면 자신의 From Pin에서 To Pin을 향하고, 역방향이면 To Pin에서 From Pin을 향한다. 원본에 대한 Clip 방향과 조회 방향을 조합하여 원본 위치와 회전을 구한다. 원본 불일치, 유효하지 않은 위치 또는 같은 위치의 핀은 유효하지 않은 Clip을 만든다. 핀을 옮기거나 재연결하여 회복할 수 있다. Clear는 끝점 연결을 비우며 핀과 원본을 폐기하지 않는다.

## SegmentView와 Route

SegmentView는 Source와 IsForward를 보유하는 불변 값이다. IsForward는 Source의 방향을 따를지를 나타낸다. EventHub나 별도 수명이 없으며 경로 편집 기능도 없다.

Route는 Segment 계약을 구현하고 Views를 읽기 전용 목록으로 제공한다. Append는 앞이나 뒤에 View를 추가하며, Remove는 지정한 Source가 경로의 맨 앞 또는 맨 뒤에 있을 때 한 View를 제거한다. 양쪽 모두 같은 Source이면 앞쪽을 먼저 제거한다. 경로 길이는 각 View의 길이를 합산하고, 같은 Source가 반복되어도 한 번의 원본 갱신으로 다시 계산한다.

Route의 From·To는 첫 View와 마지막 View의 방향에서 결정되며 Nodes는 원본 구간들의 노드를 중복 없이 열거한다. Route 자체를 원본 노드의 Segments 목록에 등록하지 않는다. 목록 순서와 연결 가능성은 호출자가 구성하며 Append는 인접 View의 연속성을 강제하지 않는다. 정확한 구간 경계에서는 현재 조회 방향으로 먼저 도달한 View의 회전을 반환한다.

개별 ConnectNode·DisconnectNode는 경로 탐색 구현 전까지 상태를 바꾸지 않고 NotImplementedException을 던진다. ClearNodes는 Clear와 같으며 View와 구독을 모두 해제한다. 원본 구간의 연결과 수명은 유지한다.

## 통신과 수명

각 객체는 자신이 참조하는 원본의 EventHub를 구독한다. RouteEvents는 Navigation 플래그와 같은 연결·형상 비트를 사용하며 Location 변경 비트를 추가한다. SegmentPin의 Location 편집은 Location과 Shape를 함께 알린다. 재연결 시 이전 원본 구독을 해제하며, 이후 이전 원본의 갱신이나 폐기는 영향을 주지 않는다.

원본의 폐기는 그 원본을 참조하는 Pin·Clip·Route와 Pin을 소유한 Hold의 수명에 전파된다. 반대로 참조 객체를 폐기해도 원본은 폐기하지 않는다. 원본 노드와 구간의 단순 연결 해제는 원본 폐기와 다르며, Pin·Clip은 살아 있는 상태에서 유효성을 잃었다가 회복할 수 있다.

[상위 구성](README.md)
