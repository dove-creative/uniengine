# Spatial System 사용 예시

애플리케이션 조립부만 엔진을 선택한다. 소비자는 CollisionSpace로 형상을 만들고 자세·질의·알림을 사용한다. PhysicalSpace와 공개 Rigidbody 기능은 미구현이다. 아래 예제는 실행 검증과 구분하며 현재 상태는 [테스트 결과](../../../../tests/01-Infrastructure/04-T-SpatialSystem/docs/03.%20Results.md)를 따른다.

## 공통 소비자

```csharp
using System.Numerics;
using BlackThunder.UniEngine.SpatialSystem;

public class ContactSensor
{
    public BoxCollider3D Shape { get; }

    public ContactSensor(CollisionSpace3D world)
    {
        Shape = world.CreateBoxCollider(Vector3.One);
        Shape.IsTrigger = true;
    }
}
```

생성 pose는 월드 자세이며 생략하면 원점·기본 회전이다. 형상과 공간은 강체 연결 인자를 받지 않는다. 소비자는 Collider.Pose를 바꿔 위치와 회전을 지정한다. Enabled·IsTrigger의 마지막 예약값은 다음 Update에서 native 객체에 적용되며, 알림 콜백에서 바꾼 값은 그다음 Update의 대상이다.

## Unity 조립

```csharp
#if UNITY_2022_3_OR_NEWER
using BlackThunder.UniEngine.Objects;
using BlackThunder.UniEngine.SpatialSystem;
using BlackThunder.UniEngine.SpatialSystem.Unity;
using N = System.Numerics;
using U = UnityEngine;

public class SpatialComposition : U.MonoBehaviour
{
    private CollisionSpace3D _world;
    private ContactSensor _sensor;

    private void Awake()
    {
        _world = new CollisionSpace3DUnity();
        _sensor = new ContactSensor(_world);
        _world.CreateBoxCollider(N.Vector3.One * 2f,
            new Transform3D(new N.Vector3(3f, 0f, 0f), N.Quaternion.Identity));
        _sensor.Shape.TriggerEnter += pair => U.Debug.Log("Sensor entered another collider.");
    }

    private void FixedUpdate()
    {
        var position = transform.position;
        var rotation = transform.rotation;
        _sensor.Shape.Pose = new Transform3D(
            new N.Vector3(position.x, position.y, position.z),
            new N.Quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
        _world.Update(U.Time.fixedDeltaTime);
    }

    private void OnDestroy() => _world?.Dispose();
}
#endif
```

이 MonoBehaviour는 애플리케이션 루프와 게임 객체의 자세를 연결하는 예제이다. 기존 게임 루프에서 생성·Update·Dispose를 호출해도 된다. Collider마다 OnTrigger·OnCollision 중계를 직접 붙이지 않으며 연결 코드가 자동 구성한다.

새 런타임 씬에는 물리 객체만 있다. 게임의 화면 객체는 원래 씬에 두고 필요한 자세를 공통 Collider에 전달한다. Update 간격은 Time.fixedDeltaTime으로 고정되지 않으며 호출자가 양의 유한 초를 정한다. 내부 고정 강체가 있더라도 중력에 따른 낙하나 공개 운동 기능을 제공하지 않는다.

## 독립 월드와 질의

Unity 조립부의 실행 중 메서드에서 다음과 같이 두 월드를 만들 수 있다.

```csharp
using var first = new CollisionSpace2DUnity();
using var second = new CollisionSpace2DUnity();
first.CreateCircleCollider(1f);
second.CreateCircleCollider(2f);
first.SetLayerCollision(2, 7, false);
first.Update(0.02f);
second.Update(0.01f);
var hits = first.OverlapCircle(System.Numerics.Vector2.Zero, 3f);
```

두 공간의 씬·객체·레이어 정책·명시적인 시간 진행은 분리된다. 질의는 해당 월드만 검색하며 시간을 진행하지 않는다. using 범위가 끝나면 각 월드를 해제한다.

접촉·Trigger 알림에는 양쪽 공통 Collider 핸들이 포함된다. Unity에서는 Update의 native 단계가 반환한 뒤 전달하며, 콜백에서 같은 월드를 다시 Update하는 것은 거부한다. 알림 전이의 남은 검토 항목은 [이슈 기록](ISSUES.md)을 따른다.

## Unity 고유 형상

```csharp
var polygon = (PolygonCollider2DUnity)binding.CreateUnityCollider<UnityEngine.PolygonCollider2D>();
polygon.Source.SetPath(0, new[]
{
    new UnityEngine.Vector2(-1f, -1f),
    new UnityEngine.Vector2(1f, -1f),
    new UnityEngine.Vector2(0f, 1f),
});
binding.Update(0.02f);
```

binding은 조립부가 가진 CollisionSpace2DUnity이다. 시스템의 생성 API로 만든 형상에서 Source를 사용해 복잡한 형상 데이터를 편집한다. 개발자가 별도로 만든 객체를 월드에 넣거나 Wrap으로 등록하는 것은 오사용이다. 월드의 씬·객체 소속·시뮬레이션은 직접 조작하지 않는다.

자세는 월드, 형상 값은 로컬, 각도는 도 기준이다. Transform 변경과 질의의 관측 시점, 복잡한 형상 및 native 알림 제약은 [Engine Binding](04.%20Engine%20Binding.md)을 따른다.

## 수명

보통 조립부가 월드의 수명을 관리하고 마지막에 Dispose한다. 소속 Collider 핸들이 먼저 해제되고 월드 Disposed가 한 번 발생한다. Unity 씬의 비동기 언로드 완료를 기다리는 이벤트는 아니다.

씬 전환 중 월드 유지는 지원하지 않는다. Unity에서 Single로 씬을 로드하면 월드의 씬도 내려가 월드가 해제되므로, 씬을 전환한 뒤 새 월드를 만든다.

Collider를 개별 Dispose하면 해당 물리 객체를 제거한다. 외부 Destroy는 실제 소멸 이후 다음 핸들 접근이나 명시적 Update에서 발견하며 매 프레임 감시하지 않는다.

[Spatial System](README.md)
