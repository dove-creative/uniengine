# PhysicalSpace

PhysicalSpace는 Rapier·Parry의 충돌·물리·기하 기능을 C# 객체로 제공하는 Infrastructure 모듈이다. 현재는 0단계의 독립 Rust 연결 DLL을 검증하는 중이다. 이 문서는 2D·3D의 객체 구성, 충돌 상태 조회와 수명 관리 계약을 정의한다. C# API와 예시는 설계 단계이며 본체 구현 완료를 뜻하지 않는다.

## 목차

- 소개
- 객체 구성과 핸들
- 도형과 단위
- 생성과 갱신
- 이벤트와 충돌 상태 조회
- Dispose와 소유권
- 네이티브 구성과 배포
- 공개 API 초안
- 대표 흐름
- 검토 기준

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | Rapier의 시뮬레이션과 Parry의 기하 기능을 2D·3D에서 이용하는 모듈이다. |
| 목적 | 원본의 용어와 기능을 유지하면서 C# 객체, 대리자와 명시적 자원 해제로 사용할 수 있게 한다. |
| 구현 | Rapier·Parry를 고정 버전으로 참조하는 별도 Rust 프로젝트에서 공개 API를 C ABI로 연결한다. 0단계는 Rapier 로컬 수정본 0.36.1-r1과 수정하지 않은 Parry 0.31.1을 검증했으며, PhysicalSpace의 C# 호출 구현은 후속 단계이다. |

전체 기능은 채택 버전의 공개 라이브러리 기능을 기준으로 한다. 차원·정밀도·선택 기능에 따른 제약과 연결 경로는 [API Coverage](API%20Coverage.md)에 기록한다. 데모와 별도 엔진 통합 패키지는 이 범위에 포함하지 않는다.

Infrastructure의 3단계 모듈이며 네임스페이스는 BlackThunder.UniEngine.PhysicalSpace이다. 2단계 Objects의 Transform2D·Transform3D로 f32 자세를, Vector2Int·Vector3Int로 정수 격자 좌표를 표현하며 1단계 Math의 각도 단위·근사 비교·예외 기준을 따른다. Core와 Services에는 의존하지 않는다. C# 래퍼는 본체 어셈블리에 포함하며, Rapier·Parry 네이티브 모듈은 Foundations의 [Rapier](../../00-Foundations/Rapier.md) 모듈로서 컴파일된 DLL로 호출한다.

## 객체 구성과 핸들

도형·충돌체·강체를 구분하는 원본 객체 모델을 사용한다. 사용자 게임 객체는 라이브러리가 제공하는 객체를 보유하며, 이벤트 수신을 위해 인터페이스를 구현하거나 클래스를 상속할 필요가 없다.

| 원본 개념 | C# 타입 예시 | 책임과 소유 정보 |
| --- | --- | --- |
| `PhysicsWorld` | `PhysicsWorld2D`, `PhysicsWorld3D` | 등록 객체, 중력, 갱신 설정과 시뮬레이션 상태를 소유한다. |
| `Shape` | `Shape2D`, `Shape3D`와 구체 도형 | 위치와 독립적인 기하를 표현한다. |
| `SharedShape` | `SharedShape2D`, `SharedShape3D` | 여러 콜라이더·질의가 공유할 네이티브 도형 참조를 소유한다. |
| `Collider` | `Collider2D`, `Collider3D` | 도형, 자세, 센서 여부, 재질, 충돌 필터와 이벤트 설정을 제공한다. |
| `RigidBody` | `RigidBody2D`, `RigidBody3D` | 자세, 질량 특성, 속도, 힘과 운동 방식을 제공한다. |
| `ImpulseJoint`, `MultibodyJoint` | 차원별 동일 이름 타입 | 강체 사이의 제약, 한계와 모터를 제공한다. |
| `SoftBody` | `SoftBody2D`, `SoftBody3D` | 원본이 제공하는 변형체와 그 구성 요소를 관리한다. |
| `NarrowPhase`, `QueryPipeline` | 차원별 동일 이름 타입 | 접촉 관계와 공간 질의를 제공한다. 월드 소속 접근 객체는 월드 수명을 따른다. |

