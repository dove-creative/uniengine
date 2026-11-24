# PhysicalSpace

PhysicalSpace는 2D·3D 물리를 하나의 C# API로 제공하는 UniEngine의 Infrastructure 모듈이다. 공개 핸들이 물리 소스별 전략을 사용한다. 유니티 물리를 연결하는 전략과 공통 API를 정의하며, 유니티 밖의 물리 소스는 미정이다. 이 문서는 공개 API의 구성과 계약, 물리 소스별 책임을 정의한다. C# API와 예시는 설계 단계이며 구현 완료를 뜻하지 않는다.

## 목차

- 소개
- 구성과 의존
- 공개 핸들과 전략
- 공통 기준
- 객체와 이름
- 도형과 단위
- 갱신과 조회
- 이벤트
- Dispose와 소유권
- 물리 소스별 동작
- 공개 API 초안
- 대표 흐름
- 검토 기준

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | 물리 소스별 전략을 통해 2D·3D 물리를 제공하는 모듈이다. |
| 목적 | 도메인 코드를 물리 엔진에서 분리하고 공통 API와 물리 소스의 책임을 나눈다. |
| 구현 | 공개 핸들과 전략 인터페이스를 UniEngine.dll에 두고, 물리 소스별 전략이 실제 물리 객체를 다룬다. 현재 C# 구현은 후속 단계이다. |

공통 API의 범위와 물리 소스별 대응은 [API Coverage](API%20Coverage.md)에 기록한다. 구조를 정한 이유는 [구조 결정](DLL%20Structure%20Decision.md)에 있다.

## 구성과 의존

```text
도메인
  ↓
UniEngine.dll  01-Infrastructure/03-M.4-PhysicalSpace
    PhysicalSpace2D, UniRigidbody2D, UniCollider2D …     공개 핸들
        └ 전략 인터페이스
            └ 유니티용 전략 → 유니티 물리 (유니티가 컴파일하는 소스)
```

| 코드 | 알고 있는 대상 | 컴파일하는 쪽 |
| --- | --- | --- |
| 공개 핸들, 전략 인터페이스 | 물리 소스를 모름 | 일반 dotnet 빌드, UniEngine.dll |
| 유니티용 전략 | 전략 인터페이스, UnityEngine | 유니티 프로젝트 |
| 도메인 | 공개 핸들 | 각 환경 |

PhysicalSpace는 Infrastructure의 3단계 모듈이며 2단계 Objects와 1단계 Math를 사용한다. Core와 Services에는 의존하지 않고, Core와 Services는 공개 핸들을 사용할 수 있다. 네임스페이스는 BlackThunder.UniEngine.PhysicalSpace이다.

UniEngine.dll은 UnityEngine을 참조하지 않는다. 유니티용 전략은 저장소에 두되 파일 전체를 `UNITY_5_3_OR_NEWER` 조건으로 감싸 일반 dotnet 빌드에서는 빈 파일이 되게 한다. 유니티 프로젝트는 UniEngine.dll을 넣고 이 소스를 함께 컴파일한다.

물리 소스는 애플리케이션을 조립하는 곳에서 선택한다. 도메인 코드는 전달받은 공개 핸들만 사용하며 물리 객체를 직접 다루는 책임은 전략에 둔다. 유니티 밖의 물리 소스와 배포 구성은 미정이다.

## 공개 핸들과 전략

| 대상 | 책임 |
| --- | --- |
| 공개 핸들 | 식별과 수명 상태, 폐기 후 사용 거부, `Disposed`와 이벤트 전달, 인자 검사와 소속 씬 확인 |
| 전략 | 소스의 물리 객체를 만들고 읽고 바꾸고 제거한다. 공개 단위·값 형식과 소스의 단위·값 형식을 변환한다. |

