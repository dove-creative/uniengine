# API Coverage

이 문서는 PhysicalSpace가 제공할 Rapier·Parry 공개 기능과 C# 연결 방향을 정리한 설계 대응표이다. 모든 C# 대응은 구현 예정이며, 표의 연결 경로는 구현 완료나 전체 멤버의 동등성 검증을 의미하지 않는다.

0단계의 실제 내보내기와 원본 멤버별 조사·실패 상태는 [네이티브 대응 조사](Native%20Coverage.md)에 기록한다.

현재 0단계 빌드는 Rapier 0.36.1에 [로컬 수정본 0.36.1-r1](../../../../packages/rapier/docs/revisions/0.36.1-r1/Plan.md)을 적용하고, Parry 0.31.1은 수정하지 않는다. 충돌 검사 이후 운동·수면 전환 회귀 검증을 통과했지만, 이 대응표의 C# 구현이나 전체 공개 기능 검증 완료를 뜻하지 않는다.

## 목차

- 기준과 상태
- Rapier 기능
- 도형
- Parry 질의와 기하 처리
- 정밀도와 선택 기능
- 연결과 수명 제약
- 대응 완료 기준

## 기준과 상태

| 항목 | 설명 |
| --- | --- |
| 개요 | 원본 기능을 C#에서 누락 없이 이용하기 위한 기능군·공개 진입점 대응표이다. |
| 목적 | 편의 API 때문에 원본 기능이 가려지거나 연결 DLL의 현재 구현 범위가 최종 지원 범위로 오인되지 않게 한다. |
| 구현 | Rapier·Parry를 고정 버전으로 참조하는 별도 Rust 연결 DLL에서 공개 API를 자체 C ABI로 제공한다. 로컬 수정 여부와 출처는 수정본 기록으로 관리한다. |

