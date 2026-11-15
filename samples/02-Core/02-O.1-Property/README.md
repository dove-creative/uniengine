# Property 예시

도메인 객체에 필드를 추가하지 않고 외부 기능의 데이터를 키별로 보관하는 예시이다.

## 실행

```powershell
dotnet run --project samples/UniEngine.Samples.csproj -- property
```

## 내용

- Unit은 Property를 소유하고 IPropertySubject로 노출한다.
- 외부 기능은 자신의 키 객체로 데이터를 등록하고, Get으로 형식을 지정해 읽는다.
- Add는 같은 키를 거부하고, 인덱서는 기존 값을 교체한다.
- GetAndRemove는 한 번 쓸 값을 꺼내며 저장소에서 제거한다.
- Property를 bool로 변환하면 저장한 값이 있는지 알 수 있다.

## 출력

```text
Knight has payloads: False
Selected: True, payloads: 2
Adding the same key again is rejected (expected).
Selected after replacement: False
Path taken: Gate -> Bridge, still stored: False
TryGet after removal: False
```

## 관련 문서

- [Property 모듈](../../../docs/02.%20UniEngine/02-Core/02-O.1-Property/README.md)
- [예시 목록](../../README.md)
