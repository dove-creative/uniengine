//! Stage 0 experiment only: this is not the production PhysicalSpace ABI.
#![deny(unsafe_op_in_unsafe_fn)]

use rapier::math::{Pose, Real, Vector};
use rapier::prelude::*;
use std::cell::RefCell;
use std::ffi::{CString, c_char, c_void};
use std::mem::{align_of, offset_of, size_of};
use std::panic::{AssertUnwindSafe, catch_unwind};

const OK: u32 = 0;
const INVALID_ARGUMENT: u32 = 1;
const ABI_MISMATCH: u32 = 2;
const PANIC: u32 = 3;
const CALLBACK_FAILED: u32 = 4;
const UNSUPPORTED: u32 = 5;

thread_local! {
    static LAST_ERROR: RefCell<CString> = RefCell::new(CString::default());
}

#[repr(C)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct ProbeAbi {
    pub revision: u32,
    pub dimension: u32,
    pub real_size: u32,
    pub pointer_size: u32,
    pub options: u32,
    pub result_size: u32,
    pub result_alignment: u32,
    pub result_offsets: [u32; 8],
}

#[repr(C)]
#[derive(Clone, Copy, Debug, Default)]
pub struct ProbeResult {
    pub start_height: Real,
    pub end_height: Real,
    pub distance: Real,
    pub shape_radius: Real,
    pub body_count: u32,
    pub collider_count: u32,
    pub active_intersections: u32,
    pub stale_handle_rejected: u32,
}

fn abi() -> ProbeAbi {
    ProbeAbi {
        revision: 1,
        dimension: rapier::math::DIM as u32,
        real_size: size_of::<Real>() as u32,
        pointer_size: size_of::<usize>() as u32,
        options: 0,
        result_size: size_of::<ProbeResult>() as u32,
        result_alignment: align_of::<ProbeResult>() as u32,
        result_offsets: [
            offset_of!(ProbeResult, start_height) as u32,
            offset_of!(ProbeResult, end_height) as u32,
            offset_of!(ProbeResult, distance) as u32,
            offset_of!(ProbeResult, shape_radius) as u32,
            offset_of!(ProbeResult, body_count) as u32,
            offset_of!(ProbeResult, collider_count) as u32,
            offset_of!(ProbeResult, active_intersections) as u32,
            offset_of!(ProbeResult, stale_handle_rejected) as u32,
        ],
    }
}

