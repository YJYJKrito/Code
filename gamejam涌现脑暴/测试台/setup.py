#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
《立法村》模拟层测试台 · 建立脚本

用法：
    python setup.py "E:\\CODE\\testgame\\My village_Data\\Managed\\Assembly-CSharp.dll"

做四件事：
  1. 用 ilspycmd 把 DLL 反编译成 C#
  2. 从反编译源码里抽出 22 个模拟类 → pscan/Sim.cs
  3. 抽出建局代码 → pscan/Bootstrap.cs
  4. 在 Sim.cs 上打实验开关（Cfg.xxx），让参数可以不改 Unity 工程就扫

★ 关键：如果某条 patch 没打上，脚本会**明确报出来**。
   那说明程序改了那几行 —— 不是脚本坏了，是**测试台和工程对不上了**，
   必须先去核对，否则测出来的数是错的。
"""
import os, re, subprocess, sys, tempfile
try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

WANT = ['VillageSaveData','LifecycleReport','LawTickResult','VillageLifecycleService',
        'LawEngine','Condition','LawCatalog','LawDefinition','LawCond','LawEffect',
        'LawField','LawCondKind','LawScope','LawEffectKind','Rng','Innate','Mark',
        'Traits','VillageTime','Farmland','TerrainType','VillageMap','VillageMapGenerator',
        'VillageEvents','VillagerProfessions','ObitRules','RunRules','LawAttribution']

HERE = os.path.dirname(os.path.abspath(__file__))
OUT  = os.path.join(HERE, 'pscan')

# ─────────────────────────── 实验开关（打在 Sim.cs 上）───────────────────────────
PATCHES = [

 ('生育子女上限',
  '&& data.villagerChildren[index] < 12 &&',
  '&& (Cfg.BirthChildCap >= 999 || data.villagerChildren[index] < Cfg.BirthChildCap) &&'),

 ('粮仓预留线（循环外）',
  '\t\t\tfor (int j = 0; j < data.villagerAlive.Length; j++)\n\t\t\t{\n\t\t\t\tif (data.granaryFood <= 40)\n\t\t\t\t{\n\t\t\t\t\tbreak;\n\t\t\t\t}',
  '\t\t\tint reserve = Cfg.Reserve(data);\n\t\t\tfor (int j = 0; j < data.villagerAlive.Length; j++)\n\t\t\t{\n\t\t\t\tif (data.granaryFood <= reserve)\n\t\t\t\t{\n\t\t\t\t\tbreak;\n\t\t\t\t}'),

 ('粮仓预留线（补粮循环）',
  'while (data.villagerFood[j] < num10 && data.granaryFood > 40)',
  'while (data.villagerFood[j] < num10 && data.granaryFood > reserve)'),

 ('劳动力产出开关',
  'int num4 = LaborOutput(data) + val;',
  'int num4 = Cfg.Output(data, population) + val;'),

 ('私粮上限·成人',
  'private const int PrivateFoodFloorAdult = 20;',
  'private static int PrivateFoodFloorAdult => Cfg.FoodCapAdult;'),

 ('私粮上限·小孩',
  'private const int PrivateFoodFloorChild = 10;',
  'private static int PrivateFoodFloorChild => Cfg.FoodCapChild;'),

   ('退化后变荒地的比例',
  'data.terrain[i] = (byte)((!Rng.Chance(data.rngSeed, tick, 32, i, 35)) ? 1 : 4);',
  'data.terrain[i] = (byte)((!Rng.Chance(data.rngSeed, tick, 32, i, Cfg.DegradeWastePct)) ? 1 : 4);'),

 ('产出对耕地的敏感度',
  'long num8 = 1000 + num7 * 1400 / 1000;',
  'long num8 = 1000 + num7 * Cfg.ArableCoef / 1000;'),

 ('移民参数化',
  '\t\t\tif (tick > 0 && tick % 2400 == 0 && CountAlive(data) < 25 && data.granaryFood > 20)\n\t\t\t{\n\t\t\t\tfor (int j = 0; j < 2; j++)\n\t\t\t\t{\n\t\t\t\t\tif (CountAlive(data) >= 25)\n\t\t\t\t\t{\n\t\t\t\t\t\tbreak;\n\t\t\t\t\t}\n\t\t\t\t\tAppendImmigrant(data);\n\t\t\t\t\treport.immigrants++;\n\t\t\t\t}\n\t\t\t\treport.eventText = "有流民在村口停下了。";\n\t\t\t}',
  '\t\t\tif (tick > 0 && tick % 2400 == 0 && Cfg.Immigration > 0 && CountAlive(data) < Cfg.ImmThreshold && data.granaryFood > Cfg.ImmGranary)\n\t\t\t{\n\t\t\t\tfor (int j = 0; j < Cfg.Immigration; j++)\n\t\t\t\t{\n\t\t\t\t\tif (CountAlive(data) >= Cfg.ImmThreshold)\n\t\t\t\t\t{\n\t\t\t\t\t\tbreak;\n\t\t\t\t\t}\n\t\t\t\t\tAppendImmigrant(data);\n\t\t\t\t\treport.immigrants++;\n\t\t\t\t}\n\t\t\t\treport.eventText = "有流民在村口停下了。";\n\t\t\t}'),

 ('恐惧条件门槛缩放（当前构建已经是 ×70，默认 100 = 不动）',
  '\t\t\tLawCondKind kind = (LawCondKind)cond.kind;\n\t\t\tLawField field = (LawField)cond.field;',
  '\t\t\tLawCondKind kind = (LawCondKind)cond.kind;\n\t\t\tLawField field = (LawField)cond.field;\n\t\t\tif (field == LawField.Fear && Cfg.FearThScale != 100) cond.a = cond.a * Cfg.FearThScale / 100;'),

 ('原生事件的触发率开关（默认 45 = 构建原样）',
  'if (Rng.Range(data.rngSeed, tick, 62, 0, 0, 100) >= 45)',
  'if (Cfg.NativeEventChance <= 0 || Rng.Range(data.rngSeed, tick, 62, 0, 0, 100) >= Cfg.NativeEventChance)'),

 ("税库进项参数化",
  "data.treasuryGold = Math.Min(999999, data.treasuryGold + CountAlive(data) / 3);",
  "data.treasuryGold = Math.Min(999999, data.treasuryGold + ((Cfg.TreasuryPopDiv > 0) ? (CountAlive(data) / Cfg.TreasuryPopDiv) : 0));"),

 ("税库俸禄参数化",
  "data.treasuryGold = Math.Max(0, data.treasuryGold - CountAlive(data) / 4);",
  "data.treasuryGold = Math.Max(0, data.treasuryGold - ((Cfg.TreasuryUpkeepDiv > 0) ? (CountAlive(data) / Cfg.TreasuryUpkeepDiv) : 0));"),

 ('Bootstrap.GetVillagerName 转发',
  'VillageSaveService.GetVillagerName',
  'Bootstrap.GetVillagerName'),

    ("耕者角色：要求勤勉词条（Cfg.FarmerMode=1 生效）",
  "if (num2 >= 16 && num2 < 60 && !Traits.HasMark(data, j, 1u) && !Traits.HasMark(data, j, 2u))",
  "if (num2 >= 16 && num2 < 60 && !Traits.HasMark(data, j, 1u) && !Traits.HasMark(data, j, 2u) && (Cfg.FarmerMode == 0 || Traits.HasInnate(data, j, 16u)))"),

 ("劫匪阈值参数化（构建 55；推荐 45）",
  "if (HasLaw(data, 2) && num3 > 55)",
  "if (HasLaw(data, 2) && num3 > Cfg.ProfBanditHunger)"),

 ("蝗灾伤害计数（诊断）",
  "data.granaryFood = ClampGranary(data, data.granaryFood - Sk(data.granaryFood * (flag ? 30 : 60) / 100, pct));",
  "int _g0 = data.granaryFood;" + chr(10) + "\t\t\t\t" + "data.granaryFood = ClampGranary(data, data.granaryFood - Sk(data.granaryFood * (flag ? 30 : 60) / 100, pct));" + chr(10) + "\t\t\t\t" + "Cfg.EventGranary[0] += _g0 - data.granaryFood;"),

 ("寒潮伤害计数（诊断）",
  "data.granaryFood = ClampGranary(data, data.granaryFood - Sk(data.granaryFood * (flag6 ? 10 : 20) / 100, pct));",
  "int _g5 = data.granaryFood;" + chr(10) + "\t\t\t\t" + "data.granaryFood = ClampGranary(data, data.granaryFood - Sk(data.granaryFood * (flag6 ? 10 : 20) / 100, pct));" + chr(10) + "\t\t\t\t" + "Cfg.EventGranary[5] += _g5 - data.granaryFood;"),

 ("事件连锁 A/B 开关",
  "1 => 1 + num / 15 + ((tick < data.chainPlague) ? 30 : 0),",
  "1 => 1 + num / 15 + ((Cfg.ChainMode == 1 && tick < data.chainPlague) ? 30 : 0),"),

 ("事件连锁 A/B 开关 · 民变",
  "3 => 1 + (300 - data.prestige) / 12 + num3 / 3 + ((tick < data.chainMobilize) ? 60 : 0),",
  "3 => 1 + (300 - data.prestige) / 12 + num3 / 3 + ((Cfg.ChainMode == 1 && tick < data.chainMobilize) ? 60 : 0),"),

]

AGE_OLD = """\t\t\t\tvillageSaveData.villagerAges[i] = i switch
\t\t\t\t{
\t\t\t\t\t19 => 78,
\t\t\t\t\t9 => 82,
\t\t\t\t\t_ => 18 + (i * 7 + 5) % 38,
\t\t\t\t};"""
AGE_NEW = "\t\t\t\tvillageSaveData.villagerAges[i] = Cfg.InitAge(i, seed);"


def sh(cmd):
    return subprocess.run(cmd, shell=True, capture_output=True, text=True)


def main():
    if len(sys.argv) < 2:
        print(__doc__); sys.exit(1)
    dll = sys.argv[1]
    if not os.path.isfile(dll):
        print("找不到 DLL：", dll); sys.exit(1)

    os.makedirs(OUT, exist_ok=True)
    tmp = tempfile.mkdtemp(prefix='lv_decomp_')

    # ── 1. 反编译 ──
    ilspy = os.path.expanduser('~/.dotnet/tools/ilspycmd')
    if os.name == 'nt': ilspy += '.exe'
    print("① 反编译 ...")
    r = sh(f'"{ilspy}" -o "{tmp}" "{dll}"')
    dec = os.path.join(tmp, 'Assembly-CSharp.decompiled.cs')
    if not os.path.isfile(dec):
        print("反编译失败：", r.stdout, r.stderr); sys.exit(1)
    src = open(dec, encoding='utf-8').read()
    lines = src.split('\n')
    print(f"   {len(lines)} 行")

    # ── 2. 抽模拟类 ──
    print("② 抽取模拟类 ...")
    found, miss, i = {}, [], 0
    while i < len(lines):
        m = re.match(r'^\t(?:public|internal)\s+(?:static\s+|sealed\s+|abstract\s+|readonly\s+)*(class|struct|enum)\s+(\w+)', lines[i])
        if m:
            nm, j = m.group(2), i
            while j < len(lines) and lines[j] != '\t}': j += 1
            if nm in WANT and nm not in found:
                found[nm] = '\n'.join(lines[i:j+1]); i = j
        i += 1
    for w in WANT:
        if w not in found: miss.append(w)
    if miss:
        print("   ★ 缺这些类（可能被改名/删掉）：", ' '.join(miss))
    sim = ('using System;\nusing System.Collections.Generic;\nusing System.Globalization;\nusing System.Text;\n\n'
           + '\n\n'.join(found[w] for w in WANT if w in found) + '\n')
    print(f"   {len(found)} 个类")

    # ── 3. 抽建局代码 ──
    print("③ 抽取建局代码 ...")
    def field(sig):
        a = src.find(sig); b = src.find(';', src.find('{', a)); return src[a:b+1]
    def method(sig):
        a = src.find(sig); b = src.find('\n\t\t}', a); return src[a:b+4]
    try:
        body = '\n\n'.join([
            field('private static readonly string[] GeneratedSurnames'),
            field('private static readonly string[] GeneratedGivens'),
            method('public static string GetVillagerName(int seed, int serial)'),
            method('public static VillageSaveData CreateNewGame(int slot, int seed)')])
    except Exception as e:
        print("   建局代码抽取失败：", e); sys.exit(1)
    boot = ('using System;\nusing System.Globalization;\n\npublic static class Bootstrap {\n'
            + body.replace('VillageSaveService.', 'Bootstrap.') + '\n}\n')

    # ── 4. 打实验开关 ──
    print("④ 打实验开关 ...")
    failed = []
    REPLACE_ALL = ('Bootstrap.GetVillagerName 转发',)   # 这两处都要换（AppendChild 和 AppendImmigrant）
    for name, old, new in PATCHES:
        n = sim.count(old)
        if n == 0:
            failed.append(name); print(f"   [FAIL] {name} —— 找不到目标代码")
        elif name in REPLACE_ALL:
            sim = sim.replace(old, new); print(f"   [OK]   {name}（替换了 {n} 处）")
        elif n > 1:
            sim = sim.replace(old, new, 1); print(f"   [WARN] {name} —— 匹配到 {n} 处，只改了第 1 处")
        else:
            sim = sim.replace(old, new, 1); print(f"   [OK]   {name}")
    # 注意：反编译产物在这几行有**行尾空格**，所以用正则而不是字面量匹配
    boot, nsub = re.subn(r'villageSaveData\.villagerAges\[i\] = i switch\s*\{[^}]*\};', AGE_NEW, boot)
    if nsub:
        print("   [OK]   初始年龄分布")
    else:
        failed.append("初始年龄分布"); print("   [FAIL] 初始年龄分布 —— 找不到目标代码")

    # ── 5. 职业（烙记）获取规则的插入点 ──
    #    必须走这一步：上面 PATCHES 是「替换」，这里是「插入调用点」，
    #    真正的规则代码在 pscan/ProfRules.cs（不被重生成覆盖）。
    try:
        import prof_patch
        sim, plog = prof_patch.apply(sim)
        for nm, st in plog:
            tag = st.split()[0]
            print(f"   [{tag:<4}] {nm}")
            if tag == "FAIL":
                failed.append(nm)
    except Exception as e:
        print("   [FAIL] prof_patch ——", e); failed.append("prof_patch")

    # ── 6. 事件系统的两项规格补丁（对口法律 / 事件连锁 / 冷却 3 年）──
    try:
        import event_patch
        sim, elog = event_patch.apply(sim)
        for nm, st in elog:
            tag = st.split()[0]
            print(f"   [{tag:<4}] {nm}")
            if tag == "FAIL":
                failed.append(nm)
    except Exception as e:
        print("   [FAIL] event_patch ——", e); failed.append("event_patch")

    # ── 7. 讣告归因重做（ObitRules.cs）──
    try:
        olog = []  # 构建已原生实现讣告归因
        for nm, st in olog:
            tag = st.split()[0]
            print(f"   [{tag:<4}] {nm}")
            if tag == "FAIL":
                failed.append(nm)
    except Exception as e:
        print("   [FAIL] obit_patch ——", e); failed.append("obit_patch")

    # ── 名存实亡用 endKind 5（不要和期满共用 4）──
    sim, nsub = re.subn(r'(if \(data\.tinyStreak >= 24\)\s*\{\s*data\.endKind = )4;', r'\g<1>5;', sim, count=1)
    if nsub:
        print("   [OK]   名存实亡 endKind 5")
    else:
        print("   [FAIL] 名存实亡 endKind 5")
        failed.append("名存实亡 endKind 5")

    open(os.path.join(OUT, 'Sim.cs'), 'w', encoding='utf-8').write(sim)
    open(os.path.join(OUT, 'Bootstrap.cs'), 'w', encoding='utf-8').write(boot)

    print()
    if failed:
        print("=" * 60)
        print("*** 有 patch 没打上 ***")
        for f in failed: print("   -", f)
        print("这通常意味着**程序动过那几行**。")
        print("先去看《测试台说明.md》的「当 patch 失败时」一节，核对完再跑测试。")
        print("=" * 60)
    print("\n下一步：")
    print(f'   cd "{OUT}" && dotnet run -c Release -- lawsets 40')


if __name__ == '__main__':
    main()
