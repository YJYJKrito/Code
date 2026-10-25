# -*- coding: utf-8 -*-
"""报告正文中自绘的图（图4.1~图4.4），以 inline SVG 形式返回。"""


def fig_block_diagram():
    """图4.1 2位二进制乘法器电路系统功能框图"""
    # 4 位输入 / 4 位积的引脚 y 坐标
    IN = [95, 125, 155, 185]      # K3 K2 K1 K0
    OUTY = [100, 128, 156, 184]   # Z3 Z2 Z1 Z0
    n_in = ["K3", "K2", "K1", "K0"]
    n_out = ["Z3", "Z2", "Z1", "Z0"]

    inp = "\n".join(
        f'    <line x1="46" y1="{y}" x2="72" y2="{y}" stroke="#000" stroke-width="1.2"/>'
        f'\n    <circle cx="46" cy="{y}" r="2.4" fill="#000"/>'
        f'\n    <text x="8" y="{y - 6}" font-size="12">{n}</text>'
        for n, y in zip(n_in, IN)
    )
    mid = "\n".join(
        f'    <line x1="248" y1="{y}" x2="312" y2="{y}" stroke="#000" stroke-width="1.2"/>'
        f'\n    <circle cx="300" cy="{y}" r="2.4" fill="#000"/>'
        f'\n    <text x="256" y="{y - 6}" font-size="12">{n}</text>'
        for n, y in zip(n_out, OUTY)
    )

    return f"""
<svg viewBox="0 0 660 268" xmlns="http://www.w3.org/2000/svg" class="fig">
  <defs>
    <marker id="arBD" markerWidth="9" markerHeight="9" refX="8" refY="3" orient="auto">
      <path d="M0,0 L8,3 L0,6 z" fill="#000"/>
    </marker>
  </defs>
  <g font-family="SimSun" font-size="12.5" fill="#000">

    <!-- 输入总线 -->
    <line x1="46" y1="{IN[0]}" x2="46" y2="{IN[-1]}" stroke="#000" stroke-width="1.2"/>
{inp}

    <!-- 乘法器主电路 -->
    <rect x="72" y="80" width="176" height="122" fill="#f2f2f2" stroke="#000" stroke-width="1.3"/>
    <text x="98" y="126">乘法器主电路</text>
    <text x="88" y="152" font-size="11">（4-16 译码器 +</text>
    <text x="104" y="170" font-size="11">与非门实现）</text>

    <!-- 中间的积 -->
    <line x1="300" y1="{OUTY[0]}" x2="300" y2="{OUTY[-1]}" stroke="#000" stroke-width="1.2"/>
{mid}

    <!-- 显示转换电路 -->
    <rect x="312" y="80" width="196" height="122" fill="#f2f2f2" stroke="#000" stroke-width="1.3"/>
    <text x="330" y="126">2-10 进制显示转换电路</text>
    <text x="330" y="152" font-size="11">（74LS47 + 共阳极</text>
    <text x="346" y="170" font-size="11">七段数码管实现）</text>

    <!-- 输出 -->
    <line x1="508" y1="141" x2="575" y2="141" stroke="#000" stroke-width="1.4" marker-end="url(#arBD)"/>
    <text x="583" y="146">十进制积</text>

    <!-- 标注 -->
    <text x="8" y="228" font-size="11">两个 2 位二进制数形成的输入信号，</text>
    <text x="8" y="246" font-size="11">K3 为最高位，K0 为最低位</text>
    <text x="272" y="52" font-size="11">二进制积输出</text>
    <text x="256" y="70" font-size="11">Z3 为最高位，Z0 为最低位</text>
    <line x1="300" y1="56" x2="300" y2="76" stroke="#666" stroke-width="0.8" stroke-dasharray="3,3"/>
  </g>
</svg>
"""


