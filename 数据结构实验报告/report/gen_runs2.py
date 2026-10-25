import os, html, subprocess

FIG = r'E:\CODE\数据结构实验报告\report\fig'
EDGE = r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
PR = r'E:\CODE\数据结构实验报告\code>'

RUNS = {
    'e2_1a': ('exp2_1.exe', ['3×(7-2×2)+8÷4'], ['计算结果为:11']),
    'e2_1b': ('exp2_1.exe', ['(1+2)*(3+4)'], ['计算结果为:21']),
    'e2_1c': ('exp2_1.exe', ['3/0'], ['除数不能为0']),
    'e2_2a': ('exp2_2.exe', ['4'], ['(a+b)^4 展开后各项系数为:1 4 6 4 1 ']),
    'e2_2b': ('exp2_2.exe', ['2'], ['(a+b)^2 展开后各项系数为:1 2 1 ']),
    'e2_2c': ('exp2_2.exe', ['1'], ['k必须大于等于2']),
    'e2_3a': ('exp2_3.exe', ['ababcabcab ab'], ['substr 最后一次出现的位置为:9']),
    'e2_3b': ('exp2_3.exe', ['aaaa aa'], ['substr 最后一次出现的位置为:3']),
    'e2_3c': ('exp2_3.exe', ['hello xyz'], ['substr 在 str 中不存在']),
}

TPL = """<!doctype html><meta charset="utf-8">
<style>
 html,body{margin:0;padding:0;background:#fff}
 .win{width:880px;border:1px solid #7a7a7a;border-radius:2px;overflow:hidden;
      font-family:Consolas,"Courier New",monospace;font-size:17px;line-height:26px}
 .bar{background:#e8e8e8;color:#1a1a1a;font-family:"SimSun",sans-serif;font-size:15px;
      padding:7px 12px;border-bottom:1px solid #b8b8b8}
 .bar span{float:right;letter-spacing:3px;color:#4a4a4a}
 .scr{background:#0c0c0c;color:#cccccc;padding:12px 16px;white-space:pre-wrap;word-break:break-all}
</style>
<div class="win">
 <div class="bar">命令提示符 - __EXE__<span>— &nbsp;□ &nbsp;×</span></div>
 <div class="scr">__BODY__</div>
</div>
"""

for name, (exe, ins, outs) in RUNS.items():
    body = html.escape(PR + exe) + '\n' + '\n'.join(html.escape(i) for i in ins) + '\n'
    body += '\n'.join(html.escape(o) for o in outs) + '\n\n'
    open(os.path.join(FIG, name + '.html'), 'w', encoding='utf-8').write(
        TPL.replace('__EXE__', exe).replace('__BODY__', body))
    h = 38 + 24 + (body.count('\n') + 1) * 26
    subprocess.run([EDGE, '--headless=new', '--disable-gpu', '--no-sandbox',
                    '--force-device-scale-factor=2', '--hide-scrollbars',
                    f'--screenshot=E:/CODE/数据结构实验报告/report/fig/{name}.png',
                    f'--window-size=882,{h}', '--default-background-color=FFFFFFFF',
                    'file:///E:/CODE/数据结构实验报告/report/fig/' + name + '.html'],
                   capture_output=True)
    print(name, h)
