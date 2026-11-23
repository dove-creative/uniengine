# Rapier 0.36.1: 충돌 검사 후 새 동적 강체가 운동하지 않는 현상 검토 요청

## 검토 목적

아래 현상이 Rapier의 결함인지, 공개 API의 사용 조건을 잘못 이해한 것인지 독립적으로 검토해 주세요. 아래의 원인 분석은 가설이며 확정된 결론으로 전제하지 말아 주세요.

사용하려는 기능은 같은 월드에서 다음 두 동작을 제공하는 것입니다.

- `detect_collisions`: 시간을 진행시키지 않고 충돌 상태를 갱신한다.
- `step`: 시간을 진행시키면서 물리 시뮬레이션을 수행한다.

Rapier·Parry 원본 패키지를 수정하지 않고 공개 Rust API만 사용하는 것이 프로젝트의 제약입니다.

## 확인된 현상

새 월드에 높이 2의 동적 강체와 반지름 0.5의 Ball 콜라이더를 넣고, 중력 Y = -9.81, 시간 간격 1/60초로 Step을 10번 실행합니다.

- **대조군:** Step만 실행하면 높이가 감소하고 Y 속도가 음수가 됩니다.
- **문제 사례:** 첫 Step 전에 `world.detect_collisions(&(), &())`를 한 번 호출하면, Step을 10번 실행해도 높이는 2, Y 속도는 0입니다.
- 두 경우 모두 질량은 양수이며 `is_sleeping() == false`입니다.
- 각 테스트는 새로운 월드를 만듭니다. 강체 하나와 그에 붙은 콜라이더 하나만 있으며, 다른 충돌 상대나 조인트는 없습니다.
- 삽입 후 강체의 위치·종류를 다시 변경하거나 수동으로 재우는 동작은 없습니다.

C#·C ABI·게임 엔진 래퍼를 제외하고 **Rapier의 공개 Rust API를 직접 호출하는 테스트에서도 재현**됐습니다.

## 최소 재현 코드

새 라이브러리 크레이트에 다음 파일을 작성합니다.

### Cargo.toml

```toml
[package]
name = "detect-step-repro"
version = "0.1.0"
edition = "2024"

[dependencies]
rapier = { package = "rapier2d", version = "=0.36.1" }
```

다른 구성은 의존성의 `package`만 `rapier2d-f64`, `rapier3d`, `rapier3d-f64`로 바꿉니다. 별칭 `rapier`와 정확한 버전 `=0.36.1`은 유지합니다.

### src/lib.rs

```rust
#[cfg(test)]
mod tests {
    use rapier::math::{Real, Vector};
    use rapier::prelude::*;

    fn simulate(detect_first: bool) -> (Real, Real) {
        let mut world = PhysicsWorld::new();
        world.gravity = -Vector::Y * 9.81;
        world.integration_parameters.dt = 1.0 / 60.0;
        let (handle, _) = world.insert(
            RigidBodyBuilder::dynamic().translation(Vector::Y * 2.0),
            ColliderBuilder::ball(0.5),
        );

        if detect_first {
            world.detect_collisions(&(), &());
        }
        for _ in 0..10 {
            world.step();
        }

        let body = &world.bodies[handle];
        let height = body.translation().y;
        let velocity = body.linvel().y;
        println!(
            "detect_first={detect_first} height={height} velocity_y={velocity} mass={} sleeping={}",
            body.mass(), body.is_sleeping()
        );
        (height, velocity)
    }

    #[test]
    fn step_only_control() {
        let (height, velocity) = simulate(false);
        assert!(height < 2.0 && velocity < 0.0);
    }

    #[test]
    fn detect_collisions_then_step() {
        let (height, velocity) = simulate(true);
        assert!(height < 2.0 && velocity < 0.0);
    }
}
```

### 실행 명령

```sh
cargo test --release -- --nocapture --test-threads=1
```

실제 검증에서는 네 구성을 같은 소스 파일을 공유하는 별도 workspace 멤버로 구성해 한 번에 실행했습니다. 실행 명령은 다음과 같습니다.

