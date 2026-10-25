# -*- coding: utf-8 -*-
"""把各章节 markdown 装配成实验报告 HTML，再用 Edge 无头模式打印成 PDF，
最后把"页眉+页码"覆盖层合并到每一页上。

用法：  uv run --python <python.exe> build.py
"""
import html
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "build")
os.makedirs(OUT, exist_ok=True)

EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"

HEADER_TEXT = "大连理工开发区校区软件学院《数字逻辑系统实验》课程实验报告（2026年度秋，实验04）"

STUDENT = {
    "课序号": "02",
    "班级": "软智2504",
    "学号": "20252082264",
    "姓名": "郝永健",
}

sys.path.insert(0, HERE)
import figures  # noqa: E402


# ---------------------------------------------------------------- markdown → html
def md_to_html(md: str) -> str:
    """把章节 markdown 转成报告正文 HTML。

    支持：中文章节标题（一、二、…）、阿拉伯数字小节（1. 2. …）、
    全角括号小节（（1）（2））、Markdown 表格、普通段落。
    """
    lines = md.replace("\r\n", "\n").split("\n")
    out = []
    i = 0
    while i < len(lines):
        raw = lines[i]
        line = raw.strip()
        if not line:
            i += 1
            continue

        # Markdown 表格
        if line.startswith("|") and i + 1 < len(lines) and re.match(r"^\|[\s:|-]+\|$", lines[i + 1].strip()):
            head_cells = [c.strip() for c in line.strip("|").split("|")]
            i += 2
            body = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                body.append([c.strip() for c in lines[i].strip().strip("|").split("|")])
                i += 1
            th = "".join(f"<th>{inline(c)}</th>" for c in head_cells)
            trs = "".join(
                "<tr>" + "".join(f"<td>{inline(c)}</td>" for c in r) + "</tr>" for r in body
            )
            out.append(f'<table class="dt"><thead><tr>{th}</tr></thead><tbody>{trs}</tbody></table>')
            continue

        # 一级中文标题：一、二、三、
        if re.match(r"^[一二三四五六七八九十]+、", line):
            out.append(f"<h2>{inline(line)}</h2>")
            i += 1
            continue

        # 表题 / 图题（独占一行）
        if re.match(r"^表\s*4\.\d+", line):
            out.append(f'<p class="tcap">{inline(line)}</p>')
            i += 1
            continue
        if re.match(r"^图\s*4\.\d+", line):
            out.append(f'<p class="fcap">{inline(line)}</p>')
            i += 1
            continue

        # 阿拉伯数字小节：1. 2.
        if re.match(r"^\d+\.\s*\S", line):
            out.append(f"<h3>{inline(line)}</h3>")
            i += 1
            continue

        # 全角括号小节：（1）（2）
        if re.match(r"^（\d+）", line):
            out.append(f'<p class="h4">{inline(line)}</p>')
            i += 1
            continue

        # 逻辑表达式独占一行（如 Z3 = Σm(15)）
        if re.match(r"^Z[0-3]\s*=", line):
            out.append(f'<p class="formula">{inline(line)}</p>')
            i += 1
            continue

        # 普通段落：把连续的普通行合成一段
        buf = [line]
        i += 1
        while i < len(lines):
            nxt = lines[i].strip()
            if not nxt:
                break
            if (nxt.startswith("|") or re.match(r"^[一二三四五六七八九十]+、", nxt)
                    or re.match(r"^\d+\.\s*\S", nxt) or re.match(r"^（\d+）", nxt)
                    or re.match(r"^表\s*4\.\d+", nxt) or re.match(r"^图\s*4\.\d+", nxt)
                    or re.match(r"^Z[0-3]\s*=", nxt)):
                break
            buf.append(nxt)
            i += 1
        out.append(f"<p>{inline(''.join(buf))}</p>")
    return "\n".join(out)


def inline(text: str) -> str:
    """行内处理：转义 HTML，再把 *斜体* 转换，保护上标撇号。"""
    t = html.escape(text)
    t = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", t)
    t = re.sub(r"(?<!\*)\*(?!\s)(.+?)(?<!\s)\*(?!\*)", r"<i>\1</i>", t)
    # 与非/反变量用上标撇号时不特殊处理，保持原样
    return t


