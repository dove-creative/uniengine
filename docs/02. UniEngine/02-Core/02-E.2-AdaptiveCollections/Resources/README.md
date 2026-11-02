# Adaptive Collections 그림

현재 순회 구현의 정책·구조·상태 변화를 설명하는 SVG와 편집 가능한 drawio 원본이다.

## 목차

1. 구성
2. 편집

## 구성

| 그림 | 설명 | 연결 문서 |
| --- | --- | --- |
| [ChangePolicies.svg](ChangePolicies.svg) | 지연·즉시 추가와 삭제의 방문 정책 | [모듈 개요](../README.md) |
| [EnumerationContract.svg](EnumerationContract.svg) | ValueEnumerator·IEnumerationSource와 계약을 구현하는 세 컬렉션의 역할 | [모듈 개요](../README.md) |
| [DeferredAddition.svg](DeferredAddition.svg) | 추가 대기 목록과 종료 시 반영 | [AdaptiveCollection](../AdaptiveCollection.md) |
| [RemovalIndexAdjustment.svg](RemovalIndexAdjustment.svg) | 이전·현재·다음 항목 삭제와 인덱스 보정 | [AdaptiveCollection](../AdaptiveCollection.md) |
| [PendingBatchIteration.svg](PendingBatchIteration.svg) | 추가·제거 기록과 즉시 모드의 후속 순회 | [AdaptiveDictionary](../AdaptiveDictionary.md) · [AdaptiveHashSet](../AdaptiveHashSet.md) |
| [ActionSelfRemoval.svg](ActionSelfRemoval.svg) | 현재 등록 항목 제거 후 남은 콜백 처리 | [AdaptiveAction](../AdaptiveAction.md) |

## 편집

[AdaptiveCollections.drawio](AdaptiveCollections.drawio)는 여섯 그림을 페이지별로 보관하는 편집 원본이다. 상자·글자·화살표는 각각 편집 가능한 도형으로 구성되어 있다. 수정할 페이지를 편집한 뒤 해당 SVG 파일명으로 내보내어 문서의 그림을 갱신한다.

그림은 맑은 고딕을 기준으로 작성했으며 SVG에는 Noto Sans KR과 sans-serif 대체 글꼴도 지정되어 있다. 내보낸 그림의 한글, 줄바꿈, 화살표와 항목 배치를 확인한다. 삭제 위치 비교의 각 행과 정책 비교의 추가·삭제는 독립된 예시이며, 사전·집합 그림의 방문 순서는 보장을 의미하지 않는다.
