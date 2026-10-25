# -*- coding: utf-8 -*-
"""七一讲话 PPT 矢量配图元素库"""
import math

GOLD = "#EBCE86"
GOLD_D = "#D4AF37"
RED = "#C8161D"


def P(cx, cy, r, deg):
    """极坐标 -> 屏幕坐标（y 轴向下，deg 逆时针）"""
    a = math.radians(deg)
    return (cx + r * math.cos(a), cy - r * math.sin(a))


def poly(pts, fill="none", op=None):
    s = " ".join(f"{x:.1f},{y:.1f}" for x, y in pts)
    e = f' opacity="{op}"' if op is not None else ""
    return f'<polygon points="{s}" fill="{fill}"{e}/>'


def path(d, fill="none", stroke="none", sw=0, op=None):
    e = ""
    if stroke != "none":
        e += f' stroke="{stroke}" stroke-width="{sw}" stroke-linecap="round" stroke-linejoin="round"'
    if op is not None:
        e += f' opacity="{op}"'
    return f'<path d="{d}" fill="{fill}"{e}/>'


def circle(cx, cy, r, fill):
    return f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="{r:.1f}" fill="{fill}"/>'


def rect(x, y, w, h, fill, rx=0):
    r = f' rx="{rx}"' if rx else ""
    return f'<rect x="{x:.1f}" y="{y:.1f}" width="{w:.1f}" height="{h:.1f}" fill="{fill}"{r}/>'


def star(cx, cy, R, deg=-90, inner=0.382):
    pts = []
    for i in range(5):
        pts.append(P(cx, cy, R, deg + i * 72))
        pts.append(P(cx, cy, R * inner, deg + 36 + i * 72))
    return pts


def rays(cx, cy, r0, r1, n=28, deg=-90, spread=5.5, fill=GOLD, op=0.5):
    o = [f'<g fill="{fill}" opacity="{op}">']
    for i in range(n):
        a = deg + i * (360.0 / n)
        o.append(poly([P(cx, cy, r0, a), P(cx, cy, r1, a), P(cx, cy, r1, a + spread)]))
    o.append("</g>")
    return "".join(o)


# ================= 党徽：镰刀 + 锤头 =================
def _blade(cx, cy, R, w, a0, a1, steps=80):
    """新月形刀身：a0 为刀尾（厚），a1 为刀尖（收成一点）"""
    outer, inner = [], []
    for i in range(steps + 1):
        t = i / steps
        a = a0 + (a1 - a0) * t
        outer.append(P(cx, cy, R, a))
        inner.append(P(cx, cy, (R - w) + w * (t ** 2.4), a))
    return outer + inner[::-1]


def emblem(size=200):
    """中国共产党党徽。刀柄在左下，刀身自左下向上兜过顶部，刀尖指向右侧。"""
    out = [f'<g transform="scale({size / 200.0:.5f})">']
    bcx, bcy, R, w = 112, 126, 78, 24

    # 刀身
    out.append(poly(_blade(bcx, bcy, R, w, 208, 14), fill=GOLD))

    # 刀柄：自刀尾向左下伸出
    bx, by = P(bcx, bcy, R - w / 2.0, 208)
    hx, hy = P(bcx, bcy, R + 30, 224)
    out.append(path(f"M {bx:.1f} {by:.1f} L {hx:.1f} {hy:.1f}", stroke=GOLD, sw=15))
    out.append(circle(hx, hy, 7.5, GOLD))

    # 锤头：柄自左下斜向右上
    ax, ay = 58, 196
    tx, ty = 148, 66
    ang = math.degrees(math.atan2(-(ty - ay), tx - ax))
    out.append(path(f"M {ax} {ay} L {tx} {ty}", stroke=GOLD, sw=17))
    out.append(circle(ax, ay, 8.5, GOLD))
    out.append(f'<g transform="translate({tx},{ty}) rotate({-ang:.2f})">'
               f'<rect x="-14" y="-44" width="42" height="88" rx="8" fill="{GOLD}"/></g>')
    out.append("</g>")
    return "".join(out)


