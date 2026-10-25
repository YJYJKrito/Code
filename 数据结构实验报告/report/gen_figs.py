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
    def __init__(self, w):
        self.w = w
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
        self.arrow([(body[0] - body[2] / 2, body[1]), (LOOP_X, body[1]),
                    (LOOP_X, dec[1]), (dec[0] - dec[2] / 2, dec[1])])

    def skip_over(self, dec, body, nxt):
        ym = (body[1] + body[3] / 2 + nxt[1] - nxt[3] / 2) / 2
        self.arrow([(dec[0] + dec[2] / 2, dec[1]), (SKIP_X, dec[1]), (SKIP_X, ym),
                    (dec[0], ym), (dec[0], nxt[1] - nxt[3] / 2)],
                   '否', (dec[0] + dec[2] / 2 + 10, dec[1] - 9))

    def group(self, dec, lines, label, w=250):
        b = self.box(lines, 'io', cx=RCX, cy=dec[1], w=w, adv=False)
        self.right(dec, b, label)
        z = self.box(['结束'], 'start', cx=RCX, cy=b[1] + b[3] / 2 + GAP + 22,
                     w=130, adv=False, fill=GREY)
        self.arrow([(RCX, b[1] + b[3] / 2), (RCX, z[1] - 22)])

    def save(self, name):
        h = int(self.maxb + 26)
        svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}" height="{h}" '
               f'viewBox="0 0 {self.w} {h}"><rect width="{self.w}" height="{h}" fill="#fff"/>'
               + ''.join(self.p) + '</svg>')
        open(os.path.join(OUT, name), 'w', encoding='utf-8').write(svg)


# ---------------- 图1.1 试题1 ----------------
def fig1():
    f = Flow(860)
    a = f.box(['开始'], 'start', w=130, fill=GREY)
    b = f.box(['输入n及n个数据', '建立带头结点的单链表'], 'io')
    c = f.box(['输入k'], 'io', w=200)
    d = f.box(['k > 0 ?'], 'dec')
    f.group(d, ['输出“该结点不存在”'], '否')
    e = f.box(['p = q = head->next，i = 0'], 'proc', w=300)
    g = f.box(['i < k 且 p != NULL ?'], 'dec')
    gb = f.box(['p = p->next，i++'], 'proc')
    h = f.box(['i < k ?'], 'dec')
    f.group(h, ['输出“该结点不存在”'], '是')
    j = f.box(['p != NULL ?'], 'dec')
    jb = f.box(['p = p->next，q = q->next'], 'proc', w=280)
    k = f.box(['输出 q->data'], 'io', w=200)
    z = f.box(['结束'], 'start', w=130, fill=GREY)
    for p, q in ((a, b), (b, c), (c, d), (e, g), (h, j), (k, z)):
        f.down(p, q)
    f.down(d, e, '是')
    f.loop(g, gb)
    f.skip_over(g, gb, h)
    f.down(h, j, '否')
    f.loop(j, jb)
    f.skip_over(j, jb, k)
    f.save('fig1_1.svg')


# ---------------- 图1.2 试题2主流程 ----------------
def fig2():
    f = Flow(830)
    a = f.box(['开始'], 'start', w=130, fill=GREY)
    b = f.box(['输入n及n个数据，建立单链表'], 'io')
    c = f.box(['head == NULL ?'], 'dec')
    f.group(c, ['输出“链表为空”'], '是')
    d = f.box(['输出最大值 Max(head)'], 'io', w=280)
    e = f.box(['输出结点个数 Count(head)'], 'io', w=280)
    g = f.box(['输出平均值 Average(head, n)'], 'io', w=280)
    z = f.box(['结束'], 'start', w=130, fill=GREY)
    for p, q in ((a, b), (b, c), (d, e), (e, g), (g, z)):
        f.down(p, q)
    f.down(c, d, '否')
    f.save('fig1_2.svg')


