# -*- coding: utf-8 -*-
"""
FIRR 敏感性分析：销售价格 / 经营成本

基础数据取自《项目现金流量表》(建设期2年 + 投产期2年 + 达产期11年, 共15年)。
基准折现率 ic = 10% (由表中"折现净现金流量"行反推: 12108.98/1.1 = 11008.16)。

计税口径(由基表精确反推, 三年数据全部吻合):
    所得税 = 15% × (销售收入 − 经营成本 − 销售税金及附加)
    即折旧摊销为 0、所得税率 15%
  校验: y3 0.15×(68216.19−52180.18−3208.39)=1924.14 基表 1924.14
        y4 0.15×(87706.53−67088.81−4125.07)=2473.90 基表 2473.90
        y5 0.15×(97451.70−74543.12−4583.41)=2748.78 基表 2748.77

变动规则:
    销售价格 ±Δ  -> 销售收入、销售税金及附加同比例联动, 所得税按上式重算
    经营成本 ±Δ  -> 仅经营成本变动, 所得税按上式重算

输出:
    firr_sensitivity.csv     敏感性分析数据表
    sensitivity_chart.svg    独立 SVG 图(可插入 Word / PPT)
    敏感性分析图.html         交互版(悬停读数、数据表、深色模式)
"""

import csv
import math
import os

OUT_DIR = os.path.dirname(os.path.abspath(__file__))

# ---------------------------------------------------------------- 1. 基础数据
YEARS = list(range(1, 16))
LOAD = [0.0, 0.0, 0.7, 0.9] + [1.0] * 11       # 生产负荷

REV_DA = 97451.70        # 达产期 销售收入
COST_DA = 74543.12       # 达产期 经营成本
TAXSUR_DA = 4583.41      # 达产期 销售税金及附加

CONSTR = [10602.625, 10602.625] + [0.0] * 13   # 建设期投资
WCAP = [1506.358, 1506.358] + [0.0] * 13       # 流动资金
LOAN = {3: 2062.07, 4: 2651.24, 5: 2945.82, 6: 2945.82, 7: 2945.82}   # 偿还本息

REVENUE = [REV_DA * k for k in LOAD]
OPCOST = [COST_DA * k for k in LOAD]
TAXSUR = [TAXSUR_DA * k for k in LOAD]
LOANS = [LOAN.get(t, 0.0) for t in YEARS]

TAX_RATE = 0.15          # 所得税率(反推)
IC = 0.10                # 基准折现率

# ---------------------------------------------------------------- 2. 现金流模型


def cash_flow(price_delta=0.0, cost_delta=0.0):
    """返回 (净现金流量, 所得税, 销售收入, 经营成本, 销售税金及附加) 各 15 年序列。"""
    ncf, itax, rev_s, cost_s, tax_s = [], [], [], [], []
    for i in range(len(YEARS)):
        rev = REVENUE[i] * (1 + price_delta)
        cost = OPCOST[i] * (1 + cost_delta)
        taxsur = TAXSUR[i] * (1 + price_delta)
        # 所得税: 利润总额 × 15%, 亏损年份为 0
        profit = rev - cost - taxsur
        tax = max(0.0, profit * TAX_RATE)
        outflow = CONSTR[i] + WCAP[i] + cost + taxsur + LOANS[i] + tax
        ncf.append(rev - outflow)
        itax.append(tax)
        rev_s.append(rev)
        cost_s.append(cost)
        tax_s.append(taxsur)
    return ncf, itax, rev_s, cost_s, tax_s


def npv(rate, cf):
    return sum(c / (1.0 + rate) ** (i + 1) for i, c in enumerate(cf))


def firr(cf, lo=-0.90, hi=20.0, tol=1e-11):
    """二分法求 FIRR。全部现金流同号(无符号变化)时返回 None —— 此时 FIRR 不存在。"""
    f_lo, f_hi = npv(lo, cf), npv(hi, cf)
    if f_lo * f_hi > 0:
        return None
    for _ in range(400):
        mid = 0.5 * (lo + hi)
        if npv(mid, cf) * f_hi <= 0:
            lo = mid
        else:
            hi = mid
        if hi - lo < tol:
            break
    return 0.5 * (lo + hi)