```sh
cargo test --workspace --release --locked --offline --no-fail-fast --target x86_64-pc-windows-msvc -- --nocapture --test-threads=1
```

## 네 구성의 실측 결과

2026-11-23 재검증 결과입니다.

| Rapier 패키지 | 대조군 최종 높이 | 대조군 Y 속도 | 충돌 검사 선행 시 최종 높이 | 충돌 검사 선행 시 Y 속도 |
| --- | ---: | ---: | ---: | ---: |
| rapier2d 0.36.1 | 1.8603438 | -1.6349996 | 2 | 0 |
| rapier2d-f64 0.36.1 | 1.8603437499999997 | -1.635 | 2 | 0 |
| rapier3d 0.36.1 | 1.8603438 | -1.6349996 | 2 | 0 |
| rapier3d-f64 0.36.1 | 1.8603437499999997 | -1.635 | 2 | 0 |

각 구성에서 `step_only_control`은 통과하고 `detect_collisions_then_step`은 실패했습니다. 최소 재현 테스트 합계는 **4개 통과·4개 실패·무시 0개**, Cargo 종료 코드는 101입니다.

2D f32의 핵심 출력은 다음과 같습니다.

```text
detect_first=true height=2 velocity_y=0 mass=0.7853982 sleeping=false
assertion failed: height < 2.0 && velocity < 0.0

detect_first=false height=1.8603438 velocity_y=-1.6349996 mass=0.7853982 sleeping=false
```

이 수치는 이번 최소 재현 테스트의 결과입니다. 별도 연결 DLL의 ABI 검증 결과와 구분합니다.

## 실행 환경과 검증 범위

- Windows x64, `x86_64-pc-windows-msvc`.
- Rust: `rustc 1.99.1 (58cf8f9c3 2026-10-13)`.
- Cargo: `cargo 1.99.1 (5f94df478 2026-08-27)`.
- Visual Studio 2022, MSVC 도구 디렉터리 14.44.35207, Windows SDK 10.0.26100.0.
- Release 빌드, 각 패키지의 기본 feature. 기본 feature의 block-solver는 rapier2d·rapier2d-f64·rapier3d-f64에 포함되고 rapier3d에는 포함되지 않습니다. rapier3d-f64 manifest의 주석은 3D에서 기본 비활성이라고 설명하지만, 실제 기본 feature 목록에는 포함돼 있습니다.
- parallel·SIMD·enhanced-determinism 등의 추가 feature는 활성화하지 않았습니다.
- 네 구성의 대응 Parry 의존성은 모두 0.31.1로 해석됐습니다.
- crates.io 배포본을 사용했습니다. Cargo.lock과 패키지 압축 파일의 체크섬, 압축 파일과 실제 빌드 입력의 파일별 일치를 확인했습니다.
- 최소 재현의 전체 registry 의존성 버전·체크섬도 최초 검증의 Cargo.lock과 동일했습니다.
- 현재 Git master, Debug 빌드, 다른 운영체제, 이전 버전과의 비교는 수행하지 않았습니다.

### 소스 출처에 관한 주의

배포 crate의 `.cargo_vcs_info.json`은 Rapier 커밋 `475caa3ff4646fd9a796b67d14116256f4962bc7`, Parry 커밋 `3383f51cbbe9af70565427e7a66c605e1557c1fc`와 `dirty: true`를 기록합니다. 이는 배포자가 기록한 메타데이터이며 테스트 과정에서 패키지를 수정했다는 의미가 아닙니다. 특정 Git 태그의 깨끗한 checkout과 배포 crate가 같다고 가정하지 않고, 실제 0.36.1 배포 소스를 기준으로 검토해 주세요.

## 기존 이슈 #815·#662와의 차이

### 기존 보고

