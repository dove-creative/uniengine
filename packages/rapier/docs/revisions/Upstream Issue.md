## Summary

With the published Rapier **0.36.1** crates, calling `PhysicsWorld::detect_collisions(&(), &())` after inserting a new dynamic rigid body, **before its first physics step**, leaves that body stationary during the following ten `step()` calls.

The otherwise identical control, which skips `detect_collisions`, falls normally under gravity. This reproduces in **2D/3D × f32/f64**.

The reproduction below uses Rapier's public Rust API directly. It has no engine wrapper, FFI, C# code, direct Parry calls, or upstream source modifications.

## Expected and actual behavior

Collision-only detection should update collision information without advancing time; subsequent physics steps should still simulate the newly inserted dynamic body.

After ten steps of 1/60 s with gravity Y = -9.81:

- **Expected:** both tests have a height below 2 and a negative Y velocity.
- **Actual:** the control falls; the detect-first case remains at height 2 with Y velocity 0.
- The body has positive mass and reports `is_sleeping() == false` in both cases.
- Each test creates a fresh world containing just this one body and its ball collider. There are no other bodies, contacts, or joints.

## Minimal reproduction

Create a library crate with this `Cargo.toml`:

```toml
[package]
name = "detect-step-repro"
version = "0.1.0"
edition = "2024"

[dependencies]
rapier = { package = "rapier2d", version = "=0.36.1" }
```

Put this in `src/lib.rs`:

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

Run:

```sh
cargo test --release -- --nocapture --test-threads=1
```

To test the other configurations, replace only the dependency's `package` value with `rapier2d-f64`, `rapier3d`, or `rapier3d-f64`. Keep the alias `rapier` and the exact version unchanged. All four use the same Rust test source.

## Four-configuration results

Rechecked on 2026-11-23. Each configuration produces **1 passed / 1 failed / 0 ignored**; `step_only_control` passes and `detect_collisions_then_step` fails. Total: **4 passed / 4 failed**. Cargo exits with code 101.

| Published crate | Control height | Control Y velocity | Detect-first height | Detect-first Y velocity |
| --- | ---: | ---: | ---: | ---: |
| rapier2d 0.36.1 | 1.8603438 | -1.6349996 | 2 | 0 |
| rapier2d-f64 0.36.1 | 1.8603437499999997 | -1.635 | 2 | 0 |
| rapier3d 0.36.1 | 1.8603438 | -1.6349996 | 2 | 0 |
| rapier3d-f64 0.36.1 | 1.8603437499999997 | -1.635 | 2 | 0 |

Representative f32 2D output:

```text
detect_first=true height=2 velocity_y=0 mass=0.7853982 sleeping=false
assertion failed: height < 2.0 && velocity < 0.0
test tests::detect_collisions_then_step ... FAILED
detect_first=false height=1.8603438 velocity_y=-1.6349996 mass=0.7853982 sleeping=false
test tests::step_only_control ... ok
test result: FAILED. 1 passed; 1 failed; 0 ignored
```

## Environment and version scope

- Windows x64, target `x86_64-pc-windows-msvc`.
- `rustc 1.99.1 (58cf8f9c3 2026-10-13)`.
- `cargo 1.99.1 (5f94df478 2026-08-27)`.
- Visual Studio 2022 MSVC tool directory 14.44.35207; Windows SDK 10.0.26100.0.
- Release builds, default crate features; no additional parallel, SIMD, or enhanced-determinism features enabled. The default features enable `block-solver` in `rapier2d`, `rapier2d-f64`, and `rapier3d-f64`, but not in `rapier3d`.
- The corresponding Parry dependencies resolve to **0.31.1** in all four configurations.
- The four packages were tested together as workspace members with `cargo test --workspace --release --locked --offline --no-fail-fast --target x86_64-pc-windows-msvc -- --nocapture --test-threads=1`.
- These are crates.io packages, not a Git checkout. The Rapier/Parry archives match Cargo.lock checksums, and their extracted source files match the archives. I have not tested current Git master, debug builds, or other platforms.

## Relationship to #815 and #662

These reports concern the same change-tracking area, but the observed failure is different:

- **#815** describes the collision pipeline consuming the modified-body list **without clearing the bodies' change flags**. Later `get_mut` operations then fail to re-add bodies to that list, so subsequent body changes do not propagate.
- **#662** also describes stale `MODIFIED` flags in the collision-only path, along with broader API feedback. The 0.35.0 release notes in #982 explicitly list clearing rigid-body modified flags as the fix.
- In the **0.36.1 package tested here**, `CollisionPipeline::clear_modified_bodies` exists and is called. This reproduction requires **no subsequent body mutation**: a newly inserted body simply fails to move when collision detection precedes its first physics step.

I therefore cannot identify this as the same already-fixed bug. It may be related to the earlier changes, but I have not bisected the versions or established a regression boundary.

## Possible cause — not yet confirmed

In the published 0.36.1 source:

1. `CollisionPipeline::step` consumes `bodies.take_modified()`.
2. It calls `handle_user_changes_to_rigid_bodies(None, ...)`, without the island manager for that part of the update.
3. That helper calls `islands.rigid_body_updated(...)` only when an island manager is provided.
4. The collision pipeline then clears the modified-body flags and list.

My hypothesis is that this consumes the new body's pending update before the physics pipeline can register it in the active island set. I have not applied a workaround or a patch; the behavior and test matrix above are the confirmed observations.

Is this sequence intended to be supported by `PhysicsWorld`, and should the collision-only update preserve or complete the activation/island bookkeeping needed by the next physics step?