1. 공개 핸들은 물리 소스와 관계없이 하나이다. 소스마다 다른 공개 형식을 두지 않는다.
2. 수명 상태는 공개 핸들만 가진다. 전략은 폐기 상태를 갖지 않으므로 두 층의 상태를 맞추지 않는다.
3. 식별·수명 확인·무효화·이벤트 연결의 기준은 공개 핸들 하나이다. 소스가 먼저 제거한 객체는 전략이 알리고 공개 핸들이 폐기 상태가 된다.
4. 전략은 씬을 만들거나 얻을 때 정하고 이후 바꾸지 않는다. 물리 상태를 다른 소스로 옮길 수 없다.
5. 전략 인터페이스는 유니티용 전략이 다른 어셈블리에서 구현하므로 공개한다. 도메인 코드가 호출하는 API가 아니다.
6. 공통 API에 없는 소스의 고유 기능은 전략에 넣지 않는다.

물리 소스를 추가할 때는 공개 핸들을 고치지 않고 전략을 구현한다.

## 공통 기준

1. 이름은 유니티를 우선한다. 유니티 6의 이름을 기준으로 하고 표기는 저장소의 C# 컨벤션에 따라 PascalCase로 쓴다. 유니티에 없는 개념은 기존 UniEngine의 이름을 쓴다.
2. 의미가 유니티와 다르면 이 문서를 따른다. 값의 단위, 객체의 상태와 규칙이 이에 해당한다.
3. 시점과 수명은 통일하지 않고 물리 소스를 따른다. 물리 객체가 사라지는 시점, 시간을 진행하는 주체와 주기, 씬을 만드는 방법과 수명, 콜백의 시점과 스레드가 이에 해당한다.
4. 물리 소스가 제공하지 못하는 기능은 빈 결과나 다른 기능으로 대신하지 않고 `NotSupportedException`으로 알린다. 지원 여부는 호출 전에 확인할 수 있게 한다.

월드의 공개 형식은 `PhysicalSpace2D`, `PhysicalSpace3D`이다. 접미사 없는 `PhysicalSpace`는 네임스페이스 `BlackThunder.UniEngine.PhysicalSpace`와 겹쳐 UniEngine 네임스페이스 안의 코드에서 형식으로 사용할 때 CS0118이 발생하므로 차원을 붙인다. 유니티에는 같은 형식 이름이 없어 `Uni` 접두사를 붙이지 않는다. 유니티의 `PhysicsScene2D`·`PhysicsScene`에 대응한다.

그 밖의 공개 형식에는 `Uni` 접두사를 유지한다. 유니티와 같은 형식 이름을 쓰면 UnityEngine과 이 모듈의 네임스페이스를 함께 가져온 코드가 모호한 참조(CS0104)로 컴파일되지 않는다.

물리 엔진의 계산 방법이 다를 수 있으므로 같은 입력에 대한 움직임이 소스 사이에서 같다고 보장하지 않는다. 같은 결과가 필요한 실행 환경 사이에서는 같은 물리 소스를 사용한다.

## 객체와 이름

| 공개 형식 | 유니티 | 책임 |
| --- | --- | --- |
| `PhysicalSpace2D`, `PhysicalSpace3D` | `PhysicsScene2D`, `PhysicsScene` | 물리 객체의 생성과 질의, 갱신을 제공한다. |
| `UniRigidbody2D`, `UniRigidbody3D` | `Rigidbody2D`, `Rigidbody` | 자세, 속도, 질량, 힘과 운동 방식을 제공한다. |
| `UniCollider2D`, `UniCollider3D` | `Collider2D`, `Collider` | 도형, 트리거 여부, 재질, 필터와 이벤트를 제공한다. |
| `UniJoint2D`, `UniJoint3D` | `Joint2D`, `Joint` | 강체 사이의 제약을 제공한다. |
| `UniShape2D`, `UniShape3D` | 콜라이더의 종류와 크기 | 위치와 독립적인 기하를 값으로 표현한다. |
| `UniRaycastHit2D`, `UniRaycastHit3D` | `RaycastHit2D`, `RaycastHit` | 질의에 닿은 콜라이더, 위치, 법선과 거리를 제공한다. |

