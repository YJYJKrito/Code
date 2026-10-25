# -*- coding: utf-8 -*-
"""还原 PPT 封面背景（去掉所有文字），输出 PNG。"""
import math
import numpy as np
from PIL import Image

# 与 CSS 中 1280x720 舞台对应的设计尺寸
DW, DH = 1280.0, 720.0

GOLD_RGB = (212, 175, 55)      # rgba(212,175,55,·)
RING_RGB = (235, 206, 134)     # rgba(235,206,134,·)

STOPS = [(0.00, (142, 11, 18)),   # #8E0B12
         (0.46, (103, 6, 12)),    # #67060C
         (1.00, (59, 3, 7))]      # #3B0307


def _lerp_stops(t):
    t = np.clip(t, 0.0, 1.0)
    out = np.zeros(t.shape + (3,), dtype=np.float64)
    for i in range(len(STOPS) - 1):
        p0, c0 = STOPS[i]
        p1, c1 = STOPS[i + 1]
        m = (t >= p0) & (t <= p1)
        if not m.any():
            continue
        k = ((t[m] - p0) / (p1 - p0))[..., None]
        out[m] = np.array(c0) * (1 - k) + np.array(c1) * k
    out[t <= STOPS[0][0]] = STOPS[0][1]
    out[t >= STOPS[-1][0]] = STOPS[-1][1]
    return out


def render(W, H, ss=2):
    """W,H 为输出尺寸；内部按 ss 倍超采样抗锯齿。"""
    w, h = W * ss, H * ss
    s = w / DW                                  # 统一的长度缩放
    y, x = np.mgrid[0:h, 0:w].astype(np.float64)

    # ---------- 1. 线性渐变 140deg ----------
    th = math.radians(140.0)
    dx, dy = math.sin(th), -math.cos(th)        # 屏幕坐标（y 向下）
    L = (abs(DW * math.sin(th)) + abs(DH * math.cos(th))) * ss
    t = ((x - w / 2.0) * dx + (y - h / 2.0) * dy) / L + 0.5
    img = _lerp_stops(t)

    # ---------- 2. 两个径向金色光晕 ----------
    def glow(cx_pct, cy_pct, rx, ry, a_max, stop):
        cx, cy = cx_pct * w, cy_pct * h
        rx, ry = rx * s, ry * s
        d = np.sqrt(((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2)
        a = np.clip(1.0 - d / stop, 0.0, 1.0) * a_max
        return a

    a2 = glow(0.06, 1.08, 760, 520, 0.13, 0.60)
    a1 = glow(0.82, -0.10, 1100, 620, 0.20, 0.62)
    for a in (a2, a1):                          # 先远的，后近的（CSS 靠后的层在下面）
        k = a[..., None]
        img = np.array(GOLD_RGB) * k + img * (1 - k)

    # ---------- 3. ::after 圆环装饰 ----------
    ccx, ccy = (DW - 520 + 120 + 260) * s, (-120 + 260) * s
    r = np.sqrt((x - ccx) ** 2 + (y - ccy) ** 2)

    def band(r_in, r_out, alpha):
        m = (r >= r_in) & (r <= r_out)
        if not m.any():
            return
        k = alpha
        img[m] = np.array(RING_RGB) * k + img[m] * (1 - k)

    band(260 * s, 390 * s, 0.03)                # box-shadow 130px
    band(260 * s, 320 * s, 0.05)                # box-shadow  60px
    band(259.5 * s, 260.5 * s, 0.22)            # border 1px

    img = np.clip(img, 0, 255).astype(np.uint8)
    im = Image.fromarray(img, "RGB")
    if ss != 1:
        im = im.resize((W, H), Image.LANCZOS)
    return im


if __name__ == "__main__":
    render(1280, 720).save(r"E:\CODE\cover-bg-1280x720.png")
    render(1920, 1080).save(r"E:\CODE\cover-bg-1920x1080.png")
    print("done")