# ================= 天安门城楼剪影 =================
def tiananmen(w=420, h=205):
    o = ['<g>']
    cx = w / 2.0
    base = h - 4
    wall_h = 62                      # 城台高度
    wy = base - wall_h

    # 城台 + 底座
    o.append(rect(16, wy, w - 32, wall_h, GOLD))
    o.append(rect(4, base - 9, w - 8, 9, GOLD))

    # 五个券门（中间最大）
    doors = [(-132, 30), (-80, 34), (0, 46), (80, 34), (132, 30)]
    for dx, dw in doors:
        x = cx + dx - dw / 2.0
        ah = dw * 0.62
        o.append(f'<path d="M {x:.1f} {base-4} L {x:.1f} {wy+ah:.1f} '
                 f'A {dw/2.0:.1f} {dw/2.0:.1f} 0 0 1 {x+dw:.1f} {wy+ah:.1f} '
                 f'L {x+dw:.1f} {base-4} Z" fill="#8E0B12"/>')

    # ---- 下层重檐 ----
    e1 = wy
    o.append(path(f"M {cx-152:.1f} {e1:.1f} Q {cx-124:.1f} {e1-18:.1f} {cx-88:.1f} {e1-40:.1f} "
                  f"L {cx+88:.1f} {e1-40:.1f} Q {cx+124:.1f} {e1-18:.1f} {cx+152:.1f} {e1:.1f} Z", fill=GOLD))
    # 下层楼身
    o.append(rect(cx - 116, e1 - 88, 232, 48, GOLD))
    for i in range(11):
        o.append(rect(cx - 108 + i * 21.6, e1 - 88, 4.5, 48, "#8E0B12"))

    # ---- 上层重檐 ----
    e2 = e1 - 88
    o.append(path(f"M {cx-114:.1f} {e2:.1f} Q {cx-90:.1f} {e2-17:.1f} {cx-60:.1f} {e2-36:.1f} "
                  f"L {cx+60:.1f} {e2-36:.1f} Q {cx+90:.1f} {e2-17:.1f} {cx+114:.1f} {e2:.1f} Z", fill=GOLD))
    o.append(rect(cx - 72, e2 - 74, 144, 38, GOLD))
    for i in range(9):
        o.append(rect(cx - 64 + i * 16, e2 - 74, 4, 38, "#8E0B12"))
    # 顶层屋顶
    o.append(path(f"M {cx-88:.1f} {e2-74:.1f} Q {cx-64:.1f} {e2-92:.1f} {cx-34:.1f} {e2-112:.1f} "
                  f"L {cx+34:.1f} {e2-112:.1f} Q {cx+64:.1f} {e2-92:.1f} {cx+88:.1f} {e2-74:.1f} Z", fill=GOLD))
    o.append(rect(cx - 34, e2 - 119, 68, 7, GOLD))
    o.append(rect(cx - 4, e2 - 127, 8, 9, GOLD))
    o.append("</g>")
    return "".join(o)


# ================= 山峦 / 长城 =================
def mountains(w=1280, h=170, fill=GOLD, op=0.14, phase=0):
    """两层远近山剪影"""
    o = [f'<g fill="{fill}">']
    far = [f"M 0 {h} L 0 {h*0.66:.0f}"]
    for i in range(1, 41):
        t = i / 40.0
        y = h * 0.66 - (h * 0.40) * (0.5 + 0.5 * math.sin(t * 5.2 + phase)) * (0.55 + 0.45 * math.sin(t * 11.0 + phase))
        far.append(f"L {t*w:.0f} {y:.0f}")
    far.append(f"L {w} {h} Z")
    o.append(path(" ".join(far), fill=fill, op=op * 0.75))

    near = [f"M 0 {h} L 0 {h*0.82:.0f}"]
    for i in range(1, 41):
        t = i / 40.0
        y = h * 0.82 - (h * 0.28) * (0.5 + 0.5 * math.sin(t * 3.4 + 1.7)) * (0.6 + 0.4 * math.sin(t * 8.1 + 1.7))
        near.append(f"L {t*w:.0f} {y:.0f}")
    near.append(f"L {w} {h} Z")
    o.append(path(" ".join(near), fill=fill, op=op))
    o.append("</g>")
    return "".join(o)