# ---------------------------------------------------------------- 页面模板
def figure(svg: str, caption: str) -> str:
    return f'<div class="figwrap">{svg}<p class="fcap">{caption}</p></div>'


def build_body_html(sections: dict) -> str:
    part1 = md_to_html(sections["S1"])
    part2 = md_to_html(sections["S2"])
    part3 = md_to_html(sections["S3"])
    part4 = md_to_html(sections["S4"])

    fig41 = figure(figures.fig_block_diagram(), "图 4.1  2 位二进制乘法器电路系统功能框图")
    fig42 = figure(figures.fig_decoder(), "图 4.2  用两片 74HC138 扩展为 4-16 译码器的连接示意图")
    fig43 = figure(figures.fig_current_loop(), "图 4.3  共阳极数码管一位段的驱动回路")
    fig44 = figure(figures.fig_iv_curve(), "图 4.4  段电流 I 随限流电阻 R 的变化及可用区")

    # 把自绘的图插到相应章节里：以标记行 %%FIG41%% 等形式由 sections 提供
    blocks = {
        "%%FIG41%%": fig41, "%%FIG42%%": fig42,
        "%%FIG43%%": fig43, "%%FIG44%%": fig44,
    }

    def inject(s):
        for k, v in blocks.items():
            s = s.replace(f"<p>{k}</p>", v).replace(k, v)
        return s

    info = "".join(
        f"<td>{k}：<u>{v}</u></td>" for k, v in STUDENT.items()
    )

    return f"""<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8">
<title>{HEADER_TEXT}</title>
<style>
@page {{ size: A4; margin: 24mm 18mm 18mm 18mm; }}
html, body {{ margin: 0; padding: 0; }}
body {{ font-family: "SimSun", serif; font-size: 10.5pt; line-height: 1.62; color: #000; }}

h1.title {{ font-family: "SimHei", sans-serif; font-size: 16pt; text-align: center;
            margin: 6pt 0 16pt; letter-spacing: 1px; }}
p.subtitle {{ font-family: "SimHei", sans-serif; font-size: 13pt; text-align: center;
              margin: 0 0 18pt; }}

table.info {{ width: 100%; border-collapse: collapse; font-size: 10.5pt;
              margin: 0 0 4pt; table-layout: auto; }}
table.info td {{ border: 0; text-align: center; padding: 0 4pt; white-space: nowrap; }}
table.info u {{ text-decoration: none; border-bottom: 0.75pt solid #000; padding: 0 7pt; }}

table.sign {{ width: 100%; border-collapse: collapse; font-size: 10.5pt; margin: 20pt 0 0; }}
table.sign td {{ border: 0; padding: 0; }}
table.sign td.lbl {{ white-space: nowrap; width: 1%; text-align: right; }}
table.sign td.line {{ border-bottom: 0.75pt solid #000; }}

h2 {{ font-family: "SimHei", sans-serif; font-size: 12pt; font-weight: bold;
      text-indent: 2em; margin: 15pt 0 5pt; page-break-after: avoid; }}
h3 {{ font-size: 10.5pt; font-weight: bold; text-indent: 2em; margin: 11pt 0 4pt;
      page-break-after: avoid; }}
p.h4 {{ font-size: 10.5pt; font-weight: bold; text-indent: 2em; margin: 8pt 0 3pt;
        page-break-after: avoid; }}
p {{ text-indent: 2em; margin: 0 0 5pt; text-align: justify; }}
p.formula {{ text-indent: 0; margin: 3pt 0 3pt 4em; font-family: "Cambria", "Times New Roman", serif; }}

table.dt {{ border-collapse: collapse; width: 100%; margin: 3pt 0 8pt;
            font-size: 9pt; page-break-inside: avoid; }}
table.dt th, table.dt td {{ border: 0.75pt solid #000; padding: 2.2pt 3pt;
                            text-align: center; vertical-align: middle; }}
table.dt th {{ font-weight: bold; background: #f2f2f2; }}
table.dt td {{ text-indent: 0; }}

p.tcap {{ text-align: center; text-indent: 0; font-size: 9pt; margin: 8pt 0 0;
          page-break-after: avoid; }}
p.fcap {{ text-align: center; text-indent: 0; font-size: 9pt; margin: 3pt 0 10pt; }}

.figwrap {{ text-align: center; margin: 8pt 0 4pt; page-break-inside: avoid; }}
svg.fig {{ width: 100%; max-width: 148mm; height: auto; }}

.headrule {{ border: 0; border-top: 1.4pt solid #000; margin: 26pt 0 6pt; }}
</style></head>
<body>

<p class="subtitle">实验 04  组合逻辑综合设计</p>
<h1 class="title">实验学生个人信息栏</h1>
<table class="info"><tr>{info}</tr></table>
<hr class="headrule">
<table class="sign"><tr>
  <td class="lbl">实验 04 得分：</td><td class="line"></td>
  <td class="lbl">实验教师（签字）：</td><td class="line"></td>
</tr></table>

<div style="height:14pt"></div>

{inject(part1)}
{inject(part2)}
{inject(part3)}
{inject(part4)}

</body></html>
"""


