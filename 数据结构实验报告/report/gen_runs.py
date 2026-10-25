import os, html, subprocess

FIG = r'E:\CODE\数据结构实验报告\report\fig'
EDGE = r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
PR = r'E:\CODE\数据结构实验报告\code>'

RUNS = {
    'run1_1': ('exp1_1.exe', ['5', '1 2 3 4 5', '2'], [
        '链表中的数据为:1 2 3 4 5 ', '倒数第 2 个结点的值为:4']),
    'run1_2': ('exp1_1.exe', ['5', '1 2 3 4 5', '7'], [
        '链表中的数据为:1 2 3 4 5 ', '倒数第 7 个结点不存在']),
    'run1_3': ('exp1_1.exe', ['5', '1 2 3 4 5', '1'], [
        '链表中的数据为:1 2 3 4 5 ', '倒数第 1 个结点的值为:5']),
    'run2_1': ('exp1_2.exe', ['4', '3 5 9 2'], [
        '链表中的数据为:3 5 9 2 ', '链表中的最大值为:9', '链表中结点的个数为:4',
        '链表中所有整数的平均值为:4.75']),
    'run2_2': ('exp1_2.exe', ['1', '7'], [
        '链表中的数据为:7 ', '链表中的最大值为:7', '链表中结点的个数为:1',
        '链表中所有整数的平均值为:7']),
    'run2_3': ('exp1_2.exe', ['0'], [
        '链表中的数据为:(空表)', '链表为空,不能求最大值和平均值', '结点个数为:0']),
    'run3_1': ('exp1_3.exe', ['3', '1 3 5', '3', '2 4 6'], [
        '递增有序链表A为:1 3 5 ', '递增有序链表B为:2 4 6 ',
        '归并后递减有序链表C为:6 5 4 3 2 1 ']),
    'run3_2': ('exp1_3.exe', ['3', '1 2 3', '3', '2 3 4'], [
        '递增有序链表A为:1 2 3 ', '递增有序链表B为:2 3 4 ',
        '归并后递减有序链表C为:4 3 3 2 2 1 ']),
    'run3_3': ('exp1_3.exe', ['0', '3', '1 2 3'], [
        '递增有序链表A为:', '递增有序链表B为:1 2 3 ',
        '归并后递减有序链表C为:3 2 1 ']),
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
 .cmd{color:#cccccc}
</style>
<div class="win">
 <div class="bar">命令提示符 - __EXE__<span>— &nbsp;□ &nbsp;×</span></div>
 <div class="scr">__BODY__</div>
</div>
"""

for name, (exe, ins, outs) in RUNS.items():
    body = html.escape(PR + exe) + '\n' + '\n'.join(html.escape(i) for i in ins) + '\n'
    body += '\n'.join(html.escape(o) for o in outs) + '\n\n'
    out_html = os.path.join(FIG, name + '.html')
    open(out_html, 'w', encoding='utf-8').write(
        TPL.replace('__EXE__', exe).replace('__BODY__', body))
    h = 38 + 24 + (body.count('\n') + 1) * 26
    subprocess.run([EDGE, '--headless=new', '--disable-gpu', '--no-sandbox',
                    '--force-device-scale-factor=2', '--hide-scrollbars',
                    f'--screenshot=E:/CODE/数据结构实验报告/report/fig/{name}.png',
                    f'--window-size=882,{h}', '--default-background-color=FFFFFFFF',
                    'file:///E:/CODE/数据结构实验报告/report/fig/' + name + '.html'],
                   capture_output=True)
    print(name, h)
