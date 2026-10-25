import os, html

OUT = r'E:\CODE\数据结构实验报告\report\fig'
os.makedirs(OUT, exist_ok=True)
FONT = "SimSun, SimHei, sans-serif"
GREY = "#EDEDED"
GAP = 34
CX, SPINE_W, LOOP_X, SKIP_X, RCX = 280, 240, 88, 490, 650


def bh(lines):
    return 26 + 19 * len(lines)


class Flow:
    def __init__(self, w, loopx=LOOP_X):
        self.w = w
        self.loopx = loopx
        self.y = 46
        self.maxb = 0
        self.p = ['<defs><marker id="ar" markerWidth="11" markerHeight="9" refX="10" refY="4.5" '
                  'orient="auto"><path d="M0,0 L11,4.5 L0,9 z" fill="#000"/></marker></defs>']

    def text(self, x, y, s, size=15, anchor='middle'):
        self.p.append(f'<text x="{x}" y="{y}" font-family="{FONT}" font-size="{size}" '
                      f'text-anchor="{anchor}" fill="#000">{html.escape(s)}</text>')

    def box(self, lines, shape='proc', cx=None, w=None, cy=None, adv=True, fill='#fff'):
        cx = CX if cx is None else cx
        w = SPINE_W if w is None else w
        cy = self.y if cy is None else cy
        h = bh(lines)
        x, y = cx - w / 2, cy - h / 2
        self.maxb = max(self.maxb, cy + h / 2)
        if shape == 'dec':
            self.p.append(f'<polygon points="{cx},{y} {cx+w/2},{cy} {cx},{y+h} {cx-w/2},{cy}" '
                          f'fill="{fill}" stroke="#000" stroke-width="1.4"/>')
        elif shape in ('start', 'end'):
            self.p.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{h/2}" ry="{h/2}" '
                          f'fill="{fill}" stroke="#000" stroke-width="1.4"/>')
        elif shape == 'io':
            d = 16
            self.p.append(f'<polygon points="{x+d},{y} {x+w},{y} {x+w-d},{y+h} {x},{y+h}" '
                          f'fill="{fill}" stroke="#000" stroke-width="1.4"/>')
        else:
            self.p.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill}" '
                          f'stroke="#000" stroke-width="1.4"/>')
        for i, ln in enumerate(lines):
            self.text(cx, cy + (i - (len(lines) - 1) / 2) * 19 + 5.5, ln)
        if adv:
            self.y = cy + h / 2 + GAP
        return (cx, cy, w, h)

    def arrow(self, pts, label=None, lpos=None):
        d = 'M' + ' L'.join(f'{a},{b}' for a, b in pts)
        self.p.append(f'<path d="{d}" fill="none" stroke="#000" stroke-width="1.4" marker-end="url(#ar)"/>')
        if label:
            lx, ly = lpos if lpos else pts[len(pts) // 2]
            self.text(lx, ly, label, 13, 'start')

    def down(self, a, b, label=None):
        self.arrow([(a[0], a[1] + a[3] / 2), (b[0], b[1] - b[3] / 2)],
                   label, (a[0] + 10, (a[1] + a[3] / 2 + b[1] - b[3] / 2) / 2 + 5))

    def right(self, dec, box, label):
        self.arrow([(dec[0] + dec[2] / 2, dec[1]), (box[0] - box[2] / 2, box[1])],
                   label, (dec[0] + dec[2] / 2 + 10, dec[1] - 9))

    def loop(self, dec, body):
        self.arrow([(dec[0], dec[1] + dec[3] / 2), (body[0], body[1] - body[3] / 2)],
                   '是', (dec[0] + 10, (dec[1] + dec[3] / 2 + body[1] - body[3] / 2) / 2 + 5))
        self.arrow([(body[0] - body[2] / 2, body[1]), (self.loopx, body[1]),
                    (self.loopx, dec[1]), (dec[0] - dec[2] / 2, dec[1])])

    def skip_over(self, dec, body, nxt, label='否'):
        ym = (body[1] + body[3] / 2 + nxt[1] - nxt[3] / 2) / 2
        self.arrow([(dec[0] + dec[2] / 2, dec[1]), (SKIP_X, dec[1]), (SKIP_X, ym),
                    (dec[0], ym), (dec[0], nxt[1] - nxt[3] / 2)],
                   label, (dec[0] + dec[2] / 2 + 10, dec[1] - 9))

    def group(self, dec, lines, label, w=250, tail=('结束',)):
        b = self.box(lines, 'io', cx=RCX, cy=dec[1], w=w, adv=False)
        self.right(dec, b, label)
        z = self.box(list(tail), 'start', cx=RCX, cy=b[1] + b[3] / 2 + GAP + 22,
                     w=130, adv=False, fill=GREY)
        self.arrow([(RCX, b[1] + b[3] / 2), (RCX, z[1] - 22)])

    def save(self, name):
        h = int(self.maxb + 26)
        svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}" height="{h}" '
               f'viewBox="0 0 {self.w} {h}"><rect width="{self.w}" height="{h}" fill="#fff"/>'
               + ''.join(self.p) + '</svg>')
        open(os.path.join(OUT, name), 'w', encoding='utf-8').write(svg)