def build_overlay_html(n: int) -> str:
    page = (
        '<div class="pg">'
        f'<div class="hdr">{HEADER_TEXT}</div>'
        '<div class="pno">__N__</div>'
        '</div>'
    )
    pages = "\n".join(page.replace("__N__", str(i + 1)) for i in range(n))
    return f"""<!doctype html>
<html><head><meta charset="utf-8"><style>
@page {{ size: A4; margin: 0; }}
html, body {{ margin: 0; padding: 0; }}
.pg {{ position: relative; width: 210mm; height: 297mm; overflow: hidden;
       page-break-after: always; }}
.pg:last-child {{ page-break-after: auto; }}
.hdr {{ position: absolute; top: 11mm; left: 18mm; right: 18mm;
        font-family: "SimSun", serif; font-size: 9pt;
        border-bottom: 0.75pt solid #000; padding-bottom: 1.6mm; }}
.pno {{ position: absolute; bottom: 10mm; left: 0; right: 0; text-align: center;
        font-family: "SimSun", serif; font-size: 9pt; }}
</style></head><body>
{pages}
</body></html>
"""


def edge_pdf(src_html: str, dst_pdf: str) -> None:
    subprocess.run(
        [EDGE, "--headless=new", "--disable-gpu", "--no-sandbox",
         f"--print-to-pdf={dst_pdf}", "--no-pdf-header-footer",
         "file:///" + src_html.replace("\\", "/")],
        check=True, capture_output=True, timeout=180,
    )


def main() -> None:
    from pypdf import PdfReader, PdfWriter

    sections = {k: open(os.path.join(HERE, f"{k}.md"), encoding="utf-8").read()
                for k in ("S1", "S2", "S3", "S4")}

    body_html = os.path.join(OUT, "body.html")
    body_pdf = os.path.join(OUT, "body.pdf")
    with open(body_html, "w", encoding="utf-8") as f:
        f.write(build_body_html(sections))

    edge_pdf(body_html, body_pdf)
    n = len(PdfReader(body_pdf).pages)
    print(f"正文 {n} 页")

    ov_html = os.path.join(OUT, "overlay.html")
    ov_pdf = os.path.join(OUT, "overlay.pdf")
    with open(ov_html, "w", encoding="utf-8") as f:
        f.write(build_overlay_html(n))
    edge_pdf(ov_html, ov_pdf)

    writer = PdfWriter()
    ov = PdfReader(ov_pdf)
    for i, pg in enumerate(PdfReader(body_pdf).pages):
        pg.merge_page(ov.pages[i] if i < len(ov.pages) else ov.pages[-1])
        writer.add_page(pg)

    final = os.path.join(ROOT, "PDF实验04报告正文.pdf")
    with open(final, "wb") as f:
        writer.write(f)
    print("已生成:", final)

    # 下载页
    import page as page_mod
    name = os.path.basename(final)
    with open(os.path.join(ROOT, "index.html"), "w", encoding="utf-8") as f:
        f.write(page_mod.build(name, os.path.getsize(final), n))
    print("已生成:", os.path.join(ROOT, "index.html"))


if __name__ == "__main__":
    main()
