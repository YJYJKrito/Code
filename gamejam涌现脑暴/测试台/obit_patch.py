# -*- coding: utf-8 -*-
"""
讣告归因重做（测试台版）。

修的问题：`PreferredLaw` 只看装没装，不看是不是它干的 →
装了「幼童口粮/医药」之后，每个幼儿死亡都写成「死于你写的医药」。

规则移到 pscan/ObitRules.cs（不被重生成覆盖），这里只做接线。
"""

NL = chr(10)
T = chr(9)


def _sub_once(sim, old, new, log, name):
    n = sim.count(old)
    if n == 0:
        log.append((name, "FAIL 找不到"))
        return sim
    if n > 1:
        log.append((name, "WARN 匹配 %d 处，只改第 1 处" % n))
    else:
        log.append((name, "OK"))
    return sim.replace(old, new, 1)


def apply(sim):
    log = []

    # 1) 新字段
    if "obituaryLawKinds" not in sim:
        sim = _sub_once(sim,
            "		public string[] obituaryLawNames = new string[0];",
            "		public string[] obituaryLawNames = new string[0];" + NL + NL +
            "		public byte[] obituaryLawKinds = new byte[0];",
            log, "讣告归因字段")
    else:
        log.append(("讣告归因字段", "SKIP"))

    # 2) 三个死因的归因
    sim = _sub_once(sim,
        'KillVillager(data, i, "饥饿", PreferredLaw(data, 1, 3, 11));',
        'KillVillager(data, i, "饥饿", ObitRules.AttributeSlot(data, "饥饿"));',
        log, "归因 · 饥饿")
    sim = _sub_once(sim,
        'KillVillager(data, i, "惊吓", PreferredLaw(data, 4, 6, 10));',
        'KillVillager(data, i, "惊吓", ObitRules.AttributeSlot(data, "惊吓"));',
        log, "归因 · 惊吓")
    sim = _sub_once(sim,
        'KillVillager(data, i, "夭折", PreferredLaw(data, 9, 12));',
        'KillVillager(data, i, "夭折", ObitRules.AttributeSlot(data, "夭折"));',
        log, "归因 · 夭折")

    # 3) 寿终：无归因
    sim = _sub_once(sim,
        '			KillVillager(data, i, "寿终", -1);',
        '			ObitRules.LastKind = 0; ObitRules.LastName = null;' + NL +
        '			KillVillager(data, i, "寿终", -1);',
        log, "归因 · 寿终")

    # 4) 数组补齐
    sim = _sub_once(sim,
        "			Array.Resize(ref data.obituaryLawNames, obituaryCount + 1);",
        "			Array.Resize(ref data.obituaryLawNames, obituaryCount + 1);" + NL +
        "			Array.Resize(ref data.obituaryLawKinds, obituaryCount + 1);",
        log, "讣告数组补齐")

    # 5) 归因 kind / 法名 / 文本
    old_build = "			data.obituaryTexts[obituaryCount] = BuildObituaryText(data, villagerIndex, cause, lawIndex);"
    new_build = (
        '			if (cause == "疫病" || cause == "边患" || cause == "民变" || cause == "匪患")' + NL +
        '			{' + NL +
        '				int _es; string _en;' + NL +
        '				ObitRules.LastKind = ObitRules.Attribute(data, cause, out _es, out _en);' + NL +
        '				ObitRules.LastName = _en;' + NL +
        '			}' + NL +
        '			byte _ok3 = (cause == "律法") ? ObitRules.KindCaused : ObitRules.LastKind;' + NL +
        '			string _on3 = (lawIndex >= 0 && lawIndex < data.lawNames.Length)' + NL +
        '				? data.lawNames[lawIndex]' + NL +
        '				: ((_ok3 == ObitRules.KindMissing) ? ObitRules.LastName : string.Empty);' + NL +
        '			data.obituaryLawKinds[obituaryCount] = _ok3;' + NL +
        '			data.obituaryTexts[obituaryCount] = ObitRules.Build(data, villagerIndex, cause, lawIndex, _ok3, _on3);')
    sim = _sub_once(sim, old_build, new_build, log, "讣告文本")

    # 6) 法名用 _on3
    sim = _sub_once(sim,
        "			data.obituaryLawNames[obituaryCount] = ((lawIndex >= 0 && lawIndex < data.lawNames.Length) ? data.lawNames[lawIndex] : string.Empty);",
        "			data.obituaryLawNames[obituaryCount] = _on3;",
        log, "讣告法名")

    # 7) 每次死亡后清空，避免串到下一次
    sim = _sub_once(sim,
        "			data.obituaryCount++;\n			data.totalDeaths++;",
        "			data.obituaryCount++;\n			data.totalDeaths++;\n			ObitRules.LastKind = 0; ObitRules.LastName = null;",
        log, "归因状态清理")

    return sim, log


if __name__ == "__main__":
    import io
    p = r"E:\CODE\gamejam涌现脑暴\测试台\pscan\Sim.cs"
    s = io.open(p, encoding="utf-8").read()
    s2, log = apply(s)
    for n, r in log:
        print("   [%-4s] %s" % (r.split()[0], n))
    io.open(p, "w", encoding="utf-8").write(s2)