- [#815: Stand-alone CollisionPipeline doesn't account for changes to bodies](https://github.com/dimforge/rapier/issues/815)는 충돌 파이프라인이 변경 강체 목록을 가져간 뒤 **강체의 변경 플래그를 지우지 않아**, 이후 `get_mut`에서 해당 강체가 변경 목록에 다시 추가되지 않는 문제를 설명합니다. 위치 변경 등의 후속 수정이 반영되지 않는 현상입니다. 유지보수자는 수정됐다고 판단해 종료했습니다.
- [#662: CollisionPipeline ergonomics feedback and a bug](https://github.com/dimforge/rapier/issues/662)에서도 남아 있는 `MODIFIED` 플래그 문제를 지적합니다. API 구성과 운동학적 강체에 관한 다른 논의도 포함합니다.
- [0.35.0 릴리즈 PR #982](https://github.com/dimforge/rapier/pull/982)에는 충돌 파이프라인이 강체의 변경 플래그를 지우도록 수정했다는 내용이 명시돼 있습니다.

### 이번 재현

- 테스트한 0.36.1에는 이미 `CollisionPipeline::clear_modified_bodies`가 있고 실제로 호출됩니다.
- 이후 위치 변경이나 `get_mut` 호출 없이, **새 동적 강체의 최초 운동 자체가 시작되지 않는 현상**입니다.
- 따라서 기존 이슈와 같은 코드 영역이라는 사실만으로 동일한 결함이나 이미 해결된 문제라고 단정할 수 없습니다.
- 기존 수정 때문에 생긴 회귀인지, 다른 원인인지는 확인하지 않았습니다. 버전 이분 탐색도 수행하지 않았습니다.

## 원인 가설 — 미확정

0.36.1 배포 소스에서 관찰한 흐름은 다음과 같습니다.

1. `CollisionPipeline::step`이 `bodies.take_modified()`로 변경 강체 목록을 가져옵니다.
2. `handle_user_changes_to_rigid_bodies(None, ...)`를 호출합니다. 이 사용자 변경 처리 호출에는 island manager가 전달되지 않습니다.
3. 사용자 변경 처리 함수는 island manager가 제공된 경우에만 `islands.rigid_body_updated(...)`를 호출합니다.
4. 이후 충돌 파이프라인이 변경 강체 플래그와 목록을 지웁니다.
5. 물리 파이프라인은 사용자 변경 처리에 island manager를 전달하지만, 앞선 충돌 검사에서 이미 변경 목록을 소비했을 수 있습니다.

**가설:** 새 강체를 활성 island에 등록하기 전에 변경 기록이 소비돼, 뒤따르는 물리 Step이 해당 강체를 시뮬레이션 대상으로 등록하지 못하는 것일 수 있습니다.

이 설명은 소스 흐름에 근거한 추정입니다. 활성 집합의 내부 상태를 직접 관측하거나 원인을 수정한 뒤 회귀 테스트를 통과시켜 확정한 것은 아닙니다.

## 검토받고 싶은 질문

1. 위 최소 재현 코드가 Rapier의 지원되는 공개 API 사용법에 해당하나요? 놓친 초기화나 호출 순서 조건이 있나요?
2. 같은 월드에서 `detect_collisions`를 먼저 호출한 뒤 `step`을 실행하면 정상 운동해야 한다는 기대가 타당한가요?
3. 현상의 원인이 활성 island 등록 누락이라는 가설은 맞나요? 다른 가능한 원인은 무엇인가요?
4. #815·#662와 동일한 결함인가요, 별도의 상태 전이 문제인가요?
5. 원본 패키지를 수정하지 않고 공개 API만으로 대응할 수 있나요? 가능하다면 잠든 강체·수동 이동·추가 및 제거·충돌 이벤트의 의미를 보존하는지 어떻게 검증해야 하나요?
6. 이 사례를 더 줄이거나 원인을 분리하려면 어떤 추가 테스트가 유효한가요?

보정 코드와 버전 변경은 아직 적용하지 않았습니다.

## 등록된 업스트림 이슈

[Rapier #1084](https://github.com/dimforge/rapier/issues/1084)에 위 최소 재현과 네 구성 결과를 등록했습니다. 해당 이슈는 이 재현을 보고한 것이며, 유지보수자가 원인을 확인했거나 버그로 인정했다는 근거는 아닙니다.
