# -*- coding: utf-8 -*-
"""
测试台的**插入型**补丁（替换型补丁在 setup.py 的 PATCHES 里）。

★ 2026-10-18 重构：程序已经把职业、事件、劳动力、税库、威信、私粮**全部做进构建**，
  所以这里不再插入任何"模拟逻辑" —— 只保留**诊断计数器**（税库流向）。
  规则本体请直接读 Sim.cs 里的 VillagerProfessions / VillageEvents。

锚点用正则匹配（行首缩进任意），不受反编译缩进/行尾空格影响。
"""

import re

NL = chr(10)

POST_PATCHES = [

 ("事件计数（诊断）",
  ["bool flag = Role(data, 1u);"],
  ["Cfg.EventFired[id]++;"],
  "before"),

 ("税库流向 · 阶段进项",
  ["data.treasuryGold = Math.Min(999999, data.treasuryGold + ((Cfg.TreasuryPopDiv > 0) ? (CountAlive(data) / Cfg.TreasuryPopDiv) : 0));"],
  ["if (Cfg.TreasuryPopDiv > 0) Cfg.TreasuryIn += CountAlive(data) / Cfg.TreasuryPopDiv;"]),

 ("税库流向 · 官吏俸禄",
  ["data.treasuryGold = Math.Max(0, data.treasuryGold - ((Cfg.TreasuryUpkeepDiv > 0) ? (CountAlive(data) / Cfg.TreasuryUpkeepDiv) : 0));"],
  ["if (Cfg.TreasuryUpkeepDiv > 0) Cfg.TreasuryOut += CountAlive(data) / Cfg.TreasuryUpkeepDiv;"]),

 ("税库流向 · 法条",
  ["d.treasuryGold = Math.Max(0, d.treasuryGold + amount);"],
  ["Cfg.Flow(amount);"]),

 ("税库流向 · 税收法",
  ["data.treasuryGold += 3;"],
  ["Cfg.TreasuryIn += 3;"]),

 ("税库流向 · 开垦",
  ["data.treasuryGold -= num3;"],
  ["Cfg.TreasuryOut += num3;"]),
]


def _pattern(anchors):
    return re.compile(
        r'^[ \t]*' + (NL + r'[ \t]*').join(re.escape(x.strip()) for x in anchors),
        re.M)


def _indent_of(line):
    return line[:len(line) - len(line.lstrip())]


def apply(sim, verbose=True):
    log = []
    for entry in POST_PATCHES:
        name, anchors, lines = entry[0], entry[1], entry[2]
        mode = entry[3] if len(entry) > 3 else "after"
        pat = _pattern(anchors)
        ms = list(pat.finditer(sim))
        if not ms:
            log.append((name, "FAIL 找不到锚点"))
            continue
        if len(ms) > 1:
            log.append((name, "WARN 匹配 %d 处，只改第 1 处" % len(ms)))
        m = ms[0]
        text = m.group(0)
        last = text.split(NL)[-1]
        indent = _indent_of(last)
        end = m.end()

        if mode == "before":
            probe = sim[max(0, m.start() - 3000):m.start()].split(NL)[-(len(lines) + 2):]
        else:
            probe = sim[end:end + 600].split(NL)[:len(lines) + 2]
        sig = [l.strip() for l in lines if len(l.strip()) > 15][:1]
        if sig and any(x.strip() and x.strip() == sig[0] for x in probe):
            log.append((name, "SKIP 已打过"))
            continue

        ins = NL.join(indent + x if x.strip() else "" for x in lines)
        if mode == "before":
            ls = sim.rfind(NL, 0, m.start()) + 1
            sim = sim[:ls] + ins + NL + sim[ls:]
        else:
            sim = sim[:end] + NL + ins + sim[end:]
        log.append((name, "OK"))
    return sim, log


if __name__ == "__main__":
    import io
    p = r"E:\CODE\gamejam涌现脑暴\测试台\pscan\Sim.cs"
    s = io.open(p, encoding="utf-8").read()
    s2, log = apply(s)
    for n, r in log:
        print("   [%-4s] %s" % (r.split()[0], n))
    io.open(p, "w", encoding="utf-8").write(s2)
    print("   Sim.cs 已更新")
