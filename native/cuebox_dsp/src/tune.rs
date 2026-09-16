#![allow(clippy::missing_safety_doc)]

const MAJOR: [i32; 7] = [0, 2, 4, 5, 7, 9, 11];
const MINOR: [i32; 7] = [0, 2, 3, 5, 7, 8, 10];

pub fn yin_hz(buf: &[f32], sample_rate: i32, thresh: f32) -> f32 {
    let n = buf.len();
    if n < 256 || sample_rate < 8000 {
        return 0.0;
    }
    let half = n / 2;
    if half < 8 {
        return 0.0;
    }
    let sr = sample_rate as f32;
    let max_span = (half - 2).min(512);
    if max_span <= 4 {
        return 0.0;
    }
    let mut min_lag = ((sr / 900.0) as usize).clamp(2, max_span);
    let mut max_lag = ((sr / 65.0) as usize).min(max_span);
    if max_lag <= min_lag {
        min_lag = 2;
        max_lag = max_span;
    }
    if max_lag <= min_lag {
        return 0.0;
    }

    let mut diff = [0.0f32; 514];
    for tau in 1..=max_lag {
        let mut sum = 0.0f32;
        let last = n - tau;
        let mut i = 0;
        while i + 4 < last {
            let d0 = buf[i] - buf[i + tau];
            let d1 = buf[i + 1] - buf[i + 1 + tau];
            let d2 = buf[i + 2] - buf[i + 2 + tau];
            let d3 = buf[i + 3] - buf[i + 3 + tau];
            sum += d0 * d0 + d1 * d1 + d2 * d2 + d3 * d3;
            i += 4;
        }
        while i < last {
            let d = buf[i] - buf[i + tau];
            sum += d * d;
            i += 1;
        }
        diff[tau] = sum;
    }

    let mut cmndf = [1.0f32; 514];
    let mut running = 0.0f32;
    for tau in 1..=max_lag {
        running += diff[tau];
        cmndf[tau] = diff[tau] * tau as f32 / running.max(1.0e-12);
    }

    let mut tau = min_lag;
    while tau <= max_lag && cmndf[tau] > thresh {
        tau += 1;
    }
    if tau > max_lag || tau + 1 >= cmndf.len() {
        return 0.0;
    }
    while tau + 1 <= max_lag && cmndf[tau + 1] < cmndf[tau] {
        tau += 1;
    }

    let s0 = cmndf[tau - 1];
    let s1 = cmndf[tau];
    let s2 = cmndf[tau + 1];
    let den = 2.0 * s1 - s2 - s0;
    let mut better = tau as f32;
    if den.abs() > 1.0e-8 {
        better += 0.5 * (s0 - s2) / den;
    }
    if !better.is_finite() || better < 2.0 {
        return 0.0;
    }
    let hz = sr / better;
    if hz < 70.0 || hz > 900.0 {
        0.0
    } else {
        hz
    }
}

pub fn snap_hz(freq: f32, key: i32, scale: i32) -> f32 {
    if !freq.is_finite() || freq < 70.0 || freq > 900.0 {
        return 0.0;
    }
    let midi = 69.0 + 12.0 * (freq / 440.0).log2();
    let mut nearest = midi.round();
    if scale != 0 {
        let allowed: &[i32] = if scale == 2 { &MINOR } else { &MAJOR };
        let key = ((key % 12) + 12) % 12;
        if !in_scale(nearest, key, allowed) {
            let mut down = nearest - 1.0;
            let mut up = nearest + 1.0;
            for _ in 0..6 {
                if in_scale(down, key, allowed) {
                    nearest = down;
                    break;
                }
                if in_scale(up, key, allowed) {
                    nearest = up;
                    break;
                }
                down -= 1.0;
                up += 1.0;
            }
        }
    }
    440.0 * 2.0f32.powf((nearest - 69.0) / 12.0)
}

fn in_scale(midi: f32, key: i32, allowed: &[i32]) -> bool {
    let mut pc = midi as i32 - key;
    pc %= 12;
    if pc < 0 {
        pc += 12;
    }
    allowed.contains(&pc)
}

fn wrap(x: f32, n: f32) -> f32 {
    let mut v = x % n;
    if v < 0.0 {
        v += n;
    }
    v
}