fn boundary(operation: impl FnOnce() -> Result<(), (u32, &'static str)>) -> u32 {
    let (status, message) = match catch_unwind(AssertUnwindSafe(operation)) {
        Ok(Ok(())) => (OK, ""),
        Ok(Err(error)) => error,
        Err(_) => (PANIC, "Rust panic caught by the investigation ABI"),
    };
    LAST_ERROR.with(|error| *error.borrow_mut() = CString::new(message).unwrap());
    status
}

fn validate_pointer<T>(pointer: *const T) -> Result<(), (u32, &'static str)> {
    if pointer.is_null() || (pointer as usize) % align_of::<T>() != 0 {
        return Err((INVALID_ARGUMENT, "Null or misaligned pointer"));
    }

    // The caller must still supply a live buffer of the documented size.
    Ok(())
}

// Retain the unmodified alternative for comparison with the pinned upstream crates.
#[cfg(test)]
fn detect_collisions_zero_step(world: &mut PhysicsWorld, hooks: &dyn PhysicsHooks, events: &dyn EventHandler) {
    let mut parameters = world.integration_parameters;
    parameters.dt = 0.0;
    world.physics_pipeline.step(
        world.gravity,
        &parameters,
        &mut world.islands,
        &mut world.broad_phase,
        &mut world.narrow_phase,
        &mut world.bodies,
        &mut world.colliders,
        &mut world.impulse_joints,
        &mut world.multibody_joints,
        &mut world.soft_bodies,
        &mut world.ccd_solver,
        hooks,
        events,
    );
}

fn run_direct_api(dt: Real) -> Result<ProbeResult, (u32, &'static str)> {
    if !dt.is_finite() || dt <= 0.0 {
        return Err((INVALID_ARGUMENT, "The time step must be finite and positive"));
    }

    let mut world = PhysicsWorld::new();
    world.integration_parameters.dt = dt;
    let shape = SharedShape::ball(0.5);
    let retained_shape = shape.clone();
    let body = world.bodies.insert(RigidBodyBuilder::dynamic().translation(Vector::Y * 2.0));
    world.colliders.insert_with_parent(
        ColliderBuilder::new(shape.clone()),
        body,
        &mut world.bodies,
    );
    drop(shape);

    let start_height = world.bodies[body].translation().y;
    world.detect_collisions(&(), &());
    assert_eq!(world.bodies[body].translation().y, start_height);
    world.step();
    let end_height = world.bodies[body].translation().y;

    // These types are supplied by the unmodified, exact-version Parry dependency.
    let other = parry::shape::Ball::new(0.5);
    let distance = parry::query::distance(
        &Pose::IDENTITY,
        retained_shape.as_ref(),
        &Pose::from_translation(Vector::X * 3.0),
        &other,
    ).map_err(|_| (UNSUPPORTED, "Parry does not support this shape pair"))?;
    let shape_radius = retained_shape.as_ball().unwrap().radius;

    let mut sensor_world = PhysicsWorld::new();
    let sensor = sensor_world.colliders.insert(
        ColliderBuilder::ball(1.0).sensor(true).active_collision_types(ActiveCollisionTypes::all()),
    );
    let collider = sensor_world.colliders.insert(
        ColliderBuilder::ball(1.0).active_collision_types(ActiveCollisionTypes::all()),
    );
    assert_ne!(sensor_world.narrow_phase.intersection_pair(sensor, collider), Some(true));
    sensor_world.detect_collisions(&(), &());
    let active_intersections = u32::from(
        sensor_world.narrow_phase.intersection_pair(sensor, collider) == Some(true),
    );

    let removed = world.bodies.insert(RigidBodyBuilder::fixed());
    world.bodies.remove(
        removed,
        &mut world.islands,
        &mut world.colliders,
        &mut world.impulse_joints,
        &mut world.multibody_joints,
        &mut world.soft_bodies,
        true,
    );
    let replacement = world.bodies.insert(RigidBodyBuilder::fixed());
    let stale_handle_rejected = u32::from(!world.bodies.contains(removed) && replacement != removed);

    // Compile-time proof that all world components are accessible from a separate crate.
    let _components = (
        &mut world.gravity,
        &mut world.integration_parameters,
        &mut world.physics_pipeline,
        &mut world.collision_pipeline,
        &mut world.islands,
        &mut world.broad_phase,
        &mut world.narrow_phase,
        &mut world.impulse_joints,
        &mut world.multibody_joints,
        &mut world.soft_bodies,
        &mut world.ccd_solver,
    );

    Ok(ProbeResult {
        start_height,
        end_height,
        distance: distance.distance,
        shape_radius,
        body_count: world.bodies.len() as u32,
        collider_count: world.colliders.len() as u32,
        active_intersections,
        stale_handle_rejected,
    })
}

/// The output points to a writable ProbeAbi buffer of exactly output_size bytes.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ue_probe_get_abi(output: *mut ProbeAbi, output_size: u32) -> u32 {
    boundary(|| {
        validate_pointer(output)?;
        if output_size != size_of::<ProbeAbi>() as u32 {
            return Err((ABI_MISMATCH, "ABI descriptor size mismatch"));
        }

        unsafe { output.write(abi()) };
        Ok(())
    })
}

/// The expected descriptor is independently constructed from the consumer's header.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ue_probe_check_abi(expected: *const ProbeAbi, expected_size: u32) -> u32 {
    boundary(|| {
        validate_pointer(expected)?;
        if expected_size != size_of::<ProbeAbi>() as u32 {
            return Err((ABI_MISMATCH, "ABI descriptor size mismatch"));
        }
        if unsafe { expected.read() } != abi() {
            return Err((ABI_MISMATCH, "ABI version, configuration, or POD layout mismatch"));
        }

        Ok(())
    })
}

