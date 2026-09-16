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
float cuebox_yin(const float *buf, size_t len, int sample_rate, float thresh);
float cuebox_snap_hz(float freq, int key, int scale);
void cuebox_pitch_shift(
    float *stereo,
    size_t frames,
    float *delay,
    size_t delay_len,
    int *write,
    float *read,
    float ratio,
    float amount,
    int formant);

#ifdef __cplusplus
}
#endif
