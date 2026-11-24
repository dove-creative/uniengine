# Spatial System 이슈 기록

PhysicalSpace를 제거하고 CollisionSpace만 남긴 구현의 미해결 항목이다. 2026-11-24 23시 44분의 소스를 복사하여 확인하였다. 아래 관찰 결과와 실행 수치는 해당 스냅샷 기준이며 Unity는 실행하지 않았다. 2026-11-25 수정 상태를 각 항목에 추가하였고, 수정본은 정적 분석과 전체 .NET 테스트를 수행하였다. Unity는 실행하지 않았다. 해결한 항목은 문서에서 지우며, 항목 번호는 처음 부여한 값을 유지하고 다시 사용하지 않는다.

## 목차

- 확인 범위

- 우선 처리
    - SP-04 쌍 정리가 native 종료 알림보다 먼저 실행됨
    - SP-05 남은 쌍 기록에 의한 시작 알림 누락
    - SP-07 객체 하나의 실패가 월드 진행을 막음

- 후속 처리
    - SP-08 등록되지 않은 강체를 내부 강체로 전환

- Unity 실행 확인 항목
- 제외한 항목

## 확인 범위

| 확인 | 결과 |
| --- | --- |
| 본체 Release 빌드 | 경고 0, 오류 0 |
| SpatialSystem 범주 .NET 테스트 | 34건 통과, 2D·3D seed 0·1·2가 각각 깊이 100 도달 |
| Unity 래퍼 컴파일 분석 | 공통 소스와 함께 분석하면 2022.3.60f1·6000.3.18f1 모두 오류 0, 경고 0 |
| 전체 .NET 테스트 | 미실행 |
| Unity PlayMode | 미실행 |
| 이전 기록 | PhysicalSpace를 포함한 구현에서 빌드 성공·36건 통과. 현재 소스의 결과로 사용하지 않는다. |

근거는 다음과 같이 구분한다.

| 근거 | 의미 |
| --- | --- |
| 재현 | 현재 소스에서 .NET 또는 컴파일러 실행으로 결과를 확인하였다. |
| 코드 | 소스의 실행 경로로 확인하였다. Unity에서 실행하지는 않았다. |
| 공식 문서 | Unity 문서의 설명에 근거한다. |
| Unity 실행 필요 | 엔진의 실제 동작을 확인해야 결론이 난다. |

## 우선 처리

### SP-04 쌍 정리가 native 종료 알림보다 먼저 실행됨

**2026-11-25 수정 상태.** 공통 Update에 최종 설정 적용·종료 보완·관계 식별자 검사를 반영하였다. 이전 관계의 종료를 새 관계에 적용하지 않는 공통 사례를 작성하였다. Unity 수신 순서와 실제 전환은 실행 검증 대기이다.

**결정한 규칙.** [Rules](02.%20Rules.md)에 다음 Update의 최종 설정 적용, 비활성화 시 종료 보장, Trigger 관계 전환, Disposed를 통한 수명 종료와 Source 직접 변경의 관측 기준을 정리하였다. 중복 종료와 이전 관계의 지연 알림을 구분하는 공통 구현을 반영했으며 Unity 검증은 남아 있다. 아래 관찰 결과는 수정 전 스냅샷의 문제이다.

**근거.** 재현(공통 계층의 처리 순서), Unity 실행 필요(native 알림의 발생과 순서)

**관찰 결과.** Enabled 해제와 IsTrigger 변경은 쌍 정리를 즉시 큐에 넣는다. 그 변경으로 native가 다음 단계에 보내는 종료 알림은 정리 뒤에 놓인다. 공통 계층에 같은 순서를 재현한 결과는 다음과 같다.

| 처리 순서 | 소비자가 받은 알림 |
| --- | --- |
| 시작 → 정리 → 종료 | 시작 |
| 시작 → 정리 → 새 시작 → 이전 형상의 종료 | 시작, 시작, 종료 |

첫 번째 경우 종료 알림이 사라지고, 두 번째 경우 실제로는 접촉 중인데 종료를 받는다. 3D Trigger는 비활성화 때 [종료 메시지를 보내지 않으므로](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnTriggerExit.html) 해당하지 않는다. ContactEvent로 받는 3D 접촉, 2D 알림과 IsTrigger 변경은 Unity에서 확인해야 한다.