def solve_delta(target, factor):
    """求使 FIRR = target 的因素变化率(临界点)。"""
    def g(d):
        r = firr(cash_flow(**{factor + "_delta": d})[0])
        return (r if r is not None else -0.90) - target

    lo, hi = -0.90, 3.0
    if g(lo) * g(hi) > 0:
        return None
    for _ in range(400):
        mid = 0.5 * (lo + hi)
        if g(lo) * g(mid) <= 0:
            hi = mid
        else:
            lo = mid
        if hi - lo < 1e-13:
            break
    return 0.5 * (lo + hi)


# ---------------------------------------------------------------- 3. 计算
DELTAS = [-0.20, -0.15, -0.10, -0.05, 0.0, 0.05, 0.10, 0.15, 0.20]

base_cf, base_tax, *_ = cash_flow()
BASE_IRR = firr(base_cf)
BASE_NPV = npv(IC, base_cf)

price_irr = [firr(cash_flow(price_delta=d)[0]) for d in DELTAS]
cost_irr = [firr(cash_flow(cost_delta=d)[0]) for d in DELTAS]

CP_PRICE = solve_delta(IC, "price")    # 价格临界点
CP_COST = solve_delta(IC, "cost")      # 成本临界点


def sens_coef(curve, idx_a, idx_b):
    """敏感度系数 = (ΔFIRR/FIRR) / (Δ因素/因素), 取正负两侧的平均。"""
    vals = [(curve[idx_a], DELTAS[idx_a]), (curve[idx_b], DELTAS[idx_b])]
    if any(v is None for v, _ in vals):
        return None
    return sum((v - BASE_IRR) / BASE_IRR / d for v, d in vals) / 2


S_PRICE = sens_coef(price_irr, 3, 5)   # ±5%
S_COST = sens_coef(cost_irr, 3, 5)

# ---------------------------------------------------------------- 4. 控制台校验
print("=" * 78)
print("一、基表现金流量复核 (万元)")
print("-" * 78)
hdr = f"{'年份':<5}{'销售收入':>12}{'经营成本':>12}{'销售税金及附加':>15}{'偿还本息':>11}{'所得税':>11}{'净现金流量':>13}"
print(hdr)
for i, t in enumerate(YEARS):
    print(f"{t:<5}{REVENUE[i]:>12.2f}{OPCOST[i]:>12.2f}{TAXSUR[i]:>15.2f}"
          f"{LOANS[i]:>11.2f}{base_tax[i]:>11.2f}{base_cf[i]:>13.2f}")
print("-" * 78)
print(f"所得税公式校验: y3 {base_tax[2]:.2f} (基表 1924.14) | "
      f"y4 {base_tax[3]:.2f} (基表 2473.90) | y5 {base_tax[4]:.2f} (基表 2748.77)")
print(f"基准 FIRR = {BASE_IRR*100:.2f}%   ic = {IC*100:.0f}%   基准 NPV = {BASE_NPV:,.2f} 万元")

print()
print("二、敏感性分析表 (FIRR, %)")
print("-" * 78)
print(f"{'因素':<10}" + "".join(f"{d*100:>+8.0f}%" for d in DELTAS))
for name, curve in (("销售价格", price_irr), ("经营成本", cost_irr)):
    row = "".join(f"{'  不可行' if v is None else format(v*100, '>9.2f')}" for v in curve)
    print(f"{name:<10}" + row)
print("-" * 78)
print(f"敏感度系数(±5%)   销售价格 {S_PRICE:+.3f}   经营成本 {S_COST:+.3f}")
print(f"临界点(FIRR=ic)   销售价格 {CP_PRICE*100:+.2f}%   经营成本 {CP_COST*100:+.2f}%")
print("=" * 78)