def fig_decoder():
    """图4.2 用两片74HC138扩展为4-16译码器的连接示意图

    使能端极性按 74HC138 数据手册与附录4.2的图形符号：
      E1 = 6脚 = G1，高电平有效
      E2 = 4脚 = G2A，低电平有效
      E3 = 5脚 = G2B，低电平有效
    使能条件为 E1=1 且 E2=0 且 E3=0。
    K3=0 时 U1 工作（输出 m0~m7）；K3=1 时 U2 工作（输出 m8~m15）。
    """
    # 引脚纵坐标
    A1 = dict(A=52, B=68, C=84, E1=118, E2=134, E3=150)
    A2 = dict(A=222, B=238, C=254, E1=288, E2=304, E3=320)
    OUT1 = [42, 58, 74, 90, 106, 122, 138, 154]
    OUT2 = [212, 228, 244, 260, 276, 292, 308, 324]

    L, R = 150, 234          # 芯片左右边
    K3X = 126                # K3 纵向干线
    STUB = 70                # 使能端引线左端

    def outs(ys, base):
        s = []
        for i, y in enumerate(ys):
            s.append(f'    <line x1="{L}" y1="{y}" x2="{R}" y2="{y}" stroke="#000" stroke-width="1"/>')
            s.append(f'    <text x="{R + 6}" y="{y + 4}" font-size="11">m{base + i}（Y{i}）</text>')
        return "\n".join(s)

    def name(x, y, txt, size=11.5):
        return f'    <text x="{x}" y="{y}" font-size="{size}">{txt}</text>'

    def dot(x, y):
        return f'    <circle cx="{x}" cy="{y}" r="2.5" fill="#000"/>'

    def enable(y, label, x_to):
        return (f'    <line x1="{x_to}" y1="{y}" x2="{L}" y2="{y}" stroke="#000" stroke-width="1.1"/>'
                + "\n" + name(STUB + 6, y - 5, label, 11))

    taps = "\n".join(
        f'    <line x1="{vx}" y1="{y}" x2="{L}" y2="{y}" stroke="#000" stroke-width="1.1"/>'
        + "\n" + dot(vx, y)
        for vx, y in ((30, A1["A"]), (42, A1["B"]), (54, A1["C"]),
                      (30, A2["A"]), (42, A2["B"]), (54, A2["C"]))
    )
    # 地址干线
    bus = "\n".join([
        f'    <line x1="20" y1="18" x2="60" y2="18" stroke="#000" stroke-width="1.1"/>',
        f'    <line x1="20" y1="30" x2="60" y2="30" stroke="#000" stroke-width="1.1"/>',
        f'    <line x1="20" y1="42" x2="60" y2="42" stroke="#000" stroke-width="1.1"/>',
        f'    <line x1="30" y1="18" x2="30" y2="{A2["A"]}" stroke="#000" stroke-width="1.1"/>',
        f'    <line x1="42" y1="30" x2="42" y2="{A2["B"]}" stroke="#000" stroke-width="1.1"/>',
        f'    <line x1="54" y1="42" x2="54" y2="{A2["C"]}" stroke="#000" stroke-width="1.1"/>',
        dot(30, 18), dot(42, 30), dot(54, 42),
        name(4, 16, "K0"), name(4, 28, "K1"), name(4, 40, "K2"),
    ])

    return f"""
<svg viewBox="0 0 410 396" xmlns="http://www.w3.org/2000/svg" class="fig">
  <g font-family="SimSun" font-size="11.5" fill="#000">

    <!-- ===== 地址线 ===== -->
{bus}
{taps}

    <!-- ===== U1 ===== -->
    <rect x="{L}" y="30" width="{R - L}" height="132" fill="#f2f2f2" stroke="#000" stroke-width="1.3"/>
    {name(152, 24, "U1  74HC138", 12)}
{outs(OUT1, 0)}
    {name(152, A1["A"] + 4, "A")}{name(152, A1["B"] + 4, "B")}{name(152, A1["C"] + 4, "C")}
    {name(152, A1["E1"] + 4, "E1")}{name(152, A1["E2"] + 4, "E2")}{name(152, A1["E3"] + 4, "E3")}
    {name(240, 178, "输出 m0~m7（K3 = 0 时有效）", 11)}

    <!-- ===== U2 ===== -->
    <rect x="{L}" y="200" width="{R - L}" height="132" fill="#f2f2f2" stroke="#000" stroke-width="1.3"/>
    {name(152, 194, "U2  74HC138", 12)}
{outs(OUT2, 8)}
    {name(152, A2["A"] + 4, "A")}{name(152, A2["B"] + 4, "B")}{name(152, A2["C"] + 4, "C")}
    {name(152, A2["E1"] + 4, "E1")}{name(152, A2["E2"] + 4, "E2")}{name(152, A2["E3"] + 4, "E3")}
    {name(152, 358, "输出 m8~m15（K3 = 1 时有效）", 11)}

    <!-- ===== 使能端 ===== -->
{enable(A1["E1"], "E1 = +5V", STUB)}
{enable(A1["E2"], "E2 = GND", STUB)}
{enable(A1["E3"], "E3 = K3", STUB)}
{enable(A2["E1"], "E1 = K3", STUB)}
{enable(A2["E2"], "E2 = GND", STUB)}
{enable(A2["E3"], "E3 = GND", STUB)}

    <!-- K3 干线 -->
    <line x1="{K3X}" y1="{A1["E3"]}" x2="{K3X}" y2="{A2["E1"]}" stroke="#000" stroke-width="1.5"/>
    {dot(K3X, A1["E3"])}{dot(K3X, A2["E1"])}{dot(K3X, 174)}
    <line x1="{K3X}" y1="174" x2="{K3X + 8}" y2="174" stroke="#000" stroke-width="1.1"/>
    {name(K3X + 11, 178, "K3", 11.5)}

    <!-- 说明 -->
    {name(6, 378, "E1（6 脚）高电平有效；E2（4 脚）、E3（5 脚）低电平有效。", 10.5)}
    {name(6, 390, "使能条件：E1 = 1 且 E2 = 0 且 E3 = 0。", 10.5)}
  </g>
</svg>
"""