### SP-05 남은 쌍 기록에 의한 시작 알림 누락

**2026-11-25 수정 상태.** 관계 정리를 공통 공간이 맡고 ColliderBindings.IsActive로 외부 비활성 상태를 관측한다. ForgetPairs와 Unity 직접 호출을 제거하였다. 재활성화와 같은 Update 전 반복 변경의 공통 사례는 실행하여 통과하였고 Unity는 실행 검증 대기이다.

**근거.** 재현(공통 계층), 공식 문서(6000.0·6000.3)

**관찰 결과.** 공통 공간은 이미 기록된 쌍의 시작 알림을 중복으로 처리한다. Unity 래퍼는 핸들의 Enabled 해제와 IsTrigger 변경에서 기록을 정리하지만 다음 경로에서는 정리하지 않는다.

- Source.enabled나 GameObject의 활성 상태를 직접 바꾼 경우
- Unity가 아닌 연결. 기록을 정리하는 ForgetPairs가 internal이어서 외부 어셈블리의 연결 코드가 호출할 수 없다.

공통 계층에서 시작 → Enabled 해제 → Enabled 설정 → 시작을 재현하면 두 번째 시작이 전달되지 않는다. [쌍 전이 처리](../../../../src/01-Infrastructure/04-T-SpatialSystem/01-Common/3D/CollisionSpace3D.cs)

**결정한 사항.** 쌍 정리를 공통 공간과 설정 적용이 맡는다. 엔진 어댑터는 실제 참여 여부와 관계 식별자를 제공한다.

### SP-07 객체 하나의 실패가 월드 진행을 막음

**2026-11-25 수정 상태.** 객체별 점검·유효성 오류를 ExceptionHandler에 보고하고 실패 객체를 제외한다. 공간에 등록한 콜백의 예외는 보고한 뒤 그 구독을 해제하고, Collider에 등록한 콜백의 예외는 그 알림의 양쪽 Collider를 제외한다. 두 경우 모두 월드는 유지하며, 어댑터의 잘못된 알림이나 월드 진행 예외로도 월드를 해제하지 않는다. 콜백 안의 읽기 실패는 읽지 못한 객체만 제외하며 다른 객체나 월드의 해제로 이어지지 않는다. 처리기가 없거나 던지면 정리 후 중단하고, 정상 반환하면 나머지를 처리한다. 어댑터가 식별한 엔진 오류는 SpatialEngineException으로 구분한다. 공통 사례는 실행하여 통과하였고 Unity는 실행 검증 대기이다.

**근거.** 코드

**관찰 결과.** Unity 래퍼의 Update와 질의는 객체별로 실패를 격리하지 않는다. Update는 Simulate 이전에 전체 핸들의 유효성과 Trigger 배치를 검사한다. [월드 진행](../../../../src/01-Infrastructure/04-T-SpatialSystem/02.2-UnityWrappers/3D/UnitySpaceContext3D.World.cs)

| 실패 | 결과 |
| --- | --- |
| 등록 이후 배치가 모호해진 Trigger | 배치를 고칠 때까지 모든 Update가 Simulate 전에 실패한다. |
| 무효화 중 Disposed 구독자의 예외 | 해당 Update가 Simulate 없이 끝난다. |
| 질의 결과에 포함된 모호한 Trigger | 등록 여부와 관계없이 그 질의 전체가 실패한다. |
| 질의 결과에 포함된, 등록되지 않은 부모 강체의 자식 Collider | 그 질의 전체가 실패한다. |

공통 생성 API만 사용하면 모호한 배치와 부모 강체는 생기지 않는다. 수동 구성에서 발생한다.

**결정한 규칙.** 외부 프레임워크의 오류는 해당 프레임워크의 어댑터에서 처리한다. Spatial System 측 책임이면 ExceptionHandler에 예외를 보고한 뒤 문제 객체를 해당 월드에서 제외한다. 보고 과정에서 예외가 재전파되어도 객체 제외를 보장한다. 처리기가 정상 반환하면 나머지 작업을 계속한다. 처리기가 없으면 문제 객체 제외를 보장한 뒤 예외를 재전파하여 현재 호출을 중단한다. 세부 기준은 [Rules](02.%20Rules.md)의 오류 처리와 유효성 조회 절을 따른다. 소스에 반영하였으며 Unity 실행 검증은 남아 있다.