/// The output points to a live ProbeResult buffer; errors leave that buffer unchanged.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ue_probe_run(dt: Real, output: *mut ProbeResult, output_size: u32) -> u32 {
    boundary(|| {
        validate_pointer(output)?;
        if output_size != size_of::<ProbeResult>() as u32 {
            return Err((ABI_MISMATCH, "Result buffer size mismatch"));
        }

        let result = run_direct_api(dt)?;
        unsafe { output.write(result) };
        Ok(())
    })
}

pub type ProbeCallback = unsafe extern "C" fn(*mut c_void, Real) -> i32;

/// The callback must not unwind. A negative result explicitly reports callback failure.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ue_probe_callback(
    callback: Option<ProbeCallback>,
    user_data: *mut c_void,
    value: Real,
    output: *mut i32,
) -> u32 {
    boundary(|| {
        validate_pointer(output)?;
        let callback = callback.ok_or((INVALID_ARGUMENT, "Missing callback"))?;
        let result = unsafe { callback(user_data, value) };
        if result < 0 {
            return Err((CALLBACK_FAILED, "The callback reported failure"));
        }

        unsafe { output.write(result) };
        Ok(())
    })
}

/// Borrowed UTF-8 text, valid until the next operation on this thread.
#[unsafe(no_mangle)]
pub extern "C" fn ue_probe_last_error() -> *const c_char {
    LAST_ERROR.with(|error| error.borrow().as_ptr())
}