# ---------------------------------------------------------------- 5. CSV
with open(os.path.join(OUT_DIR, "firr_sensitivity.csv"), "w", newline="", encoding="utf-8-sig") as f:
    w = csv.writer(f)
    w.writerow(["因素"] + [f"{d*100:+.0f}%" for d in DELTAS])
    w.writerow(["销售价格 FIRR(%)"] + ["不可行" if v is None else f"{v*100:.2f}" for v in price_irr])
    w.writerow(["经营成本 FIRR(%)"] + ["不可行" if v is None else f"{v*100:.2f}" for v in cost_irr])
    w.writerow([])
    w.writerow(["基准 FIRR(%)", f"{BASE_IRR*100:.2f}"])
    w.writerow(["基准折现率 ic(%)", f"{IC*100:.0f}"])
    w.writerow(["基准 NPV(万元)", f"{BASE_NPV:.2f}"])
    w.writerow(["敏感度系数 销售价格", f"{S_PRICE:.3f}"])
    w.writerow(["敏感度系数 经营成本", f"{S_COST:.3f}"])
    w.writerow(["临界点 销售价格(%)", f"{CP_PRICE*100:.2f}"])
    w.writerow(["临界点 经营成本(%)", f"{CP_COST*100:.2f}"])

# ---------------------------------------------------------------- 6. SVG
W, H = 960, 610
ML, MR, MT, MB = 86, 138, 78, 92
PW, PH = W - ML - MR, H - MT - MB

PAL = {                                   # dataviz 参考调色板 slot1 / slot2
    "surface": ("#fcfcfb", "var(--surface-1)"),
    "ink":     ("#0b0b0b", "var(--text-primary)"),
    "ink2":    ("#52514e", "var(--text-secondary)"),
    "muted":   ("#898781", "var(--text-muted)"),
    "grid":    ("#e1e0d9", "var(--gridline)"),
    "axis":    ("#c3c2b7", "var(--axis)"),
    "s1":      ("#2a78d6", "var(--series-1)"),
    "s2":      ("#eb6834", "var(--series-2)"),
}

X_LO, X_HI = -0.215, 0.215
Y_LO, Y_HI = -0.10, 0.80


def nice_ticks(lo, hi, maxn=10):
    """生成"整齐"的刻度序列。"""
    raw = (hi - lo) / maxn
    mag = 10.0 ** math.floor(math.log10(raw))
    step = next(m * mag for m in (1, 2, 2.5, 5, 10) if raw <= m * mag)
    out, v = [], math.ceil(lo / step - 1e-9) * step
    while v <= hi + 1e-9:
        out.append(round(v, 10))
        v += step
    return out


Y_TICKS = nice_ticks(Y_LO, Y_HI, 9)


def col(k, mode):
    return PAL[k][1] if mode == "vars" else PAL[k][0]


def sx(d):
    return ML + (d - X_LO) / (X_HI - X_LO) * PW


def sy(r):
    return MT + (Y_HI - r) / (Y_HI - Y_LO) * PH