| 개념 | 공개 이름 |
| --- | --- |
| 갱신 | `Simulate` |
| 운동 방식 | `BodyType`의 `Dynamic`, `Kinematic`, `Static` |
| 속도와 감쇠 | `LinearVelocity`, `AngularVelocity`, `LinearDamping`, `AngularDamping` |
| 힘과 이동 | `AddForce`, `AddTorque`, `MovePosition`, `MoveRotation` |
| 겹침만 감지하는 콜라이더 | `IsTrigger` |
| 도형 | `Box`, `Circle`(2D), `Sphere`(3D), `Capsule`, `Polygon`·`Edge`(2D), `Mesh`(3D) |
| 재질 | `Friction`, `Bounciness` |
| 질의 | `Raycast`, `BoxCast`, `CircleCast`(2D), `SphereCast`(3D), `CapsuleCast`, `OverlapBox`, `OverlapCircle`(2D), `OverlapSphere`(3D), `OverlapCapsule`, `OverlapPoint`(2D), `ClosestPoint` |
| 접촉·겹침 이벤트 | `CollisionEnter`, `CollisionExit`, `TriggerEnter`, `TriggerExit` |
| 유니티에 없는 설정 | `ActiveEvents`, `ActiveCollisionTypes` |

한 강체에 여러 콜라이더를 연결할 수 있으며, 강체에 연결하지 않은 콜라이더도 제공한다. 트리거는 겹침을 감지하되 물리 반응을 만들지 않는다. 물리 상태 전체를 별도로 복제하지 않고 메서드 호출을 전략을 거쳐 소스의 물리 객체에 전달한다.

같은 물리 객체를 가리키는 공개 핸들은 일관되게 찾는다. 식별자는 자원의 소유권이 아니며, 재사용된 식별자를 이전 객체로 취급하지 않는다.

## 도형과 단위

| 항목 | 공개 계약 |
| --- | --- |
| 각도·각속도·각가속도 | 도, 도/초, 도/초²를 사용하며 소스의 단위로 변환한다. |
| 조인트와 모터 | 회전 한계, 목표 각도와 목표 각속도에도 같은 변환을 적용한다. |
| 3D 회전 | 쿼터니언은 그대로 전달한다. 축-각도와 회전 벡터를 받는 API의 각도 크기는 도 단위이다. |
| 크기 | 이름과 뜻이 함께 정해진 값은 유니티의 뜻을 따른다. `Box`의 크기는 전체 길이, `Capsule`의 높이는 양끝의 반구를 포함한 전체 높이이다. |
| 시간·기타 물리량 | 시간은 초이다. 질량·관성·힘·토크 자체에 각도 변환을 적용하지 않는다. |
| 정밀도 | 공통 API는 f32이다. |

자세는 Transform2D·Transform3D로 표현한다. 두 타입은 병진과 회전만 가지며 Transform2D의 회전은 도 단위이다. 위치와 방향은 `System.Numerics.Vector2`·`Vector3`, 3D 회전은 `Quaternion`을 사용한다. 유니티의 값 형식은 유니티용 전략이 변환하며 공통 API에 나타나지 않는다. 유니티의 [3D 각속도](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/rigidbody/angularvelocity)는 라디안/초, [2D 각속도](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/rigidbody2d/angularvelocity)는 도/초이므로 각각 다르게 변환한다.

도형은 순수 관리 데이터이며 폐기 대상이 아니다. 메시 표면과 채워진 입체, 볼록 다각형과 임의의 오목 다각형을 동일하게 취급하지 않는다. 2D와 3D 씬은 각각 독립된 시뮬레이션이다.

## 갱신과 조회

1. 애플리케이션이 물리 소스를 선택하고 씬을 얻는다.
2. 씬에서 강체와 콜라이더를 만든다. 반환된 객체는 이미 씬에 등록된 객체이다.
3. 필요한 `ActiveEvents`, 충돌 조합, 필터와 대리자를 설정한다.
4. 물리 소스의 방식에 따라 시간이 진행된다.
5. 갱신 결과의 자세·속도·접촉을 도메인 객체에 반영하거나 충돌 상대를 조회한다.
6. 사용이 끝난 객체를 `Dispose()`한다.

`Simulate(deltaTime)`은 0보다 큰 시간 간격으로 운동과 충돌을 함께 갱신한다. 운동 없이 충돌만 갱신하는 API는 제공하지 않는다. 시간을 직접 진행할 수 없는 상태의 물리 소스는 `Simulate`를 미지원으로 알린다.

