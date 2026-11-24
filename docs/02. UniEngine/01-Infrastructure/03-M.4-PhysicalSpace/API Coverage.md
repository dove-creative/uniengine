# API Coverage

이 문서는 PhysicalSpace의 공통 API와 물리 소스별 대응을 정리한 설계 대응표이다. 모든 C# 멤버는 구현 예정이다. 대응은 문서와 공개 이름을 조사한 설계이며 동작의 동등성을 검증한 결과가 아니다.

## 목차

- 기준과 상태
- 공통 API
- 제공하지 않는 API
- 연결과 수명 제약
- 대응 완료 기준

## 기준과 상태

| 표기 | 의미 |
| --- | --- |
| 지원 | 소스의 기능으로 계약을 구현한다. |
| 조건부 | 소스의 설정이나 차원에 따라 제공하며 조건을 함께 적는다. |
| 확인 필요 | 구현 전에 대응과 동작 차이를 확인한다. |
| 미지원 | 호출하면 `NotSupportedException`으로 알린다. |

조사 기준일은 2026-11-24이다. 유니티는 공식 문서와 2022.3.60f1·6000.3.18f1에 설치된 물리 DLL의 이름을 확인했다.

공개 이름은 유니티를 우선하고, 유니티에 없는 개념은 UniEngine의 이름을 쓴다. 의미는 [PhysicalSpace](README.md)의 계약을 따르며 시점과 수명은 물리 소스를 따른다.

## 공통 API

| 기능 | 공개 이름 | 유니티 |
| --- | --- | --- |
| 씬 | `PhysicalSpace2D/3D` | 조건부. 유니티 Scene의 물리 씬을 얻으며 기본이 아닌 물리 씬은 Scene에 묶임 |
| 중력 | `Gravity` | 확인 필요. 물리 씬별 설정 가능 여부 |
| 갱신 | `Simulate` | 조건부. 시뮬레이션 모드가 Script일 때 |
| 강체의 운동 방식 | `BodyType` | 지원. 3D는 Rigidbody의 유무와 isKinematic으로 표현 |
| 자세 | `Position`, `Rotation`, `Pose` | 지원. 값 형식 변환 |
| 속도와 감쇠 | `LinearVelocity`, `AngularVelocity`, `LinearDamping`, `AngularDamping` | 지원. 2022.3은 이름이 다름. 3D 각속도는 라디안에서 변환 |
| 질량 | `Mass` | 지원 |
| 힘과 이동 | `AddForce`, `AddTorque`, `MovePosition`, `MoveRotation` | 지원 |
| 수면 | `Sleep`, `WakeUp`, `IsSleeping` | 지원 |
| 콜라이더와 트리거 | `UniCollider2D/3D`, `IsTrigger` | 지원 |
| 도형 | `Box`, `Circle`, `Sphere`, `Capsule` | 지원 |
| 도형 | `Polygon`, `Edge`(2D), `Mesh`(3D) | 확인 필요. 오목·볼록과 운동 방식의 제약 |
| 재질 | `Friction`, `Bounciness` | 확인 필요. 3D의 정지·운동 마찰 구분 |
| 충돌 필터 | 유니티의 레이어 이름 | 지원 |
| 비동적 조합 | `ActiveCollisionTypes` | 확인 필요. 지원하지 않는 조합은 미지원 |
| 이벤트 설정 | `ActiveEvents` | 조건부. 3D는 providesContacts, 트리거는 컴포넌트 콜백 |
| 접촉 이벤트 | `CollisionEnter`, `CollisionExit` | 조건부. 3D는 ContactEvent, 2D는 확인 필요 |
| 트리거 이벤트 | `TriggerEnter`, `TriggerExit` | 조건부. ContactEvent가 트리거를 알리지 않아 컴포넌트로 받음 |
| 접촉 상대 조회 | `GetCollidingColliders` | 확인 필요. 2D는 GetContacts, 3D는 접촉 이벤트의 누적 방법 |
| 광선·이동 질의 | `Raycast`, `BoxCast`, `CircleCast`, `SphereCast`, `CapsuleCast` | 지원 |
| 겹침 질의 | `OverlapBox`, `OverlapCircle`, `OverlapSphere`, `OverlapCapsule`, `OverlapPoint` | 지원 |
| 최근접점 | `ClosestPoint` | 확인 필요. 도형별 제약 |
| 조인트 | `Fixed`, `Hinge`, `Slider`, `Spring`, `Distance` | 확인 필요. 차원별 제공 조인트 |
| 기존 객체 감싸기 | 소스 진입점 | 지원. 감싼 객체의 폐기는 연결만 해제 |

## 제공하지 않는 API

| API | 이유 |
| --- | --- |
| `DetectCollisions` | 유니티 문서가 시간 간격 0의 동작을 설명하지 않는다. |
| `CollisionStay`, `TriggerStay` | 유지 중인 접촉을 매 갱신마다 알리지 않는다. `GetCollidingColliders`로 조회한다. |
| 소스 사이의 결과 일치 | 물리 엔진의 계산 방법이 달라 보장하지 않는다. |

## 연결과 수명 제약

- 공통 API는 소스의 포인터나 유니티 형식을 드러내지 않는다.
- 소스가 객체를 먼저 제거하면 대응하는 공개 핸들도 폐기 상태가 된다.
- 감싼 유니티 객체의 폐기는 연결 해제이며 유니티 객체를 제거하지 않는다.
- 공개 각도·각속도는 도 단위이며 소스의 단위는 각 전략이 변환한다.
- 다른 소스의 객체를 씬에 전달하면 오류로 거부한다.

## 대응 완료 기준

이 표는 기능군 수준의 설계이다. 구현할 때 각 행을 공개 멤버·옵션·결과로 확장하고 소스별 대응과 검증 근거를 남긴다. 다음을 만족하기 전에는 해당 소스가 공통 API를 완전히 제공한다고 표시하지 않는다.

- '확인 필요' 항목을 조사하여 지원·조건부·미지원으로 분류한다.
- 미지원 기능이 빈 결과나 다른 기능으로 대신되지 않는다.
- 접촉 상대 조회가 후보 쌍을 실제 충돌로 오인하지 않고 트리거 겹침과 복수 콜라이더를 올바르게 표현한다.
- 폐기 상태와 알림 횟수가 공통 계약을 충족하고 소스별 제거 시점이 문서와 일치한다.
- 유니티 소스는 지원하는 유니티 버전의 컴파일러로 컴파일하고 유니티에서 실행하여 확인한다.

[PhysicalSpace](README.md) · [구조 결정](DLL%20Structure%20Decision.md) · [Infrastructure](../README.md)