def fig_current_loop():
    """图4.3 共阳极数码管一位段的驱动回路"""
    return """
<svg viewBox="0 0 540 268" xmlns="http://www.w3.org/2000/svg" class="fig">
  <defs>
    <marker id="arI" markerWidth="9" markerHeight="9" refX="8" refY="3" orient="auto">
      <path d="M0,0 L8,3 L0,6 z" fill="#000"/>
    </marker>
  </defs>
  <g font-family="SimSun" font-size="12" fill="#000">
    <!-- +5V 节点 -->
    <line x1="50" y1="24" x2="96" y2="24" stroke="#000" stroke-width="1.2"/>
    <text x="54" y="18">+5V</text>
    <line x1="74" y1="24" x2="74" y2="62" stroke="#000" stroke-width="1.2"/>

    <!-- 数码管一只LED -->
    <polygon points="56,62 92,62 74,96" fill="none" stroke="#000" stroke-width="1.2"/>
    <line x1="56" y1="96" x2="92" y2="96" stroke="#000" stroke-width="1.6"/>
    <text x="104" y="74">LED 段</text>
    <text x="104" y="90">V_F ≈ 2V</text>
    <line x1="74" y1="96" x2="74" y2="122" stroke="#000" stroke-width="1.2"/>

    <!-- 限流电阻 -->
    <rect x="56" y="122" width="36" height="18" fill="#fff" stroke="#000" stroke-width="1.2"/>
    <text x="104" y="128">限流电阻 R</text>
    <text x="104" y="144" font-size="11">（排电阻 RES16DIPIS）</text>
    <line x1="74" y1="140" x2="74" y2="166" stroke="#000" stroke-width="1.2"/>

    <!-- 74LS47 输出级 -->
    <rect x="22" y="166" width="146" height="36" fill="#f2f2f2" stroke="#000" stroke-width="1.2"/>
    <text x="32" y="189">74LS47 输出级</text>
    <text x="22" y="220" font-size="11">（集电极开路，输出拉低时该段导通）</text>

    <!-- 地 -->
    <line x1="74" y1="202" x2="74" y2="234" stroke="#000" stroke-width="1.2"/>
    <line x1="62" y1="234" x2="86" y2="234" stroke="#000" stroke-width="1.4"/>
    <line x1="67" y1="239" x2="81" y2="239" stroke="#000" stroke-width="1.2"/>
    <line x1="71" y1="244" x2="77" y2="244" stroke="#000" stroke-width="1.0"/>

    <!-- 电流方向 -->
    <line x1="42" y1="36" x2="42" y2="150" stroke="#000" stroke-width="1.2" marker-end="url(#arI)"/>
    <text x="24" y="98">I</text>

    <!-- 计算 -->
    <text x="252" y="76" font-size="12.5">I = (5 − 2 − 0.4) / R = 2.6 / R</text>
    <text x="252" y="104" font-size="11.5">要求 10mA ≤ I ≤ 30mA</text>
    <text x="252" y="128" font-size="11.5">→ 86.7Ω ≤ R ≤ 260Ω</text>
    <text x="252" y="152" font-size="11.5">取 R = 200Ω 时 I ≈ 13mA</text>
  </g>
</svg>
"""