`collider.GetCollidingColliders()`는 실제 접촉과 실제 트리거 겹침의 상대를 합친 읽기 전용 스냅샷을 돌려준다. 자기 자신과 폐기된 상대를 제외하고 상대 콜라이더별로 중복을 제거한다. 같은 강체의 서로 다른 콜라이더는 별개 상대이다. 접촉 후보가 존재하는 것만으로 실제 충돌이라고 판단하지 않는다. 조회는 마지막으로 완료된 갱신의 접촉 상태를 읽으며 첫 갱신 전에는 비어 있다. 조회 자체가 시뮬레이션을 진행하지 않는다. 이미 받은 스냅샷의 객체가 이후 폐기될 수 있으므로 다시 사용할 때는 `IsDisposed`를 확인한다.

두 콜라이더가 모두 동적이 아닌 강체에 속하면 기본으로 감지하지 않는다. 강체에 연결하지 않은 콜라이더는 `Static` 강체에 속한 것으로 본다. 이 조합의 감지가 필요하면 `ActiveCollisionTypes`로 명시적으로 활성화하며, 물리 소스가 감지하지 못하는 조합은 미지원으로 알린다.

기본 검사와 조회 경로가 전체 객체 쌍을 전수 검사해서는 안 된다. 물리 소스의 공간 인덱스와 접촉 정보를 이용한다. 갱신을 콜백에서 다시 호출하지 않는다.

## 이벤트

| 이벤트 | 제공 내용 |
| --- | --- |
| `CollisionEnter`, `CollisionExit` | 한 갱신에서 접촉을 시작·종료한 상대를 읽기 전용 목록으로 전달한다. |
| `TriggerEnter`, `TriggerExit` | 한 갱신에서 트리거 겹침을 시작·종료한 상대를 읽기 전용 목록으로 전달한다. |
| `Disposed` | 해당 객체의 정리가 끝났음을 한 번 전달한다. |

접촉 유지 이벤트는 제공하지 않는다. 유지 중인 상대는 `GetCollidingColliders`로 조회한다.

이벤트 종류별로 한 갱신에 최대 한 번 전달하며 빈 목록과 같은 목록 안의 중복 상대를 제외한다. 한 갱신 안에 시작과 종료가 모두 발생했다면 같은 상대가 두 목록에 들어갈 수 있다. 이벤트를 받을 콜라이더는 `ActiveEvents`로 명시적으로 켜며, 대리자 등록만으로 설정을 바꾸지 않는다. 접촉 상대의 조회에는 이벤트 설정이나 구독이 필요하지 않다.

폐기 때문에 생긴 종료는 알리지 않으며 폐기된 객체의 대리자를 뒤늦게 호출하지 않는다. 목록을 전달하는 시점과 스레드는 물리 소스를 따른다. 개별 전이, 접촉력처럼 원본의 상세 이벤트가 필요하면 소스의 고유 기능을 직접 사용한다.

## Dispose와 소유권

### 소유 범위

| 대상 | 폐기 효과 |
| --- | --- |
| 콜라이더 | 등록과 강체 연결을 해제하고 해당 콜라이더를 제거한다. 강체는 유지한다. |
| 강체 | 연결된 콜라이더와 조인트도 제거한다. |
| 조인트 | 제약을 해제한다. 연결된 강체는 유지한다. |
| 씬 | 이 모듈이 만든 물리 객체와 연결을 정리한다. 도메인 객체는 폐기하지 않는다. |
| 감싼 유니티 객체 | 유니티 Scene에 이미 있던 물리 객체를 감쌌다면 연결만 끊고 유니티 객체는 제거하지 않는다. |
| 이벤트·훅 등록 객체 | 해당 등록과 대리자 참조를 해제한다. 연결 대상 객체 자체는 폐기하지 않는다. |

### 공통 상태 계약

