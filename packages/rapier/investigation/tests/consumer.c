#include "probe.h"
#include <windows.h>
#include <math.h>
#include <stdio.h>
#include <string.h>

static int assertions = 0;
#define CHECK(condition) do { \
    ++assertions; \
    if (!(condition)) { \
        fprintf(stderr, "FAIL line %d: %s\n", __LINE__, #condition); \
        return 1; \
    } \
} while (0)

#define LOAD(name) \
    name##_fn call_##name = (name##_fn)GetProcAddress(library, #name); \
    CHECK(call_##name != NULL)

static int32_t __cdecl callback(void *user_data, UeProbeReal value)
{
    int32_t *context = (int32_t *)user_data;
    *context += (int32_t)value;
    return *context;
}

static int32_t __cdecl failed_callback(void *user_data, UeProbeReal value)
{
    (void)user_data;
    (void)value;
    return -1;
}

int wmain(int argc, wchar_t **argv)
{
    CHECK(argc == 2);
    HMODULE library = LoadLibraryW(argv[1]);
    CHECK(library != NULL);
    LOAD(ue_probe_get_abi);
    LOAD(ue_probe_check_abi);
    LOAD(ue_probe_run);
    LOAD(ue_probe_callback);
    LOAD(ue_probe_last_error);
    LOAD(ue_probe_panic);

    /* Construct the expectation from this C compiler and header, not from the DLL. */
    UeProbeAbi expected = {
        1, UE_PROBE_DIMENSION, sizeof(UeProbeReal), sizeof(void *), 0,
        sizeof(UeProbeResult), _Alignof(UeProbeResult), {
            offsetof(UeProbeResult, start_height),
            offsetof(UeProbeResult, end_height),
            offsetof(UeProbeResult, distance),
            offsetof(UeProbeResult, shape_radius),
            offsetof(UeProbeResult, body_count),
            offsetof(UeProbeResult, collider_count),
            offsetof(UeProbeResult, active_intersections),
            offsetof(UeProbeResult, stale_handle_rejected),
        },
    };
    UeProbeAbi actual = {0};
    CHECK(call_ue_probe_get_abi(&actual, sizeof(actual)) == 0);
    CHECK(memcmp(&actual, &expected, sizeof(actual)) == 0);
    CHECK(call_ue_probe_check_abi(&expected, sizeof(expected)) == 0);

    for (size_t index = 0; index < 15; ++index) {
        UeProbeAbi changed = expected;
        uint32_t words[15];
        memcpy(words, &changed, sizeof(words));
        words[index] ^= 1;
        memcpy(&changed, words, sizeof(words));
        CHECK(call_ue_probe_check_abi(&changed, sizeof(changed)) == 2);
    }
    CHECK(call_ue_probe_check_abi(&expected, sizeof(expected) - 1) == 2);
    CHECK(call_ue_probe_check_abi(NULL, sizeof(expected)) == 1);
    CHECK(strlen(call_ue_probe_last_error()) > 0);
    CHECK(call_ue_probe_check_abi(&expected, sizeof(expected)) == 0);
    CHECK(strlen(call_ue_probe_last_error()) == 0);

    UeProbeResult result = {0};
    CHECK(call_ue_probe_run((UeProbeReal)(1.0 / 60.0), &result, sizeof(result)) == 0);
    CHECK(result.end_height < result.start_height);
    CHECK(fabs((double)result.distance - 2.0) < 1.0e-5);
    CHECK(result.shape_radius == (UeProbeReal)0.5);
    CHECK(result.body_count == 2 && result.collider_count == 1);
    CHECK(result.active_intersections == 1 && result.stale_handle_rejected == 1);

    result.start_height = 123;
    CHECK(call_ue_probe_run(0, &result, sizeof(result)) == 1);
    CHECK(result.start_height == 123);
    CHECK(call_ue_probe_run((UeProbeReal)NAN, &result, sizeof(result)) == 1);
    CHECK(call_ue_probe_run((UeProbeReal)INFINITY, &result, sizeof(result)) == 1);
    CHECK(call_ue_probe_run((UeProbeReal)0.1, &result, 0) == 2);

    int32_t context = 7;
    int32_t callback_result = 0;
    CHECK(call_ue_probe_callback(callback, &context, 5, &callback_result) == 0);
    CHECK(context == 12 && callback_result == 12);
    CHECK(call_ue_probe_callback(failed_callback, NULL, 0, &callback_result) == 4);
    CHECK(callback_result == 12);
    CHECK(call_ue_probe_callback(NULL, NULL, 0, &callback_result) == 1);

    CHECK(call_ue_probe_panic() == 3);
    CHECK(strlen(call_ue_probe_last_error()) > 0);
    CHECK(call_ue_probe_run((UeProbeReal)(1.0 / 60.0), &result, sizeof(result)) == 0);
    CHECK(FreeLibrary(library));
    printf("PASS dimension=%u precision=%zu assertions=%d\n",
        (unsigned)UE_PROBE_DIMENSION, sizeof(UeProbeReal) * 8, assertions);
    return 0;
}