# ---------------- 图2.1 试题1 计算器 ----------------
def fig21():
    f = Flow(860, loopx=70)
    a = f.box(['开始'], 'start', w=130, fill=GREY)
    b = f.box(['输入表达式 expr'], 'io', w=260)
    c = f.box(['初始化操作数栈 num', '和运算符栈 op'], 'proc', w=300)
    d = f.box(['取当前字符 c'], 'proc', w=200)
    e = f.box(["c == '\\0' ?"], 'dec', w=240)
    g = f.box(['数字 → 压入 num；\'(\' → 压入 op',
               "')' → 弹出运算符并计算，直到 '('",
               '其他 → 按优先级弹出并计算后入栈'], 'proc', w=340)
    h = f.box(['弹出 op 中剩余的运算符并计算'], 'proc', w=320)
    j = f.box(['出现括号不匹配或除数为 0 ?'], 'dec', w=300)
    f.group(j, ['输出表达式错误'], '是')
    k = f.box(['输出计算结果'], 'io', w=240)
    z = f.box(['结束'], 'start', w=130, fill=GREY)
    for p, q in ((a, b), (b, c), (c, d), (d, e)):
        f.down(p, q)
    f.down(e, g, '否')
    f.arrow([(g[0] - g[2] / 2, g[1]), (70, g[1]), (70, d[1]), (d[0] - d[2] / 2, d[1])])
    f.skip_over(e, g, h, '是')
    f.down(h, j)
    f.down(j, k, '否')
    f.down(k, z)
    f.save('fig2_1.svg')


# ---------------- 图2.2 试题2 二项式系数 ----------------
def fig22():
    f = Flow(860)
    a = f.box(['开始'], 'start', w=130, fill=GREY)
    b = f.box(['输入 k'], 'io', w=200)
    c = f.box(['k >= 2 ?'], 'dec', w=220)
    f.group(c, ['输出“k必须大于等于2”'], '否')
    d = f.box(['队列清空，依次入队 1、1'], 'proc', w=300)
    e = f.box(['level = 2'], 'proc', w=180)
    g = f.box(['level <= k ?'], 'dec', w=240)
    h = f.box(['重复 level-1 次：', '出队 a，取队头 b，入队 a+b'], 'proc', w=340)
    i = f.box(['入队 1，level++'], 'proc', w=240)
    k = f.box(['依次出队并输出各项系数'], 'io', w=300)
    z = f.box(['结束'], 'start', w=130, fill=GREY)
    for p, q in ((a, b), (b, c), (c, d), (d, e), (e, g)):
        f.down(p, q)
    f.down(c, d, '是')
    f.down(g, h, '是')
    f.down(h, i)
    f.arrow([(i[0] - i[2] / 2, i[1]), (LOOP_X, i[1]), (LOOP_X, g[1]), (g[0] - g[2] / 2, g[1])])
    f.skip_over(g, i, k)
    f.down(k, z)
    f.save('fig2_2.svg')


# ---------------- 图2.3 试题3 串的最后一次出现 ----------------
def fig23():
    f = Flow(880, loopx=60)
    a = f.box(['开始'], 'start', w=130, fill=GREY)
    b = f.box(['输入主串 str 和子串 substr'], 'io', w=320)
    c = f.box(['求长度 n、m，令 pos = 0'], 'proc', w=300)
    d = f.box(['m == 0 或 m > n ?'], 'dec', w=260)
    f.group(d, ['输出“不存在”'], '是')
    e = f.box(['i = 0'], 'proc', w=140)
    g = f.box(['i <= n - m ?'], 'dec', w=240)
    h = f.box(['从 i 开始逐字符比较，', '若 m 个字符全部相等，则 pos = i+1'], 'proc', w=340)
    i = f.box(['i++'], 'proc', w=140)
    k = f.box(['输出 pos（pos 为 0 表示不存在）'], 'io', w=380)
    z = f.box(['结束'], 'start', w=130, fill=GREY)
    for p, q in ((a, b), (b, c), (c, d), (d, e), (e, g)):
        f.down(p, q)
    f.down(d, e, '否')
    f.down(g, h, '是')
    f.down(h, i)
    f.arrow([(i[0] - i[2] / 2, i[1]), (60, i[1]), (60, g[1]), (g[0] - g[2] / 2, g[1])])
    f.skip_over(g, i, k)
    f.down(k, z)
    f.save('fig2_3.svg')


fig21(); fig22(); fig23()
print('svg done')