1. `IsDisposed`를 즉시 `true`로 만들어 새로운 사용과 이벤트 연결을 거부한다.
2. 해당 객체를 접촉 상대 조회와 이벤트에서 제외한다.
3. 등록된 `Disposed` 대리자를 한 번 호출한다.
4. 알림의 성공 여부와 무관하게 모든 이벤트·훅 대리자 참조를 해제한다.

동일 객체의 `Dispose` 재호출은 아무 작업도 하지 않는다. 폐기 후에는 상태 확인, 재폐기와 구독 해제를 허용하며 그 밖의 객체 사용과 새로운 구독은 `ObjectDisposedException`으로 거부한다. 같은 물리 객체를 가리키는 다른 접근 경로에도 무효화가 반영되어야 한다.

폐기 알림에서 예외가 발생해도 나머지 구독자와 소유 객체의 정리를 계속하고, 여러 예외는 모아서 전달한다. 물리 객체가 실제로 사라지는 시점과 `Disposed`를 알리는 시점은 다음 절의 물리 소스별 동작을 따른다.

## 물리 소스별 동작

다음 항목은 통일하지 않는다.

| 항목 | 유니티 |
| --- | --- |
| 시간 진행 | 유니티가 자기 주기로 진행한다. `Simulate`는 물리 시뮬레이션 모드가 Script일 때만 지원한다. |
| 씬 | 유니티 Scene의 물리 씬을 얻는다. 기본이 아닌 물리 씬은 유니티 Scene에 묶인다. |
| 물리 객체의 실제 제거 | 유니티의 Destroy 규칙에 따라 현재 Update 루프가 끝난 뒤에 제거한다. 그 전에는 물리 계산에 남아 있을 수 있다. |
| `Disposed` 알림 | 유니티가 객체를 파괴한 뒤에 알린다. |
| 소스가 먼저 제거한 객체 | 유니티가 객체를 파괴하면 공개 핸들도 폐기 상태가 된다. |
| 이벤트 시점과 스레드 | 유니티의 물리 콜백 시점에 메인 스레드에서 받은 결과를 이용한다. |
| 자세 변경의 질의 반영 | 유니티의 물리 동기화 설정을 따른다. |
| 콜백 예외 | 유니티의 예외 처리 방식을 따른다. |

