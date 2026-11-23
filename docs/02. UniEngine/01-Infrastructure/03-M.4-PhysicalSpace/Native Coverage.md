# 네이티브 대응 조사

0단계에서 별도 Rust 연결 DLL로 확인한 원본 공개 API와 실험용 C 진입점이다. 모든 C# 멤버는 아직 미구현이다. `ue_probe_*`는 재현·ABI 실험용이며 제품 API의 대응 완료를 뜻하지 않는다.

## 범위와 근거

Rapier 0.36.1·Parry 0.31.1의 2D/3D × f32/f64 네 조합을 사용한다. 현재 검증본은 Rapier 로컬 수정본 0.36.1-r1이며 Parry는 원본이다. 기본 feature를 유지하고 전체 feature·멤버 대조는 이후 단계에서 확장한다. [실행 결과](../../../../packages/rapier/docs/investigation/03.%20Results.md)에 원본·대안·수정본의 성공·실패와 체크섬을 구분한다.

## 원본 API 접근

| 원본 공개 항목 | 연결 함수·실험 | C# 멤버 | 차원·정밀도·조건 | 검증 상태 |
| --- | --- | --- | --- | --- |
| `PhysicsWorld::new`, `PhysicsWorld::insert` | `ue_probe_run`, 직접 Rust 재현 | 미구현 | 네 조합, 기본 feature | 직접 소유 월드 생성·삽입 가능 |
| `PhysicsWorld::{gravity,integration_parameters,physics_pipeline,collision_pipeline,islands,broad_phase,narrow_phase,bodies,colliders,impulse_joints,multibody_joints,soft_bodies,ccd_solver}` | `ue_probe_run` | 미구현 | 네 조합 | 별도 크레이트에서 공개 구성 요소 접근 컴파일 확인 |
| `RigidBodySet::{insert,remove,contains}`, `RigidBody::{translation,linvel,mass,is_sleeping}` | `ue_probe_run`, 직접 Rust 재현 | 미구현 | 네 조합 | 등록·제거·이전 핸들 거부와 상태 읽기 확인 |
| `ColliderSet::{insert,insert_with_parent}`, `ColliderBuilder::{new,ball,sensor,active_collision_types}` | `ue_probe_run` | 미구현 | 네 조합 | 부모 연결·독립 센서와 비동적 조합 활성화 확인 |
| `SharedShape::{ball,clone,as_ball}`, `Ball::new` | `ue_probe_run` | 미구현 | 네 조합 | 원래 공유 참조 해제 후 복제 참조와 콜라이더 유지 확인 |
| `PhysicsWorld::step` | `ue_probe_run` | 미구현 | 네 조합 | 새 동적 강체의 중력 낙하 확인 |
| `PhysicsWorld::detect_collisions`, `NarrowPhase::intersection_pair` | `ue_probe_run`, 직접 Rust 회귀 | 미구현 | 네 조합 | 독립 센서 겹침 갱신과 수정본의 검사 후 Step 운동·상태 전환 확인. 원본 실패는 아래 비교 기록 |
| `parry::query::distance`, `ShapeDistance` | `ue_probe_run`의 관찰 값 | 미구현 | 네 조합, Ball 쌍 | 거리 2 확인. 제품용 대응에서는 distance·subshape1·subshape2 전체를 보존해야 함 |

## 자체 C ABI

| 실제 DLL 내보내기 | 역할 | C# 멤버 | 검증 |
| --- | --- | --- | --- |
| `ue_probe_get_abi` | 실험 ABI 구성·POD 배치 조회 | 미구현 | 네 DLL 모두 C 헤더와 대조 |
| `ue_probe_check_abi` | 독립적으로 구성한 기대 ABI 비교 | 미구현 | 15개 descriptor 필드 각각 불일치 거부 |
| `ue_probe_run` | 원본 공개 API 사용 결과 관찰 | 미구현 | 네 DLL C 소비 실행, 잘못된 입력과 출력 버퍼 보존 |
| `ue_probe_callback` | 함수 포인터·사용자 데이터 왕복 | 미구현 | C 콜백 결과와 명시적 실패 보존. 실제 물리 훅·관리 콜백은 미검증 |
| `ue_probe_last_error` | 스레드별 진단 조회 | 미구현 | 실패 진단, 스레드 분리, 성공 시 초기화 |
| `ue_probe_panic` | 실험용 panic 주입 | 제품 API 대상 아님 | unwind가 C 경계를 넘지 않고 상태 코드 3으로 반환 |

## 갱신 계약의 원본 실패와 로컬 수정

원본의 새 동적 강체는 `DetectCollisions → Step`에서 정지했고 Step만 호출한 대조 사례는 낙하했다. 원본 PhysicsPipeline의 dt=0 대안은 첫 Step 전 수면 설정에서 한 번 이동했다. 원본 실패 로그를 보존하고, PhysicsWorld 검사에서 실제 물리 상태 갱신과 persistent island 초기화를 완료하는 [수정본 0.36.1-r1](../../../../packages/rapier/docs/revisions/Revisions.md)을 적용했다.

수정본은 네 구성에서 Rust Release 76개·Debug 회귀 52개와 C assertion 204개를 통과했다. 검사만 512번 호출한 경우의 자세·속도·시간 설정 보존, 삽입·편집·수면·활성·유형·질량·부모·impulse joint·제거·이벤트를 직접 검증했다. 일반 CollisionPipeline 직접 호출, multibody joint의 상세 상태 전이, soft body·선택 feature·제품 C# 연결은 검증 범위에 포함하지 않는다. [적용 계획](../../../../packages/rapier/docs/revisions/0.36.1-r1/Plan.md)과 [결과](../../../../packages/rapier/docs/investigation/03.%20Results.md)를 참조한다.

[API Coverage](API%20Coverage.md) · [PhysicalSpace](README.md)
