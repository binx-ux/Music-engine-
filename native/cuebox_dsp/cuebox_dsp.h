#pragma once
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

void cuebox_scale(float *buf, size_t len, float gain);
void cuebox_add(float *dest, const float *src, size_t len, float gain);
float cuebox_peak(const float *buf, size_t len);
void cuebox_route(
    float *dest,
    const float *a, float ga,
    const float *b, float gb,
    const float *c, float gc,
    size_t len,
    float master);

#ifdef __cplusplus
}
#endif