조사 기준일은 2026-11-23이며, 확인한 원본 저장소의 버전은 Rapier 0.36.1과 Parry 0.31.1이다. Cargo의 정확한 버전 참조와 Cargo.lock으로 원본·전이 의존성을 고정하고 자체 연결 ABI·빌드 옵션을 별도로 관리한다. 아래 문서는 고정 버전의 공개 API를 대조 기준으로 삼는다. [Rapier 저장소](https://github.com/dimforge/rapier) · [Rapier API](https://docs.rs/rapier2d/0.36.1/rapier2d/) · [Parry API](https://docs.rs/parry2d/0.31.1/parry2d/)

| 연결 표기 | 의미 |
| --- | --- |
| 직접 연결 | 원본 공개 Rust API를 UniEngine 소유의 C 함수·POD 결과·핸들로 노출한다. |
| 대리자 연결 | Rust trait·확장 지점을 C 함수 포인터와 사용자 데이터로 연결한다. |
| 빌드 구성 | 차원·정밀도·feature를 별도 DLL 구성과 자체 ABI 정보로 구분한다. |

기능군 표에는 원본의 공개 타입·함수와 C# 대응을 함께 기록한다. C#에서는 snake_case를 PascalCase로 바꾸고 차원별 타입에 `2D/3D`를 붙인다. Rust의 trait·제네릭·iterator 문법 자체를 복제하는 대신 동등한 기능을 타입, 결과 목록과 대리자로 제공한다. 사용자에게 인터페이스 구현을 필수로 요구하지 않는다.

'전체 기능'은 선택한 버전에서 지원하는 라이브러리 동작을 뜻한다. 차원 전용 기능, 특정 빌드에 없는 기능, 지원하지 않는 도형 조합을 구분한다. 데모·testbed·외부 엔진 플러그인·별도 로더 패키지의 전체 API는 이 두 라이브러리의 기능과 구분한다.

## Rapier 기능

이 절의 차원 공통 행은 2D·3D에 각각 대응한다. 세부 멤버의 차원 제한은 원본을 따른다. 전체 공개 구조는 [Dynamics](https://docs.rs/rapier2d/0.36.1/rapier2d/dynamics/index.html), [Geometry](https://docs.rs/rapier2d/0.36.1/rapier2d/geometry/index.html), [Pipeline](https://docs.rs/rapier2d/0.36.1/rapier2d/pipeline/index.html)을 대조 기준으로 한다.

| 기능군 | 원본 공개 진입점 | C# 대응·제공 범위 | 차원·조건 | 연결 |
| --- | --- | --- | --- | --- |
| 월드·등록·식별 | `PhysicsWorld`, `RigidBodySet`, `ColliderSet`, 각 `Handle` | 월드 생성, 삽입·제거·열거, 핸들 조회, 사용자 데이터, 일괄 상태 읽기 | 공통 | 직접 연결 |
| 강체 | `RigidBody`, `RigidBodyBuilder`, `RigidBodyType` | 자세·병진·회전, 선형·각속도, 중력 배율, 감쇠, 힘·토크·충격량, 운동학적 목표, 잠금 축·활성·수면·우선 관계 | 공통 | 직접 연결 |
| 질량 특성 | `MassProperties`, 강체·콜라이더의 질량 API | 질량·밀도·무게중심·관성, 추가 질량과 재계산을 원본 의미로 구분 | 2D·3D의 관성 표현 차이 유지 | 직접 연결 |
| 콜라이더 | `Collider`, `ColliderBuilder`, `SharedShape` | 도형 변경, 부모와 상대 자세, 센서, 활성 상태, 마찰·반발·결합 규칙, 접촉 관련 설정 | 공통 | 직접 연결 |
| 충돌·솔버 필터 | `InteractionGroups`, `ActiveCollisionTypes`, `ActiveEvents`, `ActiveHooks` | 충돌 그룹·솔버 그룹·강체 조합·이벤트 활성화를 각각 설정 | 공통 | 직접 연결 |
| 시뮬레이션 | `PhysicsPipeline`, `IntegrationParameters`, `IslandManager`, `CCDSolver` | 물리 갱신, 적분·솔버 설정, 활성 강체, CCD 설정 | 공통, 선택 기능별 차이 | 직접 연결 |
| 충돌 전용 갱신 | `CollisionPipeline` | `DetectCollisions`, 운동을 진행하지 않는 접촉·질의 인덱스 갱신 | 공통 | 직접 연결 |
| 접촉 관계·상세 | `NarrowPhase`, `ContactPair`, 접촉·교차 그래프 | `ContactPairsWith`, `IntersectionPairsWith`, 단일 쌍 조회, 접촉점·법선·깊이·충격량과 원본 결과 필드 | 후보 관계와 실제 접촉 구분 | 직접 연결 |
| 공간 질의 | `QueryPipeline`, `QueryPipelineMut`, `QueryFilter` | ray·shape cast, 점·도형 교차, 점 투영·근접 질의, 필터·제외 객체·술어 | 공통, 질의별 도형 제한 | 직접 연결 |
| 이벤트 | `CollisionEvent`, `ContactForceEvent`, `SoftBodyTearEvent`, `EventHandler`, `ChannelEventCollector` | 개별 원본 이벤트, 수집·읽기·비우기, 대리자 기반 즉시 콜백과 사후 알림 | 원본 플래그·결과 정보 보존 | 직접 연결·대리자 연결 |
| 계산 중 훅 | `PhysicsHooks`, 필터·접촉 수정 문맥 | 필터와 솔버 접촉 수정 대리자, 문맥 수명과 반환 의미 보존 | 병렬 구성에서는 스레드 안전 필요 | 대리자 연결 |
| 조인트 | `ImpulseJointSet`, `MultibodyJointSet`, `GenericJoint`, 원본의 개별 조인트·빌더 | 제약 축·한계·모터·접촉 설정, 연결·제거, 다물체 계층과 원본의 운동학·결합 기능 | 조인트·자유도별 차원 제한 | 직접 연결 |
| 컨트롤러 | `KinematicCharacterController`, `PdController`, `PidController`, `DynamicRayCastVehicleController` | 캐릭터 이동·경사·계단·접지 결과, 제어기 입력·출력, 차량·바퀴 설정 | 차량은 3D, 나머지도 도입 버전별 대조 | 직접 연결 |
| 변형체 | `SoftBody`, `SoftBodyBuilder`, `SoftBodySet`와 구성 타입 | 입자·요소·재질·제약·클러스터, 생성·변형·분리·절단과 이벤트 | 2D·3D 구성 차이, FEM 선택 조건 | 직접 연결 |
| 저장·복원 | 직렬화 가능한 공개 상태, 원본 스냅샷 API | 상태 직렬화·복원과 버전 정보, 복원 객체의 새로운 수명 관리 | `serde-serialize` 등 구성 조건 | 직접 연결 |
| 관찰·디버그 | `DebugRenderPipeline`, `DebugRenderBackend`, `Counters` | 렌더링 데이터를 받을 대리자, 표시 설정, 성능 카운터 | `debug-render`, `profiler` 등 | 직접 연결 |
| 독립 상태·확장 | 공개 데이터 구조, 독립 파이프라인·set, 사용자 질의 dispatcher, 기능성 `utils` | 월드 통합 경로 외에 원본 공개 알고리즘·상태 접근이 필요한 기능도 노출 | 소유권·불변식 유지 | 직접 연결 |

연결 DLL은 Rapier의 `PhysicsWorld`를 직접 생성하고 공개된 set·파이프라인·접촉 상태를 이용한다. C#의 월드 소속 `NarrowPhase`·`QueryPipeline`은 그 월드의 수명을 따르는 접근 창구이다. 독립적인 set·파이프라인도 원본 공개 API를 연결하되 소유권과 무효화를 함께 관리한다. 공식 C 바인딩의 비공개 내부 구조에 접근할 필요가 없다. [Rapier 공개 월드 API](https://docs.rs/rapier2d/0.36.1/rapier2d/pipeline/struct.PhysicsWorld.html)

조인트의 예로 `FixedJoint`, `RevoluteJoint`, `PrismaticJoint`, `RopeJoint`, `SpringJoint`, `GenericJoint`와 차원 전용 조인트가 있다. 이 예시 목록으로 지원 범위를 닫지 않고 도입 버전의 모든 공개 조인트·빌더·결과를 대응시킨다. 컨트롤러와 변형체도 [Control](https://docs.rs/rapier3d/0.36.1/rapier3d/control/index.html)과 Dynamics의 공개 항목을 기준으로 한다.

## 도형

도형 이름은 기하를 뜻하며 콜라이더·강체의 소유권을 대신하지 않는다. 아래 C# 대응은 원본 이름에 차원 접미사를 적용한다. 생성·검사·변환·세부 접근 모두 원본 공개 Rust API를 직접 연결한다. 공식 C 바인딩의 도형 지원 여부로 범위를 제한하지 않는다.

| 원본·C# 기본 이름 | 2D에서의 의미 | 3D에서의 의미 | 연결·제약 |
| --- | --- | --- | --- |
| `Ball` | 채워진 원 | 구 | 직접 연결, 반지름 입력 |
| `Cuboid` | 직사각형 | 직육면체 | 직접 연결, 원본의 반크기 입력 유지 |
| `Capsule` | 캡슐 영역 | 캡슐 입체 | 직접 연결, 축·끝점 표현 유지 |
| `Segment` | 선분 | 공간의 선분 | 직접 연결, 두께 없는 기하와 물리 설정 구분 |
| `Polyline` | 연결된 선분들 | 공간의 연결된 선분들 | 직접 연결, 꼭짓점·인덱스와 원본 플래그 |
| `Triangle` | 삼각형 영역 | 삼각형 표면 | 직접 연결, 차원에 따른 내부 의미 구분 |
| `TriMesh` | 삼각형들의 기하 | 삼각형 메시 표면 | 직접 연결, 인덱스·위상·플래그·연결 성분 |
| `ConvexPolygon` | 볼록 다각형 | 해당 없음 | 직접 연결, 임의의 오목 다각형과 구분 |
| `ConvexPolyhedron` | 해당 없음 | 볼록 다면체 | 직접 연결, 유효성·퇴화 조건 유지 |
| `Cylinder`, `Cone` | 해당 없음 | 원기둥·원뿔 | 직접 연결, 3D 전용 |
| `Tetrahedron` | 해당 없음 | 사면체 | 직접 연결, 원본 도형·질의 범위 |
| `HalfSpace` | 무한 직선 한쪽의 반평면 | 무한 평면 한쪽의 반공간 | 직접 연결, 경계면 자체와 구분 |
| `HeightField` | 높이 자료로 정의한 지형선 | 격자 높이 자료의 지형 표면 | 직접 연결, 저장 배열·스케일·셀 상태 차이 |
| `Compound` | 하위 도형의 조합 | 하위 도형의 조합 | 직접 연결, 하위 도형의 로컬 자세·구성 제약 |
| `RoundShape`와 구체 별칭 | 둥근 경계를 가진 지원 도형 | 둥근 경계를 가진 지원 도형 | 직접 연결, 원본이 제공하는 조합·별칭 전체 |
| `Voxels`와 관련 타입 | 균일 격자 셀 기하 | 균일 복셀 기하 | 직접 연결, 공유 도형·셀 데이터·상태 접근 |
| `SharedShape` | 공유 도형 참조 | 공유 도형 참조 | 직접 연결, 생성·공유·복제·참조 해제 |
| `ShapeType`, `TypedShape`, `FeatureId` 등 | 도형·기하 특징 식별 | 도형·기하 특징 식별 | 직접 연결, 형식·하위 도형·질의 결과의 식별 보존 |

도형별 지원은 [Parry 2D Shape](https://docs.rs/parry2d/0.31.1/parry2d/shape/index.html)와 [3D Shape](https://docs.rs/parry3d/0.31.1/parry3d/shape/index.html)를 따른다. `Ray`와 점 질의는 도형 콜라이더와 별도의 질의 입력이다. 유한 도형, 무한 영역과 퇴화 도형의 모든 조합이 동일하게 지원된다고 가정하지 않는다.

## Parry 질의와 기하 처리

Rapier 월드가 없어도 도형과 자세를 입력하여 Parry를 사용할 수 있게 한다. `Query2D/3D`, `Transformation2D/3D` 등은 원본 모듈의 함수를 묶는 C# 진입점이다. 아래 기능은 Parry 공개 Rust API를 직접 연결하며 Rapier 월드와 독립된 공유 도형·작업 공간도 제공한다.

| 기능군 | 원본 진입점 | C# 대응 | 차원·보존할 의미 |
| --- | --- | --- | --- |
| 점·반직선 | `PointQuery`, `PointQueryWithLocation`, `RayCast`, `Ray` | 포함·투영·최근접 특징, ray cast와 교차 결과 | 공통, solid 옵션·최대 거리와 특징 식별 |
| 두 도형 관계 | `intersection_test`, `distance`, `closest_points`, `contact` | `IntersectionTest`, `Distance`, `ClosestPoints`, `Contact` | 공통, 허용 거리·접촉·분리·미지원 구분 |
| 연속 이동 질의 | `cast_shapes`, `cast_shapes_nonlinear`, `NonlinearRigidMotion`, `ShapeCastOptions` | `CastShapes`, `CastShapesNonlinear`와 대응 옵션·결과 | 선형 이동과 회전을 포함한 운동 구분 |
| sweep·충돌 시각 | `sweep_time_of_impact`, `Sweep`, `SimplexCache`와 관련 타입 | 대응 함수·운동 입력·캐시·결과 상태 | 지원 기하·복합 도형의 제한 보존 |
| 지속 접촉 | `ContactManifold`, `ContactManifoldsWorkspace`, `TrackedContact` | 접촉 다양체, 작업 공간과 지속 질의 | 원본 결과와 작업 공간 수명 유지 |
| 질의 확장 | `QueryDispatcher`, `PersistentQueryDispatcher`, `SupportMap`, `CompositeShape` 등 | 제공 객체에 질의·지지점·하위 도형 접근 대리자를 등록 | Rust trait 동작을 연결하며 사용자 상속을 필수화하지 않음 |
| 전문 질의·알고리즘 | 공개 `query::details`, `gjk`, `epa`, `sat` | 원본의 공개 알고리즘·특정 도형 쌍 진입점 | 일반 질의 외의 설정·결과·캐시도 대상 |
| 경계·공간 인덱스 | `bounding_volume`, `partitioning` | AABB·경계 구 등, 원본 트리의 구축·갱신·탐색 | 공통, 버전별 인덱스 타입·visitor 대응 |
| 질량·수학 | `mass_properties`, `math`의 기능성 공개 API | 도형 질량 특성, 자세·회전·변환과 수치 표현. f32 자세는 Transform2D·Transform3D로 대응 | 정밀도 유지, 각도 변환 규칙 적용 |
| 볼록화·분해 | `convex_hull`, 2D 분해 함수, `vhacd` | 볼록 껍질·분해와 모든 공개 옵션·결과·오류 | 차원과 원본 선택 기능에 따름 |
| 메시 생성·변환 | 도형의 공개 변환 메서드, `transformation::utils`, `voxelization`, `volume_mesh` | 메시·선분화, 복셀화, 영역 채움과 관련 설정 | 2D 삼각형·3D 사면체 등 결과 차이 |
| 기하 교차·위상 | `polygons_intersection`, `convex_polygons_intersection`, 3D `intersect_meshes`와 허용오차 타입 | 다각형·메시 교차, 허용오차·위상 결과와 오류 | 함수별 지원 차원·입력 조건 보존 |
| 기타 공개 기능 | 위 행에 포함되지 않은 기능성 `utils`·도형 메서드·결과 타입 | 원본 공개 항목 대조 시 별도 대응 행으로 확장 | 미분류를 미지원이나 검증 완료로 취급하지 않음 |

기하 질의는 [Parry Query](https://docs.rs/parry2d/0.31.1/parry2d/query/index.html), 변환은 [2D Transformation](https://docs.rs/parry2d/0.31.1/parry2d/transformation/index.html)과 [3D Transformation](https://docs.rs/parry3d/0.31.1/parry3d/transformation/index.html)를 대조한다. 미지원 도형 조합·계산 실패·교차 없음은 서로 다른 결과이다. 단, 월드의 접촉·겹침 계산은 원본처럼 미지원 조합을 접촉·겹침 없음과 구분하지 않는다.

## 정밀도와 선택 기능

| 구성 | 제공 원칙·조건 | 연결 |
| --- | --- | --- |
| 2D·3D | 각각의 네이티브 라이브러리와 C# 타입을 구분한다. | 차원별 빌드 구성과 직접 연결 |
| f32·f64 | 양쪽의 기능 접근을 목표로 하며, f64 벡터·자세·배열을 f32로 축소하지 않는다. f32 자세는 Transform2D·Transform3D를, f64 자세는 별도 배정밀도 타입을 사용한다. | 정밀도별 C ABI와 수학 타입 |
| 병렬 실행 | 빌드 지원 여부와 스레드 풀 설정을 노출한다. 계산 중 콜백의 스레드 요구를 표시한다. | 직접 연결 |
| 결정론·SIMD | `enhanced-determinism`, `simd8` 등 빌드 구성을 구분한다. 동시에 허용되지 않는 조합을 하나의 보장으로 묶지 않는다. | 네이티브 빌드 조건 |
| 직렬화 | 상태 저장·복원 기능과 도입 버전·정밀도·구성 호환성을 명시한다. | 직접 연결 |
| 디버그·계측 | `debug-render`, `profiler` 등 선택 기능의 사용 가능 여부와 진입점을 제공한다. | 직접 연결 |
| 솔버 | `block-solver`, `fem` 등 원본의 지원 조건과 실험적 상태를 유지한다. | 빌드 설정 + 원본 런타임 옵션 |
| 기타 feature | 도입 버전의 전체 feature를 대조하여 런타임 기능, 네이티브 빌드 조건, Rust 전용 연동·개발 기능으로 구분한다. | 미분류 항목을 남기지 않음 |

기본 설명과 예시는 f32 기준이다. f64·병렬·결정론·SIMD·FEM 등의 API가 한 바이너리에서 항상 동시에 활성화된다는 뜻은 아니다. 실제 로드한 네이티브 구성의 사용 가능 기능을 확인할 수 있어야 하며, 없는 기능을 조용히 생략하거나 다른 정밀도로 실행하지 않는다. [네이티브 구성과 관리](../../00-Foundations/Rapier.md) · [Rapier 기능 설정](https://rapier.rs/docs/user_guides/rust/getting_started/)

## 연결과 수명 제약

- 일반 C# 객체는 월드·네이티브 핸들을 감싼다. 원본의 `Handle` 값과 소유권을 혼동하지 않는다.
- 미연결 기능은 네이티브 포인터를 사용자에게 맡기는 것으로 지원 처리하지 않는다. 모든 자체 C ABI가 같은 무효화·정밀도·단위·오류 계약을 따른다.
- 직접적인 set·파이프라인 조작과 월드 제거가 C# 객체의 생존 상태를 우회하지 않게 한다. 소유자·종속 객체가 바뀌면 관련 래퍼도 갱신한다.
- 공유 도형, 독립 질의 작업 공간과 월드 소속 접근 객체의 수명을 구분한다. 공유 참조 한 개의 폐기가 다른 사용자의 참조를 제거하지 않는다.
- 스냅샷은 네이티브 상태를 저장하며 C# 대리자와 외부 게임 객체를 직렬화하지 않는다. 복원 시 래퍼와 이벤트 연결을 새로 구성하고 이전 핸들과 새 객체를 혼동하지 않는다.
- 원본 이벤트의 제거·센서 표시와 상세 결과를 보존한다. 선택적 상대 목록 알림과 `GetCollidingColliders`만 래퍼 편의 기능으로 구분한다.
- 일반 알림은 갱신 후 전달하고, 원본 시점의 이벤트·필터·접촉 수정은 계산 중 동기 대리자로 제공한다. 계산 중에는 문맥이 허용하는 접근만 수행한다.
- 공개 각도·각속도와 조인트의 각도 입력은 도 단위이며 네이티브 경계에서 변환한다. 원본 스냅샷·내부 네이티브 메모리를 C# 공개 각도 값으로 오인하여 변환하지 않는다.

## 대응 완료 기준

이 표는 기능군 수준의 설계이다. 바인딩을 구현할 때는 각 행을 도입 버전의 공개 타입·메서드·옵션·결과 필드로 확장하여 자체 연결 DLL의 C 함수 및 C# 멤버와 연결한다. 각 항목에 차원·정밀도·feature 조건과 검증 근거를 남긴다.

다음 조건을 만족하기 전에는 패키지가 원본 전체 기능을 제공한다고 표시하지 않는다.

- 공개 기능을 대조하여 미분류·미연결 항목이 없다. 원본 미지원·차원 전용·빌드 전용 조건은 구분되어 있다.
- 입력 옵션과 결과 정보를 축소하지 않는다. 도형 질의의 미지원 결과, 접촉 상세, 이벤트 플래그와 오류가 보존된다.
- 일반 조회가 후보 쌍을 실제 충돌로 오인하지 않고, 센서 겹침과 복수 콜라이더도 올바르게 표현한다.
- 2D·3D, f32·f64와 필요한 선택 구성에서 원본 대비 동작과 단위 변환을 확인한다.
- 자원 제거·공유·복원·콜백 예외에서 래퍼와 네이티브 수명이 일치한다.

[PhysicalSpace](README.md) · [Infrastructure](../README.md)