def build_svg(mode="light", interactive=False):
    L, a = [], None
    a = L.append
    s1, s2 = col("s1", mode), col("s2", mode)
    surf, ink, ink2 = col("surface", mode), col("ink", mode), col("ink2", mode)
    muted, grid, axis = col("muted", mode), col("grid", mode), col("axis", mode)

    a(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}" '
      f'font-family="system-ui,-apple-system,\'Segoe UI\',\'Microsoft YaHei\',sans-serif" '
      f'role="img" aria-label="FIRR 对销售价格与经营成本的敏感性分析图">')
    a(f'<rect width="{W}" height="{H}" fill="{surf}"/>')

    # 标题
    a(f'<text x="{ML}" y="34" font-size="19" font-weight="600" fill="{ink}">'
      f'FIRR 敏感性分析图 — 销售价格 / 经营成本</text>')
    a(f'<text x="{ML}" y="56" font-size="12.5" fill="{ink2}">'
      f'基准 FIRR = {BASE_IRR*100:.2f}%　基准收益率 i_c = {IC*100:.0f}%（虚线）　'
      f'所得税按 15%×(收入−经营成本−税金及附加) 重算</text>')

    # 横向网格 + y 轴刻度
    for yt in Y_TICKS:
        y = sy(yt)
        a(f'<line x1="{ML}" y1="{y:.1f}" x2="{ML+PW}" y2="{y:.1f}" stroke="{grid}" stroke-width="1"/>')
        a(f'<text x="{ML-12}" y="{y+4:.1f}" font-size="12" text-anchor="end" fill="{muted}" '
          f'style="font-variant-numeric:tabular-nums">{yt*100:.0f}%</text>')

    # x=0 与 ic 参考线
    a(f'<line x1="{sx(0):.1f}" y1="{MT}" x2="{sx(0):.1f}" y2="{MT+PH}" '
      f'stroke="{axis}" stroke-width="1" stroke-dasharray="3 3"/>')
    y_ic = sy(IC)
    a(f'<line x1="{ML}" y1="{y_ic:.1f}" x2="{ML+PW}" y2="{y_ic:.1f}" stroke="{muted}" '
      f'stroke-width="1.5" stroke-dasharray="7 4"/>')
    a(f'<text x="{ML+8}" y="{y_ic+17:.1f}" font-size="11.5" text-anchor="start" fill="{muted}">'
      f'i_c = {IC*100:.0f}%</text>')

    # 临界点竖线
    for cp, ckey in ((CP_PRICE, s1), (CP_COST, s2)):
        if cp is None or not (X_LO < cp < X_HI):
            continue
        x = sx(cp)
        a(f'<line x1="{x:.1f}" y1="{y_ic:.1f}" x2="{x:.1f}" y2="{MT+PH}" stroke="{ckey}" '
          f'stroke-width="1" stroke-dasharray="2 3" opacity="0.55"/>')
        a(f'<text x="{x:.1f}" y="{MT+PH-6:.1f}" font-size="10.5" text-anchor="middle" '
          f'fill="{ckey}" opacity="0.9">{cp*100:+.1f}%</text>')

    # 坐标轴
    a(f'<line x1="{ML}" y1="{MT+PH}" x2="{ML+PW}" y2="{MT+PH}" stroke="{axis}" stroke-width="1"/>')
    for d in DELTAS:
        x = sx(d)
        a(f'<line x1="{x:.1f}" y1="{MT+PH}" x2="{x:.1f}" y2="{MT+PH+5}" stroke="{axis}" stroke-width="1"/>')
        a(f'<text x="{x:.1f}" y="{MT+PH+22}" font-size="12" text-anchor="middle" fill="{muted}" '
          f'style="font-variant-numeric:tabular-nums">{d*100:+.0f}%</text>')
    a(f'<text x="{ML+PW/2:.0f}" y="{MT+PH+54}" font-size="13" text-anchor="middle" fill="{ink2}">'
      f'不确定因素变化率</text>')
    a(f'<text x="22" y="{MT+PH/2:.0f}" font-size="13" text-anchor="middle" fill="{ink2}" '
      f'transform="rotate(-90 22 {MT+PH/2:.0f})">财务内部收益率 FIRR</text>')

    # 数据线
    series = (("销售价格", price_irr, "s1"), ("经营成本", cost_irr, "s2"))
    for name, data, ckey in series:
        c = col(ckey, mode)
        pts = [(sx(d), sy(v)) for d, v in zip(DELTAS, data) if v is not None]
        a(f'<polyline points="{" ".join(f"{x:.1f},{y:.1f}" for x, y in pts)}" fill="none" '
          f'stroke="{c}" stroke-width="2.5" stroke-linejoin="round" stroke-linecap="round"/>')
        for d, v in zip(DELTAS, data):
            if v is None:
                # FIRR 不存在的点: 空心标记压在坐标轴上
                a(f'<circle cx="{sx(d):.1f}" cy="{MT+PH-1:.1f}" r="5.5" fill="{surf}" '
                  f'stroke="{c}" stroke-width="2" stroke-dasharray="3 2.5"/>')
            else:
                a(f'<circle cx="{sx(d):.1f}" cy="{sy(v):.1f}" r="4.5" fill="{c}" '
                  f'stroke="{surf}" stroke-width="2"/>')

    # 右端直接标注
    for name, data, ckey in series:
        last = [(d, v) for d, v in zip(DELTAS, data) if v is not None][-1]
        y = min(max(sy(last[1]), MT + 12), MT + PH - 6)
        x = sx(last[0]) + 15
        a(f'<text x="{x:.1f}" y="{y-2:.1f}" font-size="12.5" font-weight="600" fill="{ink}">{name}</text>')
        a(f'<text x="{x:.1f}" y="{y+15:.1f}" font-size="12" fill="{ink2}" '
          f'style="font-variant-numeric:tabular-nums">{last[1]*100:.1f}%</text>')

    # 图例(与标题同一行, 右对齐; 避免压到副标题)
    lx, ly = ML + PW - 262, 28
    for i, (name, _, ckey) in enumerate(series):
        x = lx + i * 130
        a(f'<line x1="{x}" y1="{ly}" x2="{x+18}" y2="{ly}" stroke="{col(ckey, mode)}" stroke-width="2.5"/>')
        a(f'<circle cx="{x+9}" cy="{ly}" r="4" fill="{col(ckey, mode)}"/>')
        a(f'<text x="{x+24}" y="{ly+4}" font-size="12.5" fill="{ink2}">{name}</text>')

    # 不可行标注
    if price_irr[0] is None:
        a(f'<text x="{sx(DELTAS[0]):.1f}" y="{MT+PH-18:.1f}" font-size="11" text-anchor="middle" '
          f'fill="{s1}">FIRR 不存在</text>')

    if interactive:
        a(f'<g id="hoverlayer" style="display:none">'
          f'<line id="cross" x1="0" y1="{MT}" x2="0" y2="{MT+PH}" stroke="{muted}" '
          f'stroke-width="1" stroke-dasharray="3 3"/></g>')
        a(f'<rect id="hit" x="{ML}" y="{MT}" width="{PW}" height="{PH}" fill="transparent" '
          f'style="cursor:crosshair"/>')
    a("</svg>")
    return "\n".join(L)