유니티의 [Simulate](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/physics/simulate)는 시뮬레이션 모드가 Script일 때 수동으로 호출하며 작은 양수의 시간 간격을 권한다. [물리 씬](https://docs.unity.com/en-us/engine/current/script-reference/unityengine/physicsscene/simulate)은 기본 씬이 아니면 특정 Scene에 연결된다. [Destroy](https://docs.unity.com/en-us/engine/current/script-reference/unityengine/object/destroy)의 실제 제거는 현재 Update 루프 뒤로 미루어진다. 3D의 [ContactEvent](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/physics/contactevent)는 트리거를 알리지 않으므로 트리거 이벤트는 유니티 컴포넌트로 받는다. 이 문단의 내용은 유니티 문서를 조사한 결과이며, 표의 나머지 유니티 항목은 구현할 때 확인한다. 유니티에서 실행하여 확인한 것은 없다.

공개 이름은 유니티 6을 따른다. 유니티 2022.3의 Rigidbody에는 linearVelocity가 없고 velocity만 있으므로, 버전별 이름 차이는 유니티용 전략이 조건부 컴파일로 맞춘다.

## 공개 API 초안

다음 표는 대표 진입점이며 `2D` 예시와 대응되는 `3D` 타입을 함께 제공한다. 전체 목록과 물리 소스별 대응은 API Coverage를 따른다.

| 대상 | 대표 멤버 |
| --- | --- |
| `PhysicalSpace2D : IDisposable` | `Gravity`, `Simulate(float deltaTime)`, `CreateRigidbody(UniRigidbodyType bodyType)`, `CreateCollider(UniShape2D shape, UniRigidbody2D attachedRigidbody = null)`, 질의 메서드, 지원 여부 확인 |
| `UniRigidbody2D : IDisposable` | `BodyType`, `Position`, `Rotation`, 자세 전체의 `Pose`, `LinearVelocity`, `AngularVelocity`, `Mass`, 감쇠, 힘과 이동 |
| `UniCollider2D : IDisposable` | `AttachedRigidbody`, `Shape`, `IsTrigger`, `Friction`, `Bounciness`, 필터, `ActiveEvents`, `ActiveCollisionTypes`, `GetCollidingColliders()` |
| 콜라이더 이벤트 | `event Action<IReadOnlyList<UniCollider2D>> CollisionEnter`, `CollisionExit`, `TriggerEnter`, `TriggerExit` |
| `UniRaycastHit2D` | `Collider`, `Point`, `Normal`, `Distance` |
| 자원 객체 | `IsDisposed`, `Dispose()`, `event Action Disposed` |
| 물리 소스 진입점 | 유니티 Scene의 물리 씬 얻기와 기존 유니티 물리 객체 감싸기. 유니티용 전략과 같은 곳에 둔다 |

## 대표 흐름

아래 코드는 물리 소스의 선택과 공통 코드의 관계를 설명하는 초안이다. 아직 컴파일 가능한 예제가 아니다.

### 공통 코드

```csharp
using System;
using System.Numerics;
using BlackThunder.UniEngine.PhysicalSpace;

public static class BallSimulation
{
    // Uses only the common API, so the physics source does not matter here.
    public static UniCollider2D Build(PhysicalSpace2D scene)
    {
        var goal = scene.CreateCollider(UniShape2D.Box(new Vector2(4f, 4f)));
        goal.IsTrigger = true;
        goal.ActiveEvents = UniActiveEvents.Trigger;
        goal.TriggerEnter += others => Console.WriteLine(others.Count);

        var body = scene.CreateRigidbody(UniRigidbodyType.Dynamic);
        body.Position = new Vector2(0f, 5f);
        return scene.CreateCollider(UniShape2D.Circle(0.5f), body);
    }
}
```

### 유니티 안

```csharp
var scene = UnityPhysics.GetScene2D(gameObject.scene);
var ball = BallSimulation.Build(scene);
```

유니티 물리의 씬은 유니티가 시간을 진행한다.

## 검토 기준

| 상황 | 충족해야 할 동작 |
| --- | --- |
| 물리를 쓰지 않는 환경 | UniEngine.dll과 의존 DLL만으로 실행되며 UnityEngine을 요구하지 않는다. |
| 공통 API만 쓴 코드 | 물리 소스를 바꾸어도 수정 없이 컴파일된다. |
| 유니티용 전략 | 지원하는 유니티 버전의 컴파일러로 컴파일되고, 일반 dotnet 빌드에는 포함되지 않는다. |
| 새 물리 소스 | 공개 핸들을 고치지 않고 전략만 구현하여 추가한다. |
| 소스가 제공하지 못하는 기능 | 빈 결과나 다른 기능으로 대신하지 않고 미지원으로 알린다. |
| 후보 쌍만 있고 실제 접촉은 없음 | 접촉 상대 목록에 포함하지 않는다. |
| 트리거 겹침·복수 접촉점 | 실제 겹친 상대를 포함하고 동일 상대는 한 번만 반환한다. |
| 이벤트 비활성·미구독 | 갱신 뒤 접촉 상대 조회는 계속 가능하다. |
| 비동적 객체끼리의 감지 | 활성화하지 않으면 감지하지 않고, 소스가 지원하지 않는 조합은 미지원으로 알린다. |
| 폐기 | 즉시 폐기 상태가 되고 새 조회에서 제외하며 종료 알림을 합성하지 않는다. 실제 제거 시점은 소스를 따른다. |
| 감싼 유니티 객체의 폐기 | 연결만 끊고 유니티 객체를 제거하지 않는다. |
| 유니티가 먼저 파괴한 객체 | 공개 핸들도 폐기 상태가 되고 `Disposed`를 한 번 알린다. |
| 중복 폐기·콜백 예외 | 다른 소유자의 자원을 해제하지 않고 알림 중복이나 자원 누락 없이 정리한다. |
| 회전과 각속도 | 소스의 단위 차이와 관계없이 공개 값은 도 단위이다. |

[API Coverage](API%20Coverage.md) · [구조 결정](DLL%20Structure%20Decision.md) · [Infrastructure](../README.md)
