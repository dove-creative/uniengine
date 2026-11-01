# Property

## 소개

Property는 객체에 외부 시스템의 부가 데이터를 연결하는 키·값 저장 장치이다.

| 항목 | 설명 |
| --- | --- |
| 개요 | 객체 키에 서로 다른 형식의 값을 보관하는 외부 페이로드 저장 장치이다. |
| 목적 | 대상 객체에 기능별 필드를 추가하지 않고 외부 데이터를 연결한다. |
| 구현 | 내부 Dictionary에 데이터를 보관하며, 형식 지정 조회와 조회 후 삭제 기능을 제공한다. |

## 구성

- Property: 데이터의 등록·조회·갱신·삭제를 담당한다. `IDictionary<object, object>` 계약을 구현한다.
- IPropertySubject: 외부에서 접근할 Property 저장 공간을 제공하는 객체 계약이다.

두 형식은 UniEngine Core Level 2에 속하며, 네임스페이스는 `Blackthunder.UniEngine.Core`이다.

## 동작 규칙

| 동작 | 규칙 |
| --- | --- |
| Add | 새 키를 등록하며, 같은 키가 이미 있으면 ArgumentException을 발생시킨다. |
| 인덱서 대입 | 키가 없으면 등록하고, 이미 있으면 값을 갱신한다. |
| Get | 키가 없으면 KeyNotFoundException을 발생시킨다. |
| TryGet | 키가 없으면 false와 기본값을 반환한다. 키가 있으면 저장된 값을 조회한다. |
| 형식 지정 조회 | 저장된 값이 요청한 형식에 맞지 않으면 InvalidCastException을 발생시킨다. |
| null 조회 | 참조 형식이나 nullable 값 형식으로 조회하면 null을 반환한다. null을 받을 수 없는 값 형식이면 InvalidCastException을 발생시킨다. |
| Remove(key) | 키가 일치하는 항목을 삭제한다. |
| 키·값 쌍 삭제 | ICollection 계약을 통해 전달된 키와 값이 모두 일치할 때만 삭제한다. |
| GetAndRemove | 조회에 성공한 뒤 항목을 삭제한다. 조회에 실패하면 항목을 유지한다. |

null도 저장된 값이다. null을 받을 수 있는 형식으로 TryGet에 성공하면 true와 null을 반환하므로, 키가 없는 경우와 구분된다. ContainsKey는 저장값과 관계없이 키의 존재 여부를 확인한다.

참조 형식의 nullable 표기는 런타임 형식을 바꾸지 않는다. 따라서 string과 string?을 구분하지 않고, 두 경우 모두 저장된 null을 반환한다.

저장 공간에서 항목을 제거하거나 Clear를 호출해도 저장된 객체의 Dispose를 호출하지 않는다. 외부 데이터의 생애주기는 해당 데이터를 관리하는 쪽에서 담당한다.