with open(os.path.join(OUT_DIR, "sensitivity_chart.svg"), "w", encoding="utf-8") as f:
    f.write('<?xml version="1.0" encoding="UTF-8"?>\n' + build_svg("light", False))

# ---------------------------------------------------------------- 7. HTML
js_data = ",\n".join(
    "  {d:%+.4f, p:%s, c:%s}" % (d,
                                "null" if p is None else "%.6f" % p,
                                "null" if c is None else "%.6f" % c)
    for d, p, c in zip(DELTAS, price_irr, cost_irr)
)
geo = (f"{{ml:{ML}, mt:{MT}, pw:{PW}, ph:{PH}, xlo:{X_LO}, xhi:{X_HI}, "
       f"ylo:{Y_LO}, yhi:{Y_HI}, w:{W}}}")

table_rows = "\n".join(
    f'      <tr><td class="k">{d*100:+.0f}%</td>'
    f'<td>{"不可行" if p is None else f"{p*100:.2f}%"}</td>'
    f'<td>{"不可行" if c is None else f"{c*100:.2f}%"}</td>'
    f'<td>{"—" if p is None else f"{(p-BASE_IRR)*100:+.2f}"}</td>'
    f'<td>{"—" if c is None else f"{(c-BASE_IRR)*100:+.2f}"}</td></tr>'
    for d, p, c in zip(DELTAS, price_irr, cost_irr)
)