/// Deliberate panic injection exists only in this investigation library.
#[unsafe(no_mangle)]
pub extern "C" fn ue_probe_panic() -> u32 {
    boundary(|| panic!("Stage 0 panic boundary test"))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn refresh(world: &mut PhysicsWorld, events: &dyn EventHandler) {
        match std::env::var("UE_PROBE_TEST_COLLISION_PATH").as_deref() {
            Ok("world") | Err(std::env::VarError::NotPresent) => world.detect_collisions(&(), events),
            Ok("zero-step") => {
                detect_collisions_zero_step(world, &(), events);
            }
            other => panic!("Unknown collision comparison path: {other:?}"),
        }
    }

    fn close(actual: Real, expected: Real) {
        let tolerance = if size_of::<Real>() == 4 { 1.0e-5 } else { 1.0e-10 };
        assert!((actual - expected).abs() <= tolerance * (1.0 + expected.abs()),
            "actual={actual} expected={expected} tolerance={tolerance}");
    }

    fn dynamic_ball(world: &mut PhysicsWorld, position: Vector) -> (RigidBodyHandle, ColliderHandle) {
        world.insert(RigidBodyBuilder::dynamic().translation(position), ColliderBuilder::ball(0.5))
    }

    #[test]
    fn public_world_and_parry_apis_work_through_c_boundary_implementation() {
        let result = run_direct_api(1.0 / 60.0).unwrap();
        assert!(result.end_height < result.start_height, "{result:?}");
        assert!((result.distance - 2.0).abs() < 1.0e-5);
        assert_eq!(result.shape_radius, 0.5);
        assert_eq!((result.body_count, result.collider_count), (2, 1));
        assert_eq!(result.active_intersections, 1);
        assert_eq!(result.stale_handle_rejected, 1);
    }

    #[test]
    fn collision_detect_then_step_matches_control() {
        let mut reference = None;
        for detect_first in [false, true] {
            let mut world = PhysicsWorld::new();
            world.gravity = -Vector::Y * 9.81;
            world.integration_parameters.dt = 1.0 / 60.0;
            let (body, _) = world.insert(
                RigidBodyBuilder::dynamic().translation(Vector::Y * 2.0),
                ColliderBuilder::ball(0.5),
            );
            if detect_first {
                refresh(&mut world, &());
                assert_eq!(world.bodies[body].translation().y, 2.0);
            }
            for _ in 0..10 { world.step(); }
            println!("detect_first={detect_first} y={} velocity={:?} mass={} sleeping={}",
                world.bodies[body].translation().y, world.bodies[body].linvel(),
                world.bodies[body].mass(), world.bodies[body].is_sleeping());
            assert!(world.bodies[body].translation().y < 2.0, "detect_first={detect_first}");
            assert!(world.bodies[body].linvel().y < 0.0);
            let state = (world.bodies[body].translation().y, world.bodies[body].linvel().y);
            if let Some((height, velocity)) = reference {
                close(state.0, height);
                close(state.1, velocity);
            }
            reference = Some(state);
        }
    }

    #[test]
    fn collision_repeated_detection_preserves_motion_and_timestep() {
        let mut world = PhysicsWorld::new();
        world.integration_parameters.dt = 0.025;
        let (body, _) = dynamic_ball(&mut world, Vector::Y * 2.0);
        world.bodies[body].set_linvel(Vector::X, true);
        let initial_pose = *world.bodies[body].position();
        let initial_velocity = world.bodies[body].linvel();

        for _ in 0..512 {
            refresh(&mut world, &());
            assert_eq!(*world.bodies[body].position(), initial_pose);
            assert_eq!(world.bodies[body].linvel(), initial_velocity);
            assert_eq!(world.integration_parameters.dt, 0.025);
            assert!(!world.bodies[body].is_sleeping());
        }

        world.step();
        assert!(world.bodies[body].translation().y < 2.0);
        close(world.bodies[body].translation().x, 0.025);
    }

    #[test]
    fn collision_new_body_after_step_moves() {
        let mut world = PhysicsWorld::new();
        dynamic_ball(&mut world, Vector::X * 10.0);
        world.step();
        let (body, _) = dynamic_ball(&mut world, Vector::Y * 2.0);
        refresh(&mut world, &());
        world.step();
        assert!(world.bodies[body].translation().y < 2.0);
    }

    fn touching_insertions_register_and_sleep_together(existing_world: bool) {
        let mut world = PhysicsWorld::new();
        world.gravity = Vector::ZERO;
        let (first, first_collider) = dynamic_ball(&mut world, Vector::ZERO);
        if existing_world {
            world.step();
        }
        let (second, second_collider) = dynamic_ball(&mut world, Vector::X);
        refresh(&mut world, &());
        let pair = world.narrow_phase.contact_pair(first_collider, second_collider).unwrap();
        assert!(pair.has_any_active_contact());
        assert_eq!(world.islands.num_active_bodies(), 2);
        let island = world.islands.persistent_island_of(&world.bodies, first);
        assert!(island.is_some());
        assert_eq!(island, world.islands.persistent_island_of(&world.bodies, second));

        for _ in 0..300 {
            world.step();
        }
        assert!(world.bodies[first].is_sleeping());
        assert!(world.bodies[second].is_sleeping());
        world.bodies[first].wake_up(true);
        refresh(&mut world, &());
        world.step();
        assert!(!world.bodies[first].is_sleeping());
        assert!(!world.bodies[second].is_sleeping());
    }

    #[test]
    fn collision_callback_panic_preserves_world_timestep() {
        struct PanicEvents;
        impl EventHandler for PanicEvents {
            fn handle_collision_event(&self, _: &RigidBodySet, _: &ColliderSet,
                _: CollisionEvent, _: Option<&ContactPair>) { panic!("Collision callback probe"); }
            fn handle_contact_force_event(&self, _: Real, _: &RigidBodySet, _: &ColliderSet,
                _: &ContactPair, _: Real) {}
            fn handle_soft_body_tear_event(&self, _: &SoftBodySet, _: &SoftBodyTearEvent) {}
        }
        let mut world = PhysicsWorld::new();
        world.integration_parameters.dt = 0.025;
        dynamic_ball(&mut world, Vector::ZERO);
        world.colliders.insert(ColliderBuilder::ball(1.0).sensor(true)
            .active_events(ActiveEvents::COLLISION_EVENTS));
        assert!(catch_unwind(AssertUnwindSafe(|| refresh(&mut world, &PanicEvents))).is_err());
        assert_eq!(world.integration_parameters.dt, 0.025);
        // A callback panic does not promise recovery of the partially updated world.
    }

    #[test]
    fn collision_touching_new_bodies_register_and_sleep_together() {
        touching_insertions_register_and_sleep_together(false);
    }

    #[test]
    fn collision_touching_insertion_after_step_registers_and_sleeps_together() {
        touching_insertions_register_and_sleep_together(true);
    }

    #[test]
    fn collision_successive_edits_refresh_colliders_and_events() {
        let mut world = PhysicsWorld::new();
        world.gravity = Vector::ZERO;
        let (body, collider) = dynamic_ball(&mut world, Vector::X * 3.0);
        let sensor = world.colliders.insert(ColliderBuilder::ball(1.0).sensor(true)
            .active_events(ActiveEvents::COLLISION_EVENTS));
        let (collision_sender, collisions) = std::sync::mpsc::channel();
        let (force_sender, forces) = std::sync::mpsc::channel();
        let (tear_sender, _) = std::sync::mpsc::channel();
        let events = ChannelEventCollector::new(collision_sender, force_sender, tear_sender);
        refresh(&mut world, &events);

        for _ in 0..3 {
            world.bodies[body].set_translation(Vector::ZERO, true);
            refresh(&mut world, &events);
            assert_eq!(world.colliders[collider].translation().x, 0.0);
            assert_eq!(world.narrow_phase.intersection_pair(collider, sensor), Some(true));
            let started: Vec<_> = collisions.try_iter().collect();
            assert_eq!(started.len(), 1);
            assert!(matches!(started[0], CollisionEvent::Started(..)));
            refresh(&mut world, &events);
            assert_eq!(collisions.try_iter().count(), 0);
            world.bodies[body].set_translation(Vector::X * 3.0, true);
            refresh(&mut world, &events);
            assert_ne!(world.narrow_phase.intersection_pair(collider, sensor), Some(true));
            let stopped: Vec<_> = collisions.try_iter().collect();
            assert_eq!(stopped.len(), 1);
            assert!(matches!(stopped[0], CollisionEvent::Stopped(..)));
        }

        assert_eq!(forces.try_iter().count(), 0);
    }

    #[test]
    fn collision_collider_shape_mass_and_parent_changes_propagate() {
        let mut world = PhysicsWorld::new();
        world.gravity = Vector::ZERO;
        let (first, collider) = dynamic_ball(&mut world, Vector::ZERO);
        let second = world.bodies.insert(RigidBodyBuilder::dynamic().translation(Vector::X * 5.0));
        refresh(&mut world, &());
        let original_mass = world.bodies[first].mass();
        world.colliders[collider].set_density(2.0);
        refresh(&mut world, &());
        close(world.bodies[first].mass(), original_mass * 2.0);
        world.colliders[collider].set_shape(SharedShape::ball(1.0));
        refresh(&mut world, &());
        close(world.bodies[first].mass(), original_mass * 2.0 * (2.0 as Real).powi(rapier::math::DIM as i32));
        world.colliders[collider].set_mass(3.0);
        world.colliders.set_parent(collider, Some(second), &mut world.bodies);
        refresh(&mut world, &());
        close(world.bodies[first].mass(), 0.0);
        close(world.bodies[second].mass(), 3.0);
        close(world.colliders[collider].translation().x, 5.0);
    }

    #[test]
    fn collision_sleep_enable_and_body_type_changes_reach_physics() {
        let mut world = PhysicsWorld::new();
        let (body, collider) = world.insert(
            RigidBodyBuilder::dynamic().translation(Vector::Y * 2.0).sleeping(true),
            ColliderBuilder::ball(0.5),
        );
        refresh(&mut world, &());
        world.step();
        close(world.bodies[body].translation().y, 2.0);
        assert!(world.bodies[body].is_sleeping());
        world.bodies[body].wake_up(true);
        refresh(&mut world, &());
        world.step();
        assert!(world.bodies[body].translation().y < 2.0);
        world.bodies[body].set_enabled(false);
        let disabled_pose = *world.bodies[body].position();
        refresh(&mut world, &());
        world.step();
        assert!(!world.colliders[collider].is_enabled());
        assert_eq!(*world.bodies[body].position(), disabled_pose);
        world.bodies[body].set_enabled(true);
        refresh(&mut world, &());
        world.step();
        assert!(world.colliders[collider].is_enabled());
        assert!(world.bodies[body].translation().y < disabled_pose.translation.y);
        world.bodies[body].set_body_type(RigidBodyType::Fixed, true);
        let fixed_position = world.bodies[body].translation();
        refresh(&mut world, &());
        world.step();
        assert_eq!(world.bodies[body].translation(), fixed_position);
        world.bodies[body].set_body_type(RigidBodyType::KinematicPositionBased, true);
        world.bodies[body].set_next_kinematic_translation(fixed_position + Vector::X);
        refresh(&mut world, &());
        assert_eq!(world.bodies[body].translation(), fixed_position);
        world.step();
        close(world.bodies[body].translation().x, fixed_position.x + 1.0);
        world.bodies[body].set_body_type(RigidBodyType::Dynamic, true);
        world.bodies[body].set_linvel(Vector::ZERO, true);
        refresh(&mut world, &());
        let height = world.bodies[body].translation().y;
        world.step();
        assert!(world.bodies[body].translation().y < height);
    }

    #[test]
    fn collision_manual_sleep_matches_step_only_control() {
        let mut reference = None;
        for detect_first in [false, true] {
            let mut world = PhysicsWorld::new();
            let (body, _) = dynamic_ball(&mut world, Vector::Y * 2.0);
            world.step();
            world.bodies[body].sleep();
            let before = world.bodies[body].translation().y;
            if detect_first {
                refresh(&mut world, &());
                close(world.bodies[body].translation().y, before);
                assert!(world.bodies[body].is_sleeping());
            }
            world.step();
            let state = (world.bodies[body].translation().y, world.bodies[body].is_sleeping());
            println!("manual_sleep detect_first={detect_first} before={before} after={} sleeping={}",
                state.0, state.1);
            if let Some((height, sleeping)) = reference {
                close(state.0, height);
                assert_eq!(state.1, sleeping);
            }
            reference = Some(state);
        }
    }

    #[test]
    fn collision_manual_sleep_before_first_step_matches_control() {
        let mut reference = None;
        for detect_first in [false, true] {
            let mut world = PhysicsWorld::new();
            let (body, _) = dynamic_ball(&mut world, Vector::Y * 2.0);
            if detect_first { refresh(&mut world, &()); }
            world.bodies[body].sleep();
            if detect_first { refresh(&mut world, &()); }
            world.step();
            let state = (world.bodies[body].translation().y, world.bodies[body].is_sleeping());
            println!("early_manual_sleep detect_first={detect_first} after={} sleeping={}", state.0, state.1);
            if let Some((height, sleeping)) = reference {
                close(state.0, height);
                assert_eq!(state.1, sleeping);
            }
            reference = Some(state);
        }
    }

    #[test]
    fn collision_jointed_body_changes_preserve_constraints() {
        let mut world = PhysicsWorld::new();
        let fixed = world.bodies.insert(RigidBodyBuilder::fixed().translation(Vector::Y * 2.0));
        let (dynamic, _) = dynamic_ball(&mut world, Vector::Y * 2.0);
        let joint = world.impulse_joints.insert(fixed, dynamic, FixedJointBuilder::new(), true);
        refresh(&mut world, &());
        for _ in 0..10 { world.step(); }
        close(world.bodies[dynamic].translation().y, 2.0);
        world.bodies[dynamic].set_enabled(false);
        refresh(&mut world, &());
        world.step();
        assert!(!world.impulse_joints.get(joint).unwrap().data.is_enabled());
        world.bodies[dynamic].set_enabled(true);
        world.bodies[fixed].set_translation(Vector::Y * 3.0, true);
        refresh(&mut world, &());
        for _ in 0..120 { world.step(); }
        assert!(world.impulse_joints.get(joint).unwrap().data.is_enabled());
        close(world.bodies[dynamic].translation().y, 3.0);
        world.bodies[fixed].set_body_type(RigidBodyType::Dynamic, true);
        world.bodies[fixed].set_additional_mass(1.0, true);
        refresh(&mut world, &());
        let initial_height = world.bodies[dynamic].translation().y;
        for _ in 0..10 { world.step(); }
        assert!(world.bodies[dynamic].translation().y < initial_height);
        close(world.bodies[fixed].translation().y, world.bodies[dynamic].translation().y);
    }

    #[test]
    fn collision_removed_handles_do_not_affect_replacements() {
        let mut world = PhysicsWorld::new();
        for _ in 0..100 {
            let (removed, collider) = dynamic_ball(&mut world, Vector::Y * 2.0);
            refresh(&mut world, &());
            world.remove_body(removed).unwrap();
            let (replacement, _) = dynamic_ball(&mut world, Vector::Y * 4.0);
            refresh(&mut world, &());
            assert!(!world.bodies.contains(removed));
            assert!(!world.colliders.contains(collider));
            assert_ne!(removed, replacement);
            assert_eq!(world.bodies.len(), 1);
            assert_eq!(world.islands.num_active_bodies(), 1);
            world.step();
            assert!(world.bodies[replacement].translation().y < 4.0);
            world.remove_body(replacement).unwrap();
            refresh(&mut world, &());
            assert_eq!(world.islands.num_active_bodies(), 0);
        }
    }

    #[test]
    fn abi_rejects_every_changed_contract_field() {
        let expected = abi();
        assert_eq!(unsafe { ue_probe_check_abi(&expected, size_of::<ProbeAbi>() as u32) }, OK);
        for index in 0..15 {
            let mut changed = expected;
            // ProbeAbi consists of fifteen repr(C) u32 values with no padding.
            unsafe { *(std::ptr::addr_of_mut!(changed).cast::<u32>().add(index)) ^= 1 };
            assert_eq!(
                unsafe { ue_probe_check_abi(&changed, size_of::<ProbeAbi>() as u32) },
                ABI_MISMATCH,
            );
        }
    }

    #[test]
    fn invalid_input_does_not_overwrite_the_output() {
        let mut result = ProbeResult { start_height: 123.0, ..ProbeResult::default() };
        for dt in [0.0, -1.0, Real::NAN, Real::INFINITY] {
            assert_eq!(unsafe { ue_probe_run(dt, &mut result, size_of::<ProbeResult>() as u32) }, INVALID_ARGUMENT);
            assert_eq!(result.start_height, 123.0);
        }
        assert_eq!(unsafe { ue_probe_run(0.1, &mut result, 0) }, ABI_MISMATCH);
        assert_eq!(unsafe { ue_probe_run(0.1, std::ptr::null_mut(), 0) }, INVALID_ARGUMENT);
    }

    #[test]
    fn panic_is_reported_and_the_next_operation_can_succeed() {
        assert_eq!(ue_probe_panic(), PANIC);
        assert!(!unsafe { std::ffi::CStr::from_ptr(ue_probe_last_error()) }.is_empty());
        let expected = abi();
        assert_eq!(unsafe { ue_probe_check_abi(&expected, size_of::<ProbeAbi>() as u32) }, OK);
        assert!(unsafe { std::ffi::CStr::from_ptr(ue_probe_last_error()) }.is_empty());
    }

    #[test]
    fn diagnostic_storage_is_thread_local() {
        ue_probe_panic();
        std::thread::spawn(|| {
            let expected = abi();
            assert_eq!(unsafe { ue_probe_check_abi(&expected, size_of::<ProbeAbi>() as u32) }, OK);
            assert!(unsafe { std::ffi::CStr::from_ptr(ue_probe_last_error()) }.is_empty());
        }).join().unwrap();
        assert!(!unsafe { std::ffi::CStr::from_ptr(ue_probe_last_error()) }.is_empty());
    }

    #[test]
    fn callback_preserves_user_data_and_reports_failure() {
        unsafe extern "C" fn callback(data: *mut c_void, value: Real) -> i32 {
            (unsafe { *data.cast::<i32>() }) + value as i32
        }
        unsafe extern "C" fn failed(_: *mut c_void, _: Real) -> i32 { -1 }

        let mut context = 7;
        let mut output = 0;
        assert_eq!(unsafe {
            ue_probe_callback(Some(callback), std::ptr::addr_of_mut!(context).cast(), 5.0, &mut output)
        }, OK);
        assert_eq!(output, 12);
        assert_eq!(unsafe {
            ue_probe_callback(Some(failed), std::ptr::null_mut(), 0.0, &mut output)
        }, CALLBACK_FAILED);
        assert_eq!(output, 12);
    }
}