fn interp(d: &[f32], pos: f32) -> f32 {
    let n = d.len();
    if n < 2 {
        return 0.0;
    }
    let p = wrap(pos, n as f32);
    let i0 = p.floor() as usize % n;
    let f = p - p.floor();
    let i1 = (i0 + 1) % n;
    d[i0] + (d[i1] - d[i0]) * f
}

fn tap_gain(read: f32, write: usize, len: usize) -> f32 {
    let n = len as f32;
    let dist = wrap(write as f32 - read, n) / n;
    0.5 - 0.5 * (std::f32::consts::TAU * dist).cos()
}

pub fn pitch_shift(
    stereo: &mut [f32],
    frames: usize,
    delay: &mut [f32],
    write: &mut i32,
    read: &mut f32,
    ratio: f32,
    amount: f32,
    formant: bool,
) {
    let delay_len = delay.len();
    if frames == 0 || delay_len < 64 || stereo.len() < frames * 2 {
        return;
    }
    let ratio = ratio.clamp(0.5, 2.0);
    let blend = amount.clamp(0.0, 1.0);
    let dl = delay_len as f32;
    let half = dl * 0.5;
    let mut w = ((*write as usize) % delay_len + delay_len) % delay_len;
    let mut r = wrap(*read, dl);

    for i in 0..frames {
        let l = stereo[i * 2];
        let rch = stereo[i * 2 + 1];
        let dry = 0.5 * (l + rch);
        delay[w] = dry;

        r = wrap(r + ratio, dl);
        let r2 = wrap(r + half, dl);
        let g1 = tap_gain(r, w, delay_len);
        let g2 = tap_gain(r2, w, delay_len);
        let gsum = (g1 + g2).max(1.0e-4);
        let mut wet = (interp(delay, r) * g1 + interp(delay, r2) * g2) / gsum;

        if formant {
            wet = wet * 0.82 + dry * 0.18;
        }

        let o = dry + (wet - dry) * blend;
        stereo[i * 2] = o;
        stereo[i * 2 + 1] = o;

        w += 1;
        if w >= delay_len {
            w = 0;
        }
    }

    *write = w as i32;
    *read = r;
}

#[no_mangle]
pub unsafe extern "C" fn cuebox_yin(buf: *const f32, len: usize, sample_rate: i32, thresh: f32) -> f32 {
    if buf.is_null() || len == 0 {
        return 0.0;
    }
    let s = std::slice::from_raw_parts(buf, len);
    yin_hz(s, sample_rate, thresh.max(0.05).min(0.4))
}

#[no_mangle]
pub unsafe extern "C" fn cuebox_snap_hz(freq: f32, key: i32, scale: i32) -> f32 {
    snap_hz(freq, key, scale)
}

#[no_mangle]
pub unsafe extern "C" fn cuebox_pitch_shift(
    stereo: *mut f32,
    frames: usize,
    delay: *mut f32,
    delay_len: usize,
    write: *mut i32,
    read: *mut f32,
    ratio: f32,
    amount: f32,
    formant: i32,
) {
    if stereo.is_null() || delay.is_null() || write.is_null() || read.is_null() || frames == 0 {
        return;
    }
    let buf = std::slice::from_raw_parts_mut(stereo, frames * 2);
    let d = std::slice::from_raw_parts_mut(delay, delay_len);
    pitch_shift(
        buf,
        frames,
        d,
        &mut *write,
        &mut *read,
        ratio,
        amount,
        formant != 0,
    );
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn sine_440() {
        let sr = 48000i32;
        let n = 2048usize;
        let mut buf = vec![0.0f32; n];
        for i in 0..n {
            buf[i] = (2.0 * std::f32::consts::PI * 440.0 * i as f32 / sr as f32).sin();
        }
        let hz = yin_hz(&buf, sr, 0.15);
        assert!((hz - 440.0).abs() < 12.0, "got {hz}");
    }

    #[test]
    fn snap_c_major() {
        let hz = snap_hz(450.0, 0, 1);
        assert!(hz > 0.0);
        let midi = 69.0 + 12.0 * (hz / 440.0).log2();
        let pc = ((midi.round() as i32) % 12 + 12) % 12;
        assert!(MAJOR.contains(&pc));
    }
}