html = f"""<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>FIRR 敏感性分析图 — 销售价格 / 经营成本</title>
<style>
  :root {{
    --surface-1:#fcfcfb; --page:#f9f9f7;
    --text-primary:#0b0b0b; --text-secondary:#52514e; --text-muted:#898781;
    --gridline:#e1e0d9; --axis:#c3c2b7; --border:rgba(11,11,11,.10);
    --series-1:#2a78d6; --series-2:#eb6834;
    color-scheme: light;
  }}
  @media (prefers-color-scheme: dark) {{
    :root:where(:not([data-theme="light"])) {{
      --surface-1:#1a1a19; --page:#0d0d0d;
      --text-primary:#ffffff; --text-secondary:#c3c2b7; --text-muted:#898781;
      --gridline:#2c2c2a; --axis:#383835; --border:rgba(255,255,255,.10);
      --series-1:#3987e5; --series-2:#d95926;
      color-scheme: dark;
    }}
  }}
  :root[data-theme="dark"] {{
    --surface-1:#1a1a19; --page:#0d0d0d;
    --text-primary:#ffffff; --text-secondary:#c3c2b7; --text-muted:#898781;
    --gridline:#2c2c2a; --axis:#383835; --border:rgba(255,255,255,.10);
    --series-1:#3987e5; --series-2:#d95926;
    color-scheme: dark;
  }}
  * {{ box-sizing:border-box; }}
  body {{ margin:0; padding:30px 20px 56px; background:var(--page); color:var(--text-primary);
         font-family:system-ui,-apple-system,"Segoe UI","Microsoft YaHei",sans-serif; }}
  .wrap {{ max-width:1020px; margin:0 auto; }}
  .card {{ background:var(--surface-1); border:1px solid var(--border); border-radius:12px;
           padding:14px; position:relative; }}
  svg {{ display:block; width:100%; height:auto; }}
  .tip {{ position:absolute; pointer-events:none; opacity:0; transition:opacity .08s;
          background:var(--surface-1); border:1px solid var(--border); border-radius:8px;
          padding:8px 11px; font-size:12.5px; line-height:1.7;
          box-shadow:0 4px 16px rgba(0,0,0,.14); white-space:nowrap; z-index:5; }}
  .dot {{ display:inline-block; width:8px; height:8px; border-radius:50%; margin-right:6px; vertical-align:1px; }}
  .v {{ font-variant-numeric:tabular-nums; }}
  h1 {{ font-size:19px; margin:0 0 6px; }}
  h2 {{ font-size:15px; margin:34px 0 10px; }}
  p.sub {{ color:var(--text-secondary); font-size:13px; margin:0 0 18px; line-height:1.7; }}
  table {{ border-collapse:collapse; width:100%; font-size:13px; font-variant-numeric:tabular-nums; }}
  th,td {{ padding:8px 12px; text-align:right; border-bottom:1px solid var(--border); }}
  th:first-child, td:first-child {{ text-align:left; }}
  th {{ color:var(--text-secondary); font-weight:600; }}
  td.k {{ font-weight:600; }}
  tbody tr:hover {{ background:rgba(127,127,127,.07); }}
  .note {{ color:var(--text-muted); font-size:12.5px; line-height:1.9; margin-top:14px; }}
  .cards {{ display:grid; grid-template-columns:repeat(auto-fit,minmax(158px,1fr)); gap:12px; margin-top:16px; }}
  .stat {{ background:var(--surface-1); border:1px solid var(--border); border-radius:10px; padding:12px 14px; }}
  .stat .lab {{ font-size:12px; color:var(--text-secondary); }}
  .stat .val {{ font-size:21px; font-weight:600; margin-top:3px; font-variant-numeric:tabular-nums; }}
</style>
</head>
<body>
<div class="wrap">
  <h1>FIRR 敏感性分析图</h1>
  <p class="sub">分析因素：销售价格、经营成本 ｜ 变化幅度：±5% ~ ±20% ｜ 基准折现率 i<sub>c</sub> = 10%<br>
  所得税按 15% ×（销售收入 − 经营成本 − 销售税金及附加）随因素变化重算</p>

  <div class="card" id="chartcard">
    {build_svg("vars", True)}
    <div class="tip" id="tip"></div>
  </div>

  <div class="cards">
    <div class="stat"><div class="lab">基准 FIRR</div><div class="val">{BASE_IRR*100:.2f}%</div></div>
    <div class="stat"><div class="lab">价格敏感度系数</div><div class="val">{S_PRICE:+.2f}</div></div>
    <div class="stat"><div class="lab">成本敏感度系数</div><div class="val">{S_COST:+.2f}</div></div>
    <div class="stat"><div class="lab">价格临界点</div><div class="val">{CP_PRICE*100:+.2f}%</div></div>
    <div class="stat"><div class="lab">成本临界点</div><div class="val">{CP_COST*100:+.2f}%</div></div>
  </div>

  <h2>敏感性分析数据表</h2>
  <table>
    <thead><tr><th>变化率</th><th>销售价格 FIRR</th><th>经营成本 FIRR</th>
    <th>价格 Δ(pp)</th><th>成本 Δ(pp)</th></tr></thead>
    <tbody>
{table_rows}
    </tbody>
  </table>

  <p class="note">
    <b>敏感度系数</b> = FIRR 变化率 ÷ 因素变化率（取 ±5% 平均）。&nbsp;|&nbsp;
    <b>临界点</b> = 使 FIRR 恰好等于 i<sub>c</sub> = 10% 时该因素的变化率。<br>
    销售价格 −20% 时全周期净现金流量均为负，FIRR 不存在（图上以空心标记表示），说明项目对价格下跌的承受能力有限。
  </p>
</div>

<script>
const DATA = [
{js_data}
];
const GEO = {geo};
const CARD = document.getElementById('chartcard');
const TIP = document.getElementById('tip');
const HOVER = document.getElementById('hoverlayer');
const CROSS = document.getElementById('cross');
const HIT = document.getElementById('hit');

const sx = d => GEO.ml + (d - GEO.xlo) / (GEO.xhi - GEO.xlo) * GEO.pw;
const sy = r => GEO.mt + (GEO.yhi - r) / (GEO.yhi - GEO.ylo) * GEO.ph;
const pct = v => v === null ? '不可行' : (v * 100).toFixed(2) + '%';

function nearest(clientX) {{
  const scale = CARD.getBoundingClientRect().width / GEO.w;
  const x = (clientX - CARD.getBoundingClientRect().left) / scale;
  let best = 0, bd = Infinity;
  DATA.forEach((row, i) => {{
    const dist = Math.abs(sx(row.d) - x);
    if (dist < bd) {{ bd = dist; best = i; }}
  }});
  return best;
}}

HIT.addEventListener('mousemove', e => {{
  const box = CARD.getBoundingClientRect();
  const scale = box.width / GEO.w;
  const row = DATA[nearest(e.clientX)];

  HOVER.style.display = '';
  CROSS.setAttribute('x1', sx(row.d));
  CROSS.setAttribute('x2', sx(row.d));

  TIP.innerHTML =
    '<b>变化率 ' + (row.d > 0 ? '+' : '') + (row.d * 100).toFixed(0) + '%</b><br>' +
    '<span class="dot" style="background:var(--series-1)"></span>销售价格 ' +
    '<span class="v">' + pct(row.p) + '</span><br>' +
    '<span class="dot" style="background:var(--series-2)"></span>经营成本 ' +
    '<span class="v">' + pct(row.c) + '</span>';
  TIP.style.opacity = 1;

  const px = sx(row.d) * scale;
  TIP.style.left = (row.d > 0.11 ? px - TIP.offsetWidth - 16 : px + 16) + 'px';

  const cand = [row.p, row.c].filter(v => v !== null);
  const py = cand.length ? sy(Math.max.apply(null, cand)) * scale : GEO.mt * scale;
  TIP.style.top = Math.min(Math.max(py - 40, 8), box.height - TIP.offsetHeight - 8) + 'px';
}});

HIT.addEventListener('mouseleave', () => {{
  HOVER.style.display = 'none';
  TIP.style.opacity = 0;
}});
</script>
</body>
</html>
"""

with open(os.path.join(OUT_DIR, "敏感性分析图.html"), "w", encoding="utf-8") as f:
    f.write(html)

print()
print("已输出:")
for fn in ("firr_sensitivity.csv", "sensitivity_chart.svg", "敏感性分析图.html"):
    print("  " + os.path.join(OUT_DIR, fn))