def greatwall(w=1280, h=180, fill=GOLD, op=0.16):
    """长城：山脊上一道带厚度的城墙，含垛口与敌楼"""
    o = [f'<g fill="{fill}" opacity="{op}">']

    def ridge(x):
        t = x / float(w)
        return (h * 0.46
                + 26 * math.sin(t * math.pi * 2.3 + 0.6)
                + 11 * math.sin(t * math.pi * 6.1 + 1.4))

    def slope(x):
        t = x / float(w)
        return (h * 0.52
                + 16 * math.sin(t * math.pi * 3.1 + 2.2))

    # 山体
    m = [f"M 0 {h:.0f}"]
    for i in range(0, 121):
        x = w * i / 120.0
        m.append(f"L {x:.0f} {slope(x):.0f}")
    m.append(f"L {w} {h:.0f} Z")
    o.append(path(" ".join(m), fill=fill, op=0.55))

    # 城墙带
    th = 34
    top = [(w * i / 160.0, ridge(w * i / 160.0)) for i in range(161)]
    bot = [(x, y + th) for x, y in top]
    o.append(poly(top + bot[::-1]))

    # 垛口
    step = w / 46.0
    x = 0.0
    while x < w:
        y = ridge(x + step / 2)
        o.append(rect(x, y - 13, step * 0.68, 13, fill))
        x += step

    # 敌楼
    for t in (0.13, 0.37, 0.63, 0.87):
        x = t * w
        y = ridge(x)
        o.append(rect(x - 13, y - 46, 26, 46 + th, fill))
        o.append(rect(x - 17, y - 58, 34, 14, fill))
    o.append("</g>")
    return "".join(o)


# ================= 飘带 =================
def ribbon(w=520, h=170, fill=GOLD_D, op=0.9):
    """飘动的绶带（带宽随曲线自然起伏）"""
    top = (f"M 0 {h*0.60:.0f} "
           f"C {w*0.18:.0f} {h*0.06:.0f} {w*0.36:.0f} {h*1.02:.0f} {w*0.56:.0f} {h*0.50:.0f} "
           f"C {w*0.74:.0f} {h*0.04:.0f} {w*0.90:.0f} {h*0.84:.0f} {w} {h*0.36:.0f}")
    back = (f"L {w} {h*0.66:.0f} "
            f"C {w*0.90:.0f} {h*1.14:.0f} {w*0.74:.0f} {h*0.34:.0f} {w*0.56:.0f} {h*0.80:.0f} "
            f"C {w*0.36:.0f} {h*1.32:.0f} {w*0.18:.0f} {h*0.36:.0f} 0 {h*0.90:.0f} Z")
    return f'<g opacity="{op}">{path(top + " " + back, fill=fill)}</g>'


# ================= 旗帜 =================
def flag(w=300, h=200, fill=RED):
    """飘扬的红旗（旗面 + 旗杆）"""
    o = ['<g>']
    o.append(rect(0, 6, 6, h - 6, GOLD, rx=3))
    o.append(path(f"M 6 10 C {w*0.30:.0f} {h*0.02:.0f} {w*0.58:.0f} {h*0.30:.0f} {w:.0f} {h*0.14:.0f} "
                  f"L {w:.0f} {h*0.66:.0f} C {w*0.58:.0f} {h*0.82:.0f} {w*0.30:.0f} {h*0.54:.0f} 6 {h*0.62:.0f} Z",
                  fill=fill))
    o.append(poly(star(w * 0.28, h * 0.34, 20), fill=GOLD))
    o.append("</g>")
    return "".join(o)