def fig_iv_curve():
    """图4.4 段电流随限流电阻的变化及可用区"""
    # 坐标：x: R 0~320Ω -> 60~520px ; y: I 0~30mA -> 230~40px
    def X(r):
        return 60 + r * (520 - 60) / 320.0

    def Y(i):
        return 230 - i * (230 - 40) / 30.0

    pts = " ".join(
        f"{X(r):.1f},{Y(2.6 / r):.1f}" for r in range(20, 321, 5)
    )
    return f"""
<svg viewBox="0 0 600 290" xmlns="http://www.w3.org/2000/svg" class="fig">
  <g font-family="SimSun" font-size="12" fill="#000">
    <!-- 可用区 10~30mA -->
    <rect x="{X(86.7):.1f}" y="{Y(30):.1f}" width="{X(260) - X(86.7):.1f}" height="{Y(10) - Y(30):.1f}"
          fill="#d9ead3" stroke="none"/>
    <text x="{X(150):.1f}" y="{Y(27):.1f}" font-size="11">可用区 10~30mA</text>

    <!-- 坐标轴 -->
    <line x1="60" y1="230" x2="540" y2="230" stroke="#000" stroke-width="1.2"/>
    <line x1="60" y1="230" x2="60" y2="26" stroke="#000" stroke-width="1.2"/>
    <text x="516" y="252" font-size="12">R / Ω</text>
    <text x="8" y="16" font-size="12">I / mA</text>

    <!-- 刻度 -->
    <line x1="{X(100):.1f}" y1="230" x2="{X(100):.1f}" y2="235" stroke="#000" stroke-width="1"/>
    <text x="{X(100) - 12:.1f}" y="248">100</text>
    <line x1="{X(200):.1f}" y1="230" x2="{X(200):.1f}" y2="235" stroke="#000" stroke-width="1"/>
    <text x="{X(200) - 12:.1f}" y="248">200</text>
    <line x1="{X(300):.1f}" y1="230" x2="{X(300):.1f}" y2="235" stroke="#000" stroke-width="1"/>
    <text x="{X(300) - 12:.1f}" y="248">300</text>
    <line x1="55" y1="{Y(10):.1f}" x2="60" y2="{Y(10):.1f}" stroke="#000" stroke-width="1"/>
    <text x="24" y="{Y(10) + 4:.1f}">10</text>
    <line x1="55" y1="{Y(20):.1f}" x2="60" y2="{Y(20):.1f}" stroke="#000" stroke-width="1"/>
    <text x="24" y="{Y(20) + 4:.1f}">20</text>
    <line x1="55" y1="{Y(30):.1f}" x2="60" y2="{Y(30):.1f}" stroke="#000" stroke-width="1"/>
    <text x="24" y="{Y(30) + 4:.1f}">30</text>

    <!-- 曲线 -->
    <polyline points="{pts}" fill="none" stroke="#1f4e79" stroke-width="1.8"/>

    <!-- 200Ω 工作点 -->
    <line x1="{X(200):.1f}" y1="{Y(13):.1f}" x2="{X(200):.1f}" y2="230" stroke="#c00" stroke-width="1" stroke-dasharray="4,3"/>
    <line x1="60" y1="{Y(13):.1f}" x2="{X(200):.1f}" y2="{Y(13):.1f}" stroke="#c00" stroke-width="1" stroke-dasharray="4,3"/>
    <circle cx="{X(200):.1f}" cy="{Y(13):.1f}" r="4" fill="#c00"/>
    <text x="{X(200) + 10:.1f}" y="{Y(13) - 10:.1f}" fill="#c00" font-size="11.5">工作点 R=200Ω，I≈13mA</text>

    <!-- 结论 -->
    <text x="150" y="276" font-size="11">R 越小段电流越大；R=86.7Ω 时已达 30mA 上限，再小将过流。</text>
  </g>
</svg>
"""