## 후속 처리

### SP-08 등록되지 않은 강체를 내부 강체로 전환

**2026-11-25 수정 상태.** 공개 Wrap·월드 Source·UnityScene과 질의 중 자동 등록을 제거하였다. 내부 연결은 시스템 생성 객체만 허용하며 외부 객체가 질의되면 오사용 오류를 반환한다. 수동 등록 경로도 제거하였다.

**근거.** 코드

**관찰 결과.** 수동으로 만든 Collider를 Wrap하거나 질의로 발견할 때 같은 GameObject에 강체가 있으면 그 강체를 내부 강체로 바꾼다. 중력과 Kinematic을 끄고 이동·회전을 고정하며 수면하지 않게 한다. 해제할 때 원래 설정으로 되돌리지 않는다.

부모 강체를 가진 자식 Collider의 결과는 등록 순서에 따라 다르다. 부모의 Collider가 먼저 등록되어 있으면 허용되고, 그렇지 않으면 InvalidOperationException으로 거부된다. [내부 강체 구성](../../../../src/01-Infrastructure/04-T-SpatialSystem/02.2-UnityWrappers/3D/UnitySpaceContext3D.World.cs)

**결정한 규칙.** 개발자가 직접 만든 native 객체를 월드에 넣고 Wrap으로 등록하는 것은 오사용이며 지원하지 않는다. 월드 직접 조작 진입점을 공개하지 않고, 생성·등록·제거·진행은 시스템 API와 어댑터가 맡는다. 따라서 수동 객체의 강체 전환·복원 정책은 지원 대상으로 설계하지 않는다. 공개 경로 제거와 내부 연결 제한은 소스에 반영하였다. [Rules](02.%20Rules.md)의 월드 조작의 경계 절을 따른다.

## Unity 실행 확인 항목

SP-04와 SP-05 중 Enabled 비활성화와 외부 비활성 상태의 종료를 확인하는 Unity 사례를 작성했으며 실행하지 않았다.

| 항목 | 확인할 동작 |
| --- | --- |
| SP-04 | Enabled·IsTrigger 변경 뒤 ContactEvent와 2D·3D Trigger의 종료 알림 발생 여부와 순서 |
| SP-21 설정의 반복 적용 | 값이 달라질 때만 native 레이어 설정을 쓰도록 수정하였다. 종료·시작 반복 여부의 Unity 실행 확인은 남아 있다. |
| SP-22 단위 길이가 아닌 회전 | 정규화하지 않은 Quaternion을 전달했을 때의 자세와 질의 |
| SP-24 내부 강체의 비용 | 2D 전체와 3D 기본 형상이 수면하지 않는 Dynamic 강체를 가지므로, 움직이지 않는 Collider가 많을 때의 Update 비용 |

## 제외한 항목

PhysicalSpace·공개 강체·강체 연결 API를 제거하여 다음 항목은 현재 범위에서 제외한다. PhysicalSpace를 다시 구현할 때 확인한다.

| 항목 | 다시 확인할 내용 |
| --- | --- |
| SP-01 공간에 따른 알림 차이 | 강체 없는 Collider 사이의 알림을 두 공간에서 같게 보장할지 정한다. |
| SP-02 강체 자세와 Collider 위치 | Rigidbody.position은 [다음 물리 단계 이후 Transform에 반영](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Rigidbody-position.html)되므로 생성·분리의 위치 기준을 정한다. |
| SP-03 강체 해제와 비활성 Collider | 분리 대상을 attachedRigidbody로 고르면 비활성 Collider가 함께 파괴되는지 확인한다. |
| SP-09 중력과 수면 | 매 Update의 힘 전달이 강체를 [깨어 있는 상태로 설정](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Rigidbody.AddForce.html)하여 수면을 막는지 확인한다. |
| SP-20 MeshCollider의 강체 연결 | Mesh를 지정하기 전의 attachedRigidbody 값에 따라 강체 연결이 거부되는지 확인한다. |

[Spatial System](README.md) · [Engine Binding](04.%20Engine%20Binding.md) · [테스트 결과](../../../../tests/01-Infrastructure/04-T-SpatialSystem/docs/03.%20Results.md)