한 강체에 여러 콜라이더를 연결할 수 있으며, 강체에 연결하지 않은 콜라이더도 제공한다. 센서는 겹침을 감지하되 물리 반응을 만들지 않는 콜라이더이다. 원본의 기본 충돌 조합·그룹 필터를 유지한다. 원본은 두 콜라이더가 모두 동적이 아닌 강체(고정·운동학적)에 속하면 감지하지 않으며, 강체에 연결하지 않은 콜라이더는 고정 강체에 속한 것으로 취급한다. 이 조합의 감지가 필요하면 해당 조합을 명시적으로 활성화하며, 두 콜라이더 중 한쪽의 허용 설정으로 충족된다. [Rapier Colliders](https://rapier.rs/docs/user_guides/c/colliders/)

### 핸들 값과 C# 객체

C# 객체는 소유 월드와 네이티브 핸들을 기억하는 래퍼이다. 물리 상태 전체를 별도로 복제하지 않으며, 메서드 호출을 해당 네이티브 객체에 전달한다. 직접 핸들을 전달하는 API와 같은 객체를 조작하지만, 수명 확인과 이벤트 연결을 C# 객체가 함께 담당한다.

공개 객체는 네이티브 핸들을 직접 소유하는 한 층이다. 같은 네이티브 객체를 감싸는 별도 바인딩 객체를 두지 않으므로 식별·수명 확인·무효화·이벤트 연결의 기준은 공개 객체 하나이다. P/Invoke 선언, ABI 검사, 상태 코드 변환과 콜백 연결은 공개 객체와 분리한 내부 계층이 맡으며, 이 계층은 객체별 수명 상태를 소유하지 않는다.

월드·객체 종류·핸들 세대를 포함한 식별로 같은 네이티브 객체의 래퍼를 일관되게 찾는다. 핸들 값 자체는 자원의 소유권이 아니다. 조회나 이벤트에 들어 있는 핸들을 통해 객체를 찾을 때도 유효성을 확인하며, 재사용된 슬롯을 이전 객체로 취급하지 않는다.

월드·강체·콜라이더·조인트·공유 도형 등 자원을 소유하는 객체는 `IDisposable`을 제공한다. 벡터, 도형 설명, 빌더와 복사된 이벤트 결과 같은 순수 관리 데이터까지 일괄적으로 `IDisposable`로 만들지는 않는다. 네이티브 제거 경로와 C# 래퍼의 무효화는 같은 소유권 관리에 참여한다.

## 도형과 단위

### 원본 도형과 차원

`Ball`, `Cuboid`, `Capsule`, `Segment`, `Polyline`, `Triangle`, `TriMesh`, `HalfSpace`, `HeightField`, `Compound` 등의 이름을 원본대로 사용한다. 2D의 `ConvexPolygon`과 3D의 `ConvexPolyhedron`, `Cylinder`, `Cone` 등 차원별 차이도 유지한다. 전체 범위는 API Coverage의 도형 표를 따른다.

점은 `PointQuery`, 반직선은 `Ray` 질의로 다룬다. 별도의 네이티브 점 콜라이더가 있다고 가정하지 않는다. `Segment`는 두 끝점 사이의 유한한 선분이며, `HalfSpace`는 무한 경계의 한쪽을 차지하는 영역으로 두께 없는 평면과 다르다.

3D 공간 안의 선·표면·입체는 해당 차원의 도형과 질의로 다룬다. 메시 표면과 채워진 입체, 볼록 다각형과 임의의 오목 다각형을 동일하게 취급하지 않는다. 점·선 감지 예시는 질의나 센서를 사용하되, 기본 API가 원본에서 허용하는 도형·강체 조합을 추가로 제한하지 않는다. 직접 질의에서 지원하지 않는 조합은 '충돌 없음'으로 바꾸지 않고 원본의 미지원 결과를 구분하여 전달한다. 월드의 접촉·겹침 계산에서는 원본이 미지원 조합을 오류 없이 접촉·겹침 없음으로 처리하므로 래퍼도 이를 구분하지 못한다. 월드에서 감지할 도형 조합은 원본의 지원 범위를 확인하여 사용한다.

2D와 3D 월드는 각각 독립된 시뮬레이션이다. 로컬 도형, 부모 강체에 대한 콜라이더 자세, 월드 자세를 구분한다. 공개 기능의 입력 조건과 도형 지원 범위는 [Parry 2D Shape](https://docs.rs/parry2d/latest/parry2d/shape/index.html)와 [3D Shape](https://docs.rs/parry3d/latest/parry3d/shape/index.html)를 기준으로 한다.

### 운동 방식과 단위

`RigidBodyType`에는 원본의 `Fixed`, `Dynamic`, `KinematicPositionBased`, `KinematicVelocityBased` 구분을 유지한다. 위치 기반 운동학적 강체는 다음 목표 자세로, 속도 기반 운동학적 강체는 속도로 제어한다. 동적 강체에는 힘·충격량·속도 등 원본 입력을 제공한다. 운동학적 강체가 충돌만으로 자동 정지한다고 보장하지 않는다.

| 항목 | C# 공개 계약 |
| --- | --- |
| 각도·각속도·각가속도 | 도, 도/초, 도/초²를 사용하며 네이티브 경계에서 라디안 계열로 변환한다. |
| 조인트와 모터 | 회전 한계, 목표 각도와 목표 각속도에도 같은 변환을 적용한다. 각도에 의존하는 계수도 단위를 확인한다. |
| 3D 회전 | 쿼터니언은 그대로 전달한다. 축-각도와 회전 벡터를 받는 API의 각도 크기는 도 단위이다. |
| 시간·기타 물리량 | 시간은 초이다. 질량·관성·힘·토크 자체에 각도 변환을 적용하지 않는다. 길이 스케일은 원본 설정을 따른다. |
| 정밀도 | f32·f64를 구분하며 f64 값을 f32로 암묵적으로 축소하지 않는다. |

f32 API의 자세는 Transform2D·Transform3D로 표현한다. 두 타입은 병진과 회전만 가지므로 원본 자세와 같은 구성이며, Transform2D의 회전은 공개 각도 규칙과 같은 도 단위이다. 병진·속도·방향처럼 자세가 아닌 값은 `System.Numerics.Vector2`·`Vector3`, 3D 회전만 다루는 값은 `Quaternion`을 사용한다. Voxels·HeightField 등의 정수 격자 좌표에는 Vector2Int·Vector3Int를 사용한다. Transform2D·Transform3D는 f32 전용이므로 f64 API에는 배정밀도를 유지하는 별도 수학 타입을 대응시키며, 그 타입의 정의 위치는 구현 시 정한다. 상위 계층의 장면 객체는 의존성으로 두지 않는다. 공개 API의 각도 단위는 원본과 의도적으로 다른 부분이므로 원본 예제를 옮길 때 확인한다. [Rapier 회전과 자세](https://rapier.rs/docs/user_guides/templates/rigid_body_position/)

## 생성과 갱신

1. 차원과 네이티브 구성에 맞는 `PhysicsWorld`를 생성한다.
2. 빌더로 강체·콜라이더를 삽입한다. 반환된 래퍼는 이미 월드에 등록된 객체이다.
3. 필요한 `ActiveEvents`, 충돌 조합·그룹과 대리자를 설정한다.
4. `Step(deltaTime)`으로 운동과 충돌을 갱신하거나, `DetectCollisions()`로 운동 없이 충돌 상태를 갱신한다.
5. 계산된 자세·속도·접촉 결과를 확정한 뒤 일반 이벤트를 전달한다. 호출자는 결과를 게임 객체에 반영하거나 충돌 상대를 조회한다.
6. 사용이 끝난 소유 객체를 `Dispose()`한다.

접촉 후보는 원본 BroadPhase와 공간 인덱스로 좁히고 NarrowPhase에서 정밀 검사한다. 래퍼의 기본 검사·조회 경로가 전체 객체 쌍을 전수 검사해서는 안 된다. 밀집된 장면에서 실제 후보나 접촉 수가 많아지는 비용까지 없어지는 것은 아니다.

`Step`에는 충돌 검사가 포함되므로 같은 상태에 `DetectCollisions`를 추가 실행할 필요가 없다. 삽입·이동·필터 변경 후 운동을 진행하지 않고 접촉을 갱신할 때는 `DetectCollisions`를 사용한다. 공간 질의도 브로드페이즈의 BVH를 재사용하므로 마지막 `Step` 또는 `DetectCollisions` 이후의 삽입·제거·이동을 반영하지 않는다. CCD는 물리 갱신의 기능이며, 선형·회전 포함 형상 이동 질의와 별개이다. 각 기능은 원본의 설정과 질의 API로 제공한다. [Rapier Simulation structures](https://rapier.rs/docs/user_guides/c/simulation_structures/)

월드 변경과 갱신에는 배타적 접근이 필요하며 콜백에서 갱신을 재진입하지 않는다. 계산 중 호출되는 필터·접촉 수정 훅과 사후 알림을 구분한다. 병렬 실행에서는 계산 중 훅·즉시 콜백이 작업 스레드에서 호출될 수 있으므로 해당 대리자는 스레드 안전해야 한다.

0단계에서 Rapier 0.36.1 원본의 `DetectCollisions → Step` 운동 정지와 무수정 dt=0 대안의 초기 수면 전환 문제를 재현했다. [로컬 수정본 0.36.1-r1](../../../../packages/rapier/docs/revisions/0.36.1-r1/Plan.md)은 PhysicsWorld의 검사 경로와 persistent island 초기화를 보완하여 네 구성의 Release·Debug 회귀 검증을 통과했다. 이는 해당 네이티브 실험의 갱신 계약 검증이며 제품 C# API나 전체 PhysicalSpace 구현 완료는 아니다. 대응 범위는 [네이티브 조사](Native%20Coverage.md)에 기록한다.

## 이벤트와 충돌 상태 조회

### 원본 이벤트와 편의 이벤트

| 경로 | 제공 내용 |
| --- | --- |
| 월드의 `CollisionEvent` | 원본 `Started/Stopped`, 콜라이더 핸들과 센서·제거 표시를 보존한 개별 이벤트이다. |
| 월드의 `ContactForceEvent`, `SoftBodyTearEvent` | 원본의 힘·임계값 관련 정보와 변형체 찢김 정보를 보존한다. |
| 콜라이더의 `CollisionStarted`, `CollisionStopped` | 구독한 객체에 대해 해당 갱신에서 시작·종료한 상대를 읽기 전용 목록으로 전달하는 선택적 편의 이벤트이다. |
| 자원 소유 객체의 `Disposed` | 해당 객체의 정리가 끝났음을 선택적으로 한 번 전달한다. |

`CollisionStayed`는 제공하지 않는다. 유지 중인 접촉을 매 갱신마다 알리거나 이를 위해 이전 상대 목록을 비교하지 않는다.

일반 이벤트는 계산 후 갱신 호출 스레드에서 전달한다. 원본 경로는 상대 목록으로 합치지 않고 개별 전이와 플래그를 보존한다. 편의 경로는 이벤트 종류별로 최대 한 번 전달하며 빈 목록과 같은 목록 안의 중복 상대를 제외한다. 한 갱신 안에 시작과 종료가 모두 발생했다면 같은 상대가 두 목록에 들어갈 수 있다. 전이의 상세 순서가 필요하면 원본 경로를 사용한다.

원본과 편의 이벤트 모두 `ActiveEvents`에 따라 생성된 이벤트를 이용한다. 대리자 등록만으로 원본의 이벤트 활성화 설정을 암묵적으로 바꾸지 않는다. 접촉 유지 조회에는 이벤트 활성화나 구독이 필요하지 않다.

폐기 때문에 생성된 `Stopped`와 제거 표시는 살아 있는 월드의 원본 이벤트 경로에 보존한다. 편의 `CollisionStopped`에서는 제거에 의한 종료를 생략한다. 래퍼가 별도의 종료 이벤트를 합성하지 않으며, 폐기된 객체의 대리자를 뒤늦게 호출하지 않는다. 이벤트의 핸들이 더 이상 살아 있는 객체로 해석되지 않을 수도 있다.

원본 시점의 일시적인 접촉 정보가 필요한 경우 계산 중 호출되는 `EventCallbacks2D/3D`도 선택적으로 제공한다. 해당 문맥의 유효 범위는 콜백 실행 중이며, 보관할 결과는 복사해야 한다. 이벤트 수신과 훅은 제공된 객체에 대리자를 연결하는 방식이며 사용자 인터페이스 구현을 요구하지 않는다. [Rapier Advanced collision-detection](https://rapier.rs/docs/user_guides/c/advanced_collision_detection/)

### 현재 충돌 중인 상대

| API | 반환 의미 |
| --- | --- |
| `world.NarrowPhase.ContactPairsWith(collider)` | 일반 접촉 후보 쌍과 접촉 상세를 읽는다. 실제 접촉은 `HasAnyActiveContact`로 확인한다. |
| `world.NarrowPhase.IntersectionPairsWith(collider)` | 센서가 포함된 겹침 후보 쌍을 읽는다. 실제 겹침은 결과의 `Intersecting` 값으로 확인한다. |
| `collider.GetCollidingColliders()` | 실제 일반 접촉과 실제 센서 겹침의 상대를 합친 `IReadOnlyList<Collider2D>` 또는 `IReadOnlyList<Collider3D>` 스냅샷이다. |

`GetCollidingColliders`는 원본 두 조회를 이용하는 래퍼 편의 기능이다. 자기 자신과 폐기된 상대를 제외하고 상대 콜라이더별로 중복을 제거한다. 같은 강체의 서로 다른 콜라이더는 별개 상대이며, 강체 기준 집계가 필요하면 호출자가 부모를 기준으로 묶는다. 접촉 후보가 존재하는 것만으로 실제 충돌이라고 판단하지 않는다. [Rapier NarrowPhase](https://docs.rs/rapier2d/latest/rapier2d/geometry/struct.NarrowPhase.html)

조회는 마지막으로 완료된 `Step` 또는 `DetectCollisions`의 접촉 상태를 읽는다. 첫 검사 전에는 결과가 비어 있으며, 이후 자세·활성 상태·필터 변경은 다음 검사에서 반영된다. 조회 자체가 시뮬레이션이나 추가 정밀 검사를 수행하지 않는다. 충돌 필터가 제외한 관계는 반환하지 않는다.

위 접촉·겹침 조회의 C# 결과도 읽기 전용 스냅샷으로 제공하여 네이티브 자료의 재배치에 영향을 받지 않게 한다. 새 조회는 폐기된 상대를 제외하지만 이미 받은 스냅샷의 객체가 이후 폐기될 수는 있다. 다시 객체를 사용할 때는 `IsDisposed`를 확인한다. 지속되는 접촉은 네이티브 접촉 그래프에 맡기고, 상대 목록은 요청 시 구성한다.

## Dispose와 소유권

### 소유 범위

| 소유 객체 | 폐기 효과 |
| --- | --- |
| 콜라이더 | 월드 등록과 강체 연결을 해제하고 해당 콜라이더를 제거한다. 부모 강체는 유지한다. |
| 강체 | 기본 `Dispose`는 연결된 콜라이더와 조인트도 제거한다. 원본의 콜라이더 보존 제거 옵션은 별도 제거 API로 제공하며, 보존된 콜라이더는 살아 있는 월드 객체로 유지한다. |
| 조인트·변형체 | 원본의 종속 관계를 따라 제거하고 실제로 제거된 구성 요소의 래퍼도 무효화한다. 변형체의 내부 강체를 독립된 소유 객체처럼 제거하지 않는다. |
| 월드 | 소유 객체와 월드 자원을 정리한다. 외부 게임 객체와 독립적으로 소유한 공유 도형 핸들은 폐기하지 않는다. |
| 공유 도형 | 해당 핸들의 참조만 해제한다. 이미 생성된 콜라이더가 보유한 참조는 유지되므로 계속 사용할 수 있다. |
| 콜백·훅 등록 객체 | 해당 등록과 대리자 참조를 해제한다. 연결 대상 객체 자체는 폐기하지 않는다. |

월드 소속 조회 객체와 컬렉션 접근 객체는 소유자를 폐기할 권한이 없는 접근 창구이다. 월드가 폐기되면 사용할 수 없다. 빌더가 공유 도형을 참조하고 있다면 삽입이 끝날 때까지 그 도형 핸들이 유효해야 한다. 삽입 후에는 콜라이더가 자신의 참조를 유지한다.

### 폐기 계약

1. `IsDisposed`를 즉시 `true`로 만들어 새로운 사용과 이벤트 연결을 거부한다.
2. 실제 제거와 네이티브 참조 해제를 완료하고 편의 조회·이벤트에서 해당 객체를 제외한다.
3. 등록된 `Disposed` 대리자를 한 번 호출한다.
4. 알림의 성공 여부와 무관하게 모든 이벤트·훅 대리자 참조를 해제한다.

동일 객체의 `Dispose` 재호출은 아무 작업도 하지 않는다. 폐기 후에는 상태 확인, 재폐기와 구독 해제를 허용하며 그 밖의 객체 사용과 새로운 구독은 `ObjectDisposedException`으로 거부한다. 같은 네이티브 객체를 가리키는 접근 경로에도 무효화가 반영되어야 한다.

계산·순회·콜백 중에는 폐기 요청을 기록하고 안전한 지점까지 실제 해제를 미룬다. 현재 최상위 `Step`, `DetectCollisions` 또는 `Dispose`가 끝나기 전에 정리하며, `Disposed`는 실제 정리가 끝난 뒤 전달한다. 월드 폐기 요청 이후에는 소유 객체의 새 충돌 콜백도 시작하지 않는다. 소유 객체의 폐기 알림을 마친 다음 월드의 폐기 알림을 전달한다.

콜백 예외가 네이티브 경계를 넘어 전파되지 않게 한다. 필요한 내부 정리와 예약된 폐기를 마친 뒤 호출자에게 전달한다. 폐기 알림에서 예외가 발생해도 나머지 구독자와 소유 객체의 정리를 계속하고, 여러 예외는 모아서 전달한다.

## 네이티브 구성과 배포

UniEngine 전용 Rust 연결 DLL은 차원·정밀도별로 나뉜다. 연결 프로젝트가 Rapier·Parry의 공개 Rust API를 직접 호출하고 월드·도형을 소유한다. 현재 검증본은 Rapier 로컬 수정본 0.36.1-r1과 수정하지 않은 Parry 0.31.1이다. 공식 C 바인딩을 수정하거나 런타임 의존성으로 사용하지 않는다. 관리 코드는 불러온 구성이 제공하는 차원·정밀도와 선택 기능만 노출하고, 없는 구성을 다른 구성으로 대신 실행하지 않는다.

[DLL 이주 계획](../../../../packages/rapier/docs/investigation/DLL%20Migration%20Plan.md)은 C# 객체 API를 UniEngine.dll에 포함하고, 전용 Rust 소스·빌드·제품 DLL 관리를 이 모듈의 Native 폴더로 옮기는 방향이다. 외부 의존성 자료와 문서는 packages/rapier에 남긴다. 이주는 아직 적용하지 않았으며 아래 배포 경로는 제품 구현의 목표이다.

| 항목 | 기준 |
| --- | --- |
| 관리 코드 | 저장소의 C# 플랫폼 기준에 따라 netstandard2.1과 C# 9로 작성한다. |
| 네이티브 구성 | 2D·3D와 f32·f64 조합별로 라이브러리를 구분한다. 결정론·SIMD처럼 함께 쓸 수 없는 빌드 구성은 서로 다른 라이브러리가 된다. |
| 빌드 | 현재 Rust 실험은 `packages/rapier`에 있다. 이주 후 `src/01-Infrastructure/03-M.4-PhysicalSpace/Native`에서 Cargo와 대상 링커로 빌드한다. Windows x64는 MSVC를 사용한다. C 소비 검증에는 C 컴파일러를 사용하며 CMake와 바인딩 생성기는 필수로 두지 않는다. 원본 의존성은 정확한 버전과 Cargo.lock으로 고정한다. 일반 `dotnet build`·`dotnet test`에는 Rust 빌드를 연결하지 않는다. |
| ABI 확인 | 최초 네이티브 호출에서 자체 ABI 검사로 연결 ABI 버전, 차원·정밀도·포인터 폭, C 구조체 크기·정렬·필드 배치와 빌드 구성을 확인한다. 불일치하면 월드를 만들기 전에 실패를 알린다. 실제 DLL 내보내기 목록과 해당 헤더를 대조하며 Rust 내부 구조체 배치를 C ABI로 가정하지 않는다. |
| 오류 | 연결 DLL은 상태 코드와 진단 정보를 제공하며 원본의 미지원·실패·교차 없음을 보존한다. Rust panic과 관리 콜백 예외가 C ABI를 넘지 않게 한다. 관리 코드는 상태를 확인하고 실패를 예외로 전달한다. |
| 본체와의 관계 | C# 공개 객체 API와 내부 P/Invoke는 본체 어셈블리(UniEngine.csproj)에 포함하며 별도 PhysicalSpace 관리 DLL을 추가하지 않는다. Windows 동적 링크에서는 해당 네이티브 API를 처음 호출할 때 DLL이 필요하며, PhysicalSpace를 사용하지 않는 본체 경로에서는 초기화하지 않는다. 이주 후 모듈의 `Native/dist`에 채택한 검증 DLL을 빌드·테스트·게시 출력에 복사한다. 다른 플랫폼의 정적 링크 요구는 지원 시 별도로 정한다. |
| 지원 플랫폼 | 1차 빌드·검증 플랫폼은 Windows x64이다. 다른 플랫폼은 빌드 구성만 확장할 수 있게 열어 두며, 채택 시 확정하여 기록한다. |

콜백은 정적 비제네릭 메서드와 유지되는 대리자 참조를 사용하며, 사용자 데이터 포인터에 GCHandle을 연결한다. Unity 안내에 따른 `AOT.MonoPInvokeCallbackAttribute` 표시를 컴파일된 관리 DLL에 보존하는 방식을 채택하고 실제 IL2CPP 호환성은 별도로 검증한다. 네이티브가 더 이상 콜백을 시작할 수 없는 시점까지 대리자와 GCHandle을 유지한다. 관리 콜백은 예외를 수집하고 네이티브 반환 후 정리를 마쳐 전달한다. 본체의 플랫폼 기준에 없는 NativeLibrary·UnmanagedCallersOnly는 사용하지 않는다. [Unity 관리 콜백 안내](https://docs.unity.com/en-us/engine/6000.7/manual/programming-environment/plug-ins/native/invoke/managed)

[Rapier 공개 월드 API](https://docs.rs/rapier2d/0.36.1/rapier2d/pipeline/struct.PhysicsWorld.html) · [Rapier 관리](../../00-Foundations/Rapier.md)

## 공개 API 초안

다음 표는 대표 진입점이며 전체 기능 목록은 API Coverage를 따른다. `2D` 예시와 대응되는 `3D` 타입을 함께 제공한다.

| 대상                             | 대표 멤버                                                                                                                                                                                    |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `PhysicsWorld2D : IDisposable` | `Gravity`, `IntegrationParameters`, `NarrowPhase`, `QueryPipeline`, `Step(float deltaTime)`, `DetectCollisions()`                                                                        |
| 월드 생성·제거                       | `InsertRigidBody(RigidBodyBuilder2D builder)`, `InsertCollider(ColliderBuilder2D builder, RigidBody2D parent = null)`, `RemoveRigidBody(RigidBody2D body, bool removeAttachedColliders)` |
| `RigidBody2D : IDisposable`    | `Handle`, `BodyType`, 자세·속도·질량 특성, 힘·충격량·운동학적 입력과 연결된 콜라이더                                                                                                                               |
| `Collider2D : IDisposable`     | `Handle`, `Parent`, 도형·자세·재질·센서·필터 설정, `GetCollidingColliders()`                                                                                                                         |
| 콜라이더 편의 이벤트                    | `event Action<IReadOnlyList<Collider2D>> CollisionStarted`, `CollisionStopped`                                                                                                           |
| 월드 원본 이벤트                      | `event Action<CollisionEvent2D> CollisionEvent`, 접촉력·변형체 이벤트                                                                                                                             |
| 자원 소유 객체                       | `IsDisposed`, `Dispose()`, `event Action Disposed`                                                                                                                                       |

C#의 표기 규칙에 맞게 메서드와 상수의 대소문자를 조정하되, 원본 개념을 다른 의미로 바꾸지 않는다. `Position`이 전체 자세를 뜻하는 원본 API와 병진 성분인 `Translation`도 구분한다. 전체 자세는 Transform2D·Transform3D로 반환하며, 이 타입의 Position 속성은 원본의 `Translation`에 대응하는 병진 성분이다.

## 대표 흐름

아래 코드는 객체 연결과 갱신 시점을 설명하는 API 초안이다. 아직 컴파일 가능한 패키지 예제가 아니다.

### 2D 센서와 현재 상대 조회

강체에 연결하지 않은 두 콜라이더는 고정으로 취급되므로 충돌 조합을 명시적으로 활성화한다. 이벤트를 구독하지 않아도 같은 충돌 조회를 사용할 수 있다.

```csharp
using System;
using BlackThunder.UniEngine.PhysicalSpace;

using var world = new PhysicsWorld2D();
var sensor = world.InsertCollider(
    ColliderBuilder2D.Cuboid(2f, 2f)
        .Sensor(true)
        .ActiveCollisionTypes(ActiveCollisionTypes.All)
        .ActiveEvents(ActiveEvents.CollisionEvents));
var ball = world.InsertCollider(
    ColliderBuilder2D.Ball(0.5f)
        .ActiveCollisionTypes(ActiveCollisionTypes.All));

sensor.CollisionStarted += others => Console.WriteLine(others.Count);
sensor.CollisionStopped += others => Console.WriteLine(others.Count);
ball.Disposed += () => Console.WriteLine("Ball disposed.");

world.DetectCollisions();
var colliding = sensor.GetCollidingColliders();

ball.Dispose();
var remaining = sensor.GetCollidingColliders();
world.DetectCollisions();
```

첫 조회에는 `ball`이 포함되고 폐기 후 새 조회에서는 제외된다. 폐기에 따른 편의 종료 알림은 발생하지 않는다. 별도로 구독한 월드 원본 이벤트에서는 네이티브가 생성한 제거 표시를 확인할 수 있다.

### 3D 강체와 여러 콜라이더

```csharp
using System;
using System.Numerics;
using BlackThunder.UniEngine.PhysicalSpace;

using var world = new PhysicsWorld3D();
var ground = world.InsertCollider(
    ColliderBuilder3D.Cuboid(5f, 0.5f, 5f)
        .Translation(new Vector3(0f, -0.5f, 0f)));
var body = world.InsertRigidBody(
    RigidBodyBuilder3D.Dynamic()
        .Translation(new Vector3(0f, 0.25f, 0f)));

using var shape = SharedShape3D.Ball(0.5f);
var mainCollider = world.InsertCollider(
    ColliderBuilder3D.New(shape), body);
var extraCollider = world.InsertCollider(
    ColliderBuilder3D.Cuboid(0.25f, 0.25f, 0.25f)
        .Translation(new Vector3(1f, 0f, 0f)), body);
shape.Dispose();

world.DetectCollisions();
var pairs = world.NarrowPhase.ContactPairsWith(mainCollider);
foreach (var pair in pairs)
{
    if (pair.HasAnyActiveContact)
        Console.WriteLine("Active contact.");
}

var colliding = mainCollider.GetCollidingColliders();
world.Step(1f / 60f);
body.Dispose();
world.Dispose();
```

공유 도형 핸들의 폐기 후에도 두 콜라이더와 월드는 살아 있다. 강체의 기본 폐기는 연결된 두 콜라이더를 함께 제거하고, 월드 폐기는 남은 바닥도 제거한다. `using`에 따른 재폐기는 아무 작업도 하지 않는다.

## 검토 기준

| 상황 | 충족해야 할 동작 |
| --- | --- |
| 후보 쌍만 있고 실제 접촉은 없음 | 편의 충돌 상대 목록에 포함하지 않는다. |
| 센서 겹침·복수 접촉점 | 실제 겹친 상대를 포함하고 동일 상대는 한 번만 반환한다. |
| 이벤트 비활성·미구독 | 충돌 검사 후 상태 조회는 계속 가능하다. |
| 검사 전·자세 변경 직후 | 접촉 조회와 공간 질의 모두 마지막 검사 기준임을 유지하고 명시적 충돌 검사 후 결과를 갱신한다. |
| 비동적 객체끼리의 감지 | 조합을 활성화하지 않으면 감지하지 않고, 강체가 없는 콜라이더는 고정 조합으로 판정한다. |
| 월드 감지의 미지원 도형 조합 | 직접 질의는 미지원을 구분하여 전달하고, 월드 결과는 원본과 같이 접촉·겹침 없음으로 남는다. |
| 네이티브 구성 불일치 | ABI 불일치나 불러오지 않은 차원·정밀도 요청을 사용 전에 오류로 알린다. |
| 개별 객체·월드 폐기 | 새 조회에서 제외하고 편의 종료 알림을 합성하지 않는다. 원본 제거 표시는 별도로 보존한다. |
| 공유 도형·중복 폐기·콜백 예외 | 다른 소유자의 자원을 해제하지 않고, 알림 중복이나 자원 누락 없이 정리한다. |
| 2D·3D·f32·f64와 회전 입력 | 차원·정밀도를 혼동하지 않고 자세·속도·조인트 각도 등의 단위를 일관되게 변환한다. |

[API Coverage](API%20Coverage.md) · [Rapier](../../00-Foundations/Rapier.md) · [Infrastructure](../README.md)
