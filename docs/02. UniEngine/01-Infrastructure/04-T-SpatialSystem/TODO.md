# Spatial System TODO

지금은 지원하지 않고, 필요해지면 만들 기능의 목록이다. 미해결 문제는 [ISSUES](ISSUES.md)에, 후속 구현 범위는 [Physical System](03.2.%20Physical%20System.md)에 둔다.

## 목차

- Composite 형상(2D)

## Composite 형상(2D)

| 항목 | 내용 |
| --- | --- |
| 무엇인가 | Unity 2D에서 여러 Collider를 하나의 형상으로 합치는 Collider이다. [자체 형상이 없으며](https://docs.unity3d.com/2022.3/Documentation/Manual/class-CompositeCollider2D.html) Box와 Polygon을 합쳐 형상을 만든다. |
| 현재 동작 | `CreateUnityCollider<CompositeCollider2D>()`를 NotSupportedException으로 거부한다. |
| 지원하지 않는 이유 | 합칠 Collider를 넣는 API가 없고, 개발자가 자식 Collider를 직접 붙이는 것은 [Rules](02.%20Rules.md)에서 오사용이다. 만들어도 비어 있으므로 사용할 수 없다. |
| 대신 사용할 것 | 끊어진 여러 부분을 가진 형상은 PolygonCollider2D의 [여러 경로](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PolygonCollider2D-pathCount.html)로 만든다. 경로를 지정하는 방법은 [Usage](Usage.md)의 Unity 고유 형상 절을 따른다. |
| 만들 조건 | 기본 형상과 Polygon으로 표현하기 어려운, 합친 형상이 필요해질 때이다. |

만들 때 다음을 정한다.

- 합칠 형상을 추가하고 빼는 API
- 합쳐진 형상을 각각의 Collider 핸들로 다룰지, Composite 핸들 하나로 다룰지
- 질의와 알림이 가리킬 핸들
- 활성 상태의 연결. CompositeCollider2D는 [enabled 변경을 무시하므로](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/CompositeCollider2D.html) Rigidbody2D의 simulated에 연결하는 방법을 검토한다. 합칠 Collider는 Composite와 같은 Rigidbody2D에 붙어 있어야 한다.
- Unity 실행으로 확인할 동작

[Spatial System](README.md) · [ISSUES](ISSUES.md)