# ---------------- 图1.3 递归求平均值 ----------------
def fig3():
    f = Flow(830)
    a = f.box(['Average(head, n)'], 'start', w=240, fill=GREY)
    c = f.box(['head == NULL ?'], 'dec')
    f.group(c, ['n = 0', '返回 0'], '是')
    d = f.box(['avg = Average(head->next, n)'], 'proc', w=340)
    e = f.box(['n++'], 'proc', w=160)
    g = f.box(['返回 (avg×(n-1) + head->data) / n'], 'proc', w=390)
    z = f.box(['结束'], 'start', w=130, fill=GREY)
    f.down(a, c)
    f.down(c, d, '否')
    for p, q in ((d, e), (e, g), (g, z)):
        f.down(p, q)
    f.save('fig1_3.svg')


# ---------------- 图1.4 试题3 ----------------
def fig4():
    f = Flow(1010)
    a = f.box(['开始'], 'start', w=130, fill=GREY)
    b = f.box(['输入m及m个数据', '建立递增有序链表A'], 'io')
    c = f.box(['输入n及n个数据', '建立递增有序链表B'], 'io')
    d = f.box(['pa = A->next，pb = B->next'], 'proc', w=300)
    e = f.box(['C = A，C->next = NULL'], 'proc', w=300)
    g = f.box(['pa != NULL 且 pb != NULL ?'], 'dec')      # 判断1
    h = f.box(['pa->data <= pb->data ?'], 'dec')           # 判断2
    i = f.box(['p = pa，pa = pa->next'], 'proc', w=240)
    j = f.box(['p = pb，pb = pb->next'], 'proc', w=220, cx=555, cy=i[1], adv=False)
    k = f.box(['p->next = C->next，C->next = p'], 'proc', w=300)
    z = f.box(['结束'], 'start', w=130, fill=GREY)
    rc = 850
    for p, q in ((a, b), (b, c), (c, d), (d, e), (e, g)):
        f.down(p, q)
    f.down(g, h, '是')
    f.down(h, i, '是')
    f.loop(g, k)
    f.arrow([(h[0] + h[2] / 2, h[1]), (470, h[1]), (470, j[1]), (j[0] - j[2] / 2, j[1])],
            '否', (h[0] + h[2] / 2 + 10, h[1] - 9))
    f.down(i, k)
    f.arrow([(j[0], j[1] + j[3] / 2), (j[0], k[1]), (k[0] + k[2] / 2, k[1])])

    # 判断1 否 → 右列：处理剩余链
    rest = f.box(['rest = 非空那条链的表头'], 'proc', cx=rc, cy=g[1], w=250, adv=False)
    f.right(g, rest, '否')
    rd = f.box(['rest != NULL ?'], 'dec', cx=rc, cy=rest[1] + 22 + GAP + 34, w=250, adv=False)
    f.arrow([(rc, rest[1] + 22), (rc, rd[1] - 34)])
    rb = f.box(['p = rest，rest = rest->next'], 'proc', cx=rc, cy=rd[1] + 34 + GAP + 22, w=250, adv=False)
    rh = f.box(['p->next = C->next', 'C->next = p'], 'proc', cx=rc, cy=rb[1] + 22 + GAP + 34, w=250, adv=False)
    f.arrow([(rc, rd[1] + 34), (rc, rb[1] - 22)], '是', (rc + 10, rd[1] + 34 + 16))
    f.down(rb, rh)
    f.arrow([(rc + 125, rh[1]), (990, rh[1]), (990, rd[1]), (rc + 125, rd[1])])
    ro = f.box(['输出链表C'], 'io', cx=rc, cy=rh[1] + 34 + GAP + 22, w=200, adv=False)
    f.arrow([(rc - 125, rd[1]), (690, rd[1]), (690, ro[1]), (ro[0] - ro[2] / 2, ro[1])],
            '否', (700, rd[1] - 9))
    rz = f.box(['结束'], 'start', cx=rc, cy=ro[1] + 22 + GAP + 22, w=130, adv=False, fill=GREY)
    f.arrow([(rc, ro[1] + 22), (rc, rz[1] - 22)])
    f.save('fig1_4.svg')


fig1(); fig2(); fig3(); fig4()
print('svg done')
