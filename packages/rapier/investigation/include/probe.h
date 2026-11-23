#ifndef UNIENGINE_RAPIER_PROBE_H
#define UNIENGINE_RAPIER_PROBE_H

/* Stage 0 experiment only. This is not the production PhysicalSpace ABI. */
#include <stdint.h>
#include <stddef.h>

#if !defined(UE_PROBE_DIMENSION) || (UE_PROBE_DIMENSION != 2 && UE_PROBE_DIMENSION != 3)
#error Define UE_PROBE_DIMENSION as 2 or 3.
#endif

#ifdef UE_PROBE_F64
typedef double UeProbeReal;
#else
typedef float UeProbeReal;
#endif

typedef struct UeProbeAbi {
    uint32_t revision;
    uint32_t dimension;
    uint32_t real_size;
    uint32_t pointer_size;
    uint32_t options;
    uint32_t result_size;
    uint32_t result_alignment;
    uint32_t result_offsets[8];
} UeProbeAbi;

typedef struct UeProbeResult {
    UeProbeReal start_height;
    UeProbeReal end_height;
    UeProbeReal distance;
    UeProbeReal shape_radius;
    uint32_t body_count;
    uint32_t collider_count;
    uint32_t active_intersections;
    uint32_t stale_handle_rejected;
} UeProbeResult;

typedef int32_t (__cdecl *UeProbeCallback)(void *user_data, UeProbeReal value);
typedef uint32_t (__cdecl *ue_probe_get_abi_fn)(UeProbeAbi *output, uint32_t output_size);
typedef uint32_t (__cdecl *ue_probe_check_abi_fn)(const UeProbeAbi *expected, uint32_t expected_size);
typedef uint32_t (__cdecl *ue_probe_run_fn)(UeProbeReal dt, UeProbeResult *output, uint32_t output_size);
typedef uint32_t (__cdecl *ue_probe_callback_fn)(
    UeProbeCallback callback, void *user_data, UeProbeReal value, int32_t *output);
typedef const char *(__cdecl *ue_probe_last_error_fn)(void);
typedef uint32_t (__cdecl *ue_probe_panic_fn)(void);

uint32_t __cdecl ue_probe_get_abi(UeProbeAbi *output, uint32_t output_size);
uint32_t __cdecl ue_probe_check_abi(const UeProbeAbi *expected, uint32_t expected_size);
uint32_t __cdecl ue_probe_run(UeProbeReal dt, UeProbeResult *output, uint32_t output_size);
uint32_t __cdecl ue_probe_callback(UeProbeCallback callback, void *user_data, UeProbeReal value, int32_t *output);
const char *__cdecl ue_probe_last_error(void);
uint32_t __cdecl ue_probe_panic(void);

#endif
