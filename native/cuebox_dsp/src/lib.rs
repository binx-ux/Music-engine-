#![allow(clippy::missing_safety_doc)]

#[no_mangle]
pub unsafe extern "C" fn cuebox_scale(buf: *mut f32, len: usize, gain: f32) {
    if buf.is_null() || len == 0 {
        return;
    }
    let s = std::slice::from_raw_parts_mut(buf, len);
    for x in s {
        *x *= gain;
    }
}

#[no_mangle]
pub unsafe extern "C" fn cuebox_add(dest: *mut f32, src: *const f32, len: usize, gain: f32) {
    if dest.is_null() || src.is_null() || len == 0 {
        return;
    }
    let d = std::slice::from_raw_parts_mut(dest, len);
    let s = std::slice::from_raw_parts(src, len);
    for i in 0..len {
        d[i] += s[i] * gain;
    }
}

#[no_mangle]
pub unsafe extern "C" fn cuebox_peak(buf: *const f32, len: usize) -> f32 {
    if buf.is_null() || len == 0 {
        return 0.0;
    }
    let s = std::slice::from_raw_parts(buf, len);
    let mut p = 0.0f32;
    for &x in s {
        let a = x.abs();
        if a > p {
            p = a;
        }
    }
    p
}

#[no_mangle]
pub unsafe extern "C" fn cuebox_route(
    dest: *mut f32,
    a: *const f32,
    ga: f32,
    b: *const f32,
    gb: f32,
    c: *const f32,
    gc: f32,
    len: usize,
    master: f32,
) {
    if dest.is_null() || len == 0 {
        return;
    }
    let d = std::slice::from_raw_parts_mut(dest, len);
    let sa = if a.is_null() {
        None
    } else {
        Some(std::slice::from_raw_parts(a, len))
    };
    let sb = if b.is_null() {
        None
    } else {
        Some(std::slice::from_raw_parts(b, len))
    };
    let sc = if c.is_null() {
        None
    } else {
        Some(std::slice::from_raw_parts(c, len))
    };
    for i in 0..len {
        let mut v = 0.0;
        if let Some(s) = sa {
            v += s[i] * ga;
        }
        if let Some(s) = sb {
            v += s[i] * gb;
        }
        if let Some(s) = sc {
            v += s[i] * gc;
        }
        d[i] = v * master;
    }
}
