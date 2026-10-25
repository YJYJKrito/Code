# -*- coding: utf-8 -*-
import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
sys.path.insert(0, r'E:\CODE\_svgtest')
from elements import emblem, tiananmen, greatwall, ribbon
from svglib.svglib import svg2rlg
from reportlab.graphics import renderPM

W, H = 1100, 720
parts = [f'<rect width="{W}" height="{H}" fill="#8E0B12"/>']

# 党徽
parts.append(f'<g transform="translate(30,30)">{emblem(230)}</g>')
# 天安门
parts.append(f'<g transform="translate(300,60)">{tiananmen(420,200)}</g>')
# 飘带
parts.append(f'<g transform="translate(280,300)">{ribbon(560,180)}</g>')
# 长城带
parts.append(f'<g transform="translate(0,{H-150})">{greatwall(W,150,op=0.30)}</g>')

svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" '
       f'viewBox="0 0 {W} {H}">' + "".join(parts) + '</svg>')

open(r'E:\CODE\_svgtest\t1.svg', 'w', encoding='utf-8').write(svg)
renderPM.drawToFile(svg2rlg(r'E:\CODE\_svgtest\t1.svg'), r'E:\CODE\_svgtest\t1.png', fmt='PNG')
print('rendered')
