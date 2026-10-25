using System;

/// <summary>
/// 实验开关。
/// ★ 默认值 = **当前构建的真实数值**。
///   不改这里，跑出来的就是这一版游戏的真实表现；
///   改这里，可以在**不重新编译 Unity 工程**的前提下扫参数。
/// </summary>
public static class Cfg
{
    // ── 生育 ──
    /// <summary>生育子女上限。>=999 视为无上限。当前构建是 6（CanGiveBirth）</summary>
    /// <summary>0 = 当前包体（有 3 个 bug）；1 = 三个修复全开</summary>
    public static int BugFixes = 0;
    /// <summary>1 = 事件连锁生效；0 = 关（A/B 用）</summary>
    public static int ChainOn = 1;
    public static int ChainBoost = 60;
    public static int ChainMode = 2;
    /// <summary>0 = 构建原样（16-60 岁自动成为耕者）；1 = 要求勤勉词条</summary>
    public static int FarmerMode = 1;

    public static int BirthChildCap = 12;

    // ── 初始人口 ──
    /// <summary>0=原版（一代人） 1=跨两代 2=跨两代且随种子</summary>
    public static int AgeMode = 0;

    // ── 粮仓 ──
    /// <summary>0=固定40(原版) 1=人口x2 2=人口x3 3=人口x4 4=固定20 5=人口/2 6=人口 7=0</summary>
    public static int ReserveMode = 0;
    /// <summary>0=人口x4(原版) 1=人口x5 2=人口x6 3=人口x3</summary>
    public static int ProdMode = 0;

    // ── 耕地 ──
    /// <summary>每次退化的单格概率 %（原版 3）</summary>
    public static int DegradePct = 1;
    /// <summary>耕地低于 基线xN% 就停止退化（0=关，即原版行为）</summary>
    public static int DegradeFloorPct = 0;
    /// <summary>退化后变成荒地的比例 %（原版 35，其余 65 变草地）</summary>
    public static int DegradeWastePct = 35;
    /// <summary>产出对耕地多寡的敏感度（原版 1400）</summary>
    public static int ArableCoef = 1400;

    // ── 移民（当前构建**没有**这个机制，默认关闭）──
    /// <summary>每 2400 tick 到达几人（0=关闭）。本构建 = 2</summary>
    public static int Immigration = 2;
    /// <summary>0=无条件  1=需要装「流民法」(id 17)</summary>
    public static int ImmGate = 0;   // 本构建无条件
    /// <summary>人口低于它才会来</summary>
    public static int ImmThreshold = 25;
    /// <summary>粮仓高于它才会来（★ 别设太高，粮仓常年只有 40 上下）</summary>
    public static int ImmGranary = 20;

    // ── 恐惧法条门槛缩放（100 = 原样）──
    public static int FearThScale = 100;

    // ── 威信 ──
    public static int PrestigePopDiv = 4;      // 人口项 = 人口 / N
    public static int PrestigeFearBase = 30;   // max(0, N - 平均恐惧)
    public static int PrestigeFearDiv = 10;    // 再 / N
    public static int PrestigePerLaw = 3;      // 每条法每 60 tick 的维持费（固定）
    public static int PrestigePerLawDiv = 0;   // >0 则维持费 = 法条数 × 人口/N（人口相对）
    public static int PrestigeCapV = 300;
    /// <summary>软上限：威信超过它之后，进项按 (300 − 威信)/(300 − 它) 线性衰减。0 = 关（原版）</summary>
    public static int PrestigeSoftCap = 0;

    // ── 税库周期进项：每 240 tick += 人口 / N（0 = 关）──
    public static int TreasuryPopDiv = 3;   // 构建自带：每 2400 tick += 人口/3
    // 税库流向统计（诊断用）
    public static long TreasuryIn = 0;
    public static long TreasuryOut = 0;
    public static void Flow(int delta)
    {
        if (delta > 0) TreasuryIn += delta; else if (delta < 0) TreasuryOut -= delta;
    }
    public static void ResetFlow() { TreasuryIn = 0; TreasuryOut = 0; }
    public static int TreasuryPeriod = 240;   // 税库进项的周期
    /// <summary>官吏俸禄：每 2400 tick 税库 −人口 / N。0 = 关（原版没有这一项）</summary>
    public static int TreasuryUpkeepDiv = 4; // 构建自带：官吏俸禄 −人口/4

    // ── 统计用，不要动 ──
    // ── 突发事件（当前构建没有这个系统，默认关）──
    public static int EventChance = 0;        // 旧模型用；构建里的事件已经是原生实现
    public static int NativeEventChance = 45;  // 原生事件的触发率 %（45 = 构建原样；0 = 关）
    public static int ForceHelper = 0;
    public static int EventNegScale = 200;   // 负面量级 %（100 = 原始，200 = x2）        // 1 = 强制村里有对应职业（测职业修正）
    public static int[] EventFired = new int[14];
    public static int[] EventKills = new int[14];      // 每个事件累计杀了几个人
    public static int[] EventGranary = new int[14];    // 每个事件累计掏空多少粮
    public static int ForceEvent = -1;
    public static long[] EventScaleLog = new long[14];   // 累计实际生效的对口折扣 %                 // >=0 = 只让这一个事件触发（测单事件打击量）
    public static int LawModOn = 1;                    // 1 = 启用「对口法律改写事件」
    // ── 事件 × 个人存粮（0 = 关，即当前构建：事件从不碰私粮）──
    public static int FoodCapAdult = 20;     // 每户私粮上限（原版 8）；小孩 4
    public static int FoodCapChild = 10;
    public static int EventFoodHit = 0;     // 1 = 启用
    public static int BanditStealPct = 50;  // 匪患抢走每户私粮的 %
    public static int RiotStealPct = 30;    // 民变砸掉的 %
    public static int HarvestFill = 4;      // 丰年每户补多少私粮
    public static int ColdSpoilPct = 20;    // 寒潮/蝗灾损耗的 %

    public static long Fed = 0;
    // ── 开垦触发方式 ──
    // 0=原样(粮仓∈[15,150))  1=粮仓>=15 且 耕地<基线xK  2=粮仓<人口xK  3=粮仓<K  4=只要求粮仓>=15
    public static int TillMode = 0;
    public static int TillK = 2;

    // 开垦成本：粮 / 金（草地 / 荒地分开）
    public static int TillGrainGrass = 15;
    public static int TillGrainWaste = 15;   // 原版：草地荒地都是 15
    public static int TillWasteFirst = 0;   // 1 = 优先开垦荒地（新机制）；0 = 原版（草荒不分）
    public static int TillGoldGrass = 0;
    public static int TillGoldWaste = 0;

    // ══════════ 劳动力：产出 = 劳动力 × 4（0 = 原版：人口即劳动力）══════════
    /// <summary>0 = 原版（人口×4）；1 = 按年龄/饥饿折算劳动力</summary>
    public static int LaborMode = 1;   // 构建自带劳动力（5×50%）；0 = 老的人口×4
    public static int LabChildAge  = 10;   // 小于它不干活（除非装童工）
    public static int LabAdultAge  = 16;   // 小于它算半劳力
    public static int LabOldAge    = 60;   // 大于等于它算半劳力
    public static int LabChildW    = 50;   // 半劳力的百分比
    public static int LabUnder10W  = 0;    // 10 岁以下（没装童工）的产出 %
    public static int LabOldW      = 50;
    public static int LabStarveFloor = 10; // 饿到极限也还剩几成力气（0 = 饿死前不干活）
    public static int LabChildLaborLaw = 39; // 装了这条法，10 岁以下也能上工
    public static int LabPerWorker = 5;    // 每个全劳力产多少粮（原版每人 4）
    public static int LabHungerScale = 50; // 饥饿折产出的强度 %（100 = 满强度，50 = 只扣一半）

    /// <summary>产出。LaborMode=0 时就是原版的人口×4。</summary>
    public static int Output(VillageSaveData d, int population)
    {
        if (LaborMode <= 0) return Production(population);
        long w = 0;   // 全村劳动力，单位 = 百分之一全劳力
        bool childLabor = false;
        if (d.lawIds != null)
            for (int k = 0; k < d.lawIds.Length; k++) if (d.lawIds[k] == LabChildLaborLaw) childLabor = true;
        for (int i = 0; i < d.villagerAlive.Length; i++)
        {
            if (!d.villagerAlive[i]) continue;
            int age = d.villagerAges[i];
            int pct;
            if (age < LabChildAge)      pct = childLabor ? LabChildW : LabUnder10W;
            else if (age < LabAdultAge) pct = LabChildW;
            else if (age >= LabOldAge)  pct = LabOldW;
            else                        pct = 100;
            int h = d.villagerHunger[i] * LabHungerScale / 100;
            int tired = pct * (100 - h) / 100;
            int floor = pct * LabStarveFloor / 100;
            w += Math.Max(tired, floor);
        }
        return (int)(w * LabPerWorker / 100);
    }

    public static long TillCount = 0;
    public static long TillWasteCount = 0;
    public static long DegradeCount = 0;

    // ══════════ 职业（烙记）获取规则 · 见 ProfRules.cs ══════════
    /// <summary>1 = 启用职业判定。**默认 0 = 当前构建的表现**（19 个烙记只有 3 个会亮）。
    /// ★ 必须默认关：事件系统的「有对应职业 → 效果减半」读的就是这些烙记，
    ///   默认开会把之前所有事件/律法测试的结果改掉（保真度漂移）。</summary>
    public static int ProfOn = 0;
    public static int ProfSinnerFear      = 40;    // 罪人：装「私刑倾向」(7) 且 恐惧 >
    public static int ProfBanditHunger    = 45;    // 劫匪：装「饥则抢」(2)   且 饥饿 >
    public static int ProfDeserterMinAge  = 14;    // 逃兵：装「兵役征召」(15) 且 年龄 ∈ [min,max] 且 恐惧 >
    public static int ProfDeserterMaxAge  = 40;
    public static int ProfDeserterFear    = 25;    //   ★ 原设计写 70，实测恐惧最高 46 —— 区间外
    public static int ProfWornAge         = 40;    // 劳损：装「劳役」(11)    且 年龄 ≥ 且 饥饿 >
    public static int ProfWornHunger      = 56;
    public static int ProfShamanFear      = 30;    // 巫祝：装「祭祀」(35)    且 个人恐惧 >
    public static int ProfLastHeirAge     = 45;    // 绝户：年龄 ≥ 且 曾有子女 > 0 且 **在世子女 = 0**
    public static int ProfBachelorAge     = 50;    // 光棍：年龄 ≥ 且 曾有子女 = 0（原 35 —— 实测 100% 命中）
    public static int ProfDebtorAge       = 25;    // 债户：装「买粮」(26)/「丁税」(27) 且 年龄 ≥
    public static int ProfDebtorPeak      = 30;    //       且 **曾经拥有过 ≥ 这么多金币**（破产过）；当前金币 = 0
    public static int ProfBitterHunger    = 50;    // 积怨：饥饿 > 这个值
    public static int ProfBitterTicks     = 730;  //       连续 ≥ 这么多 tick（5840 = 2 年）
    public static int ProfMournerDeaths   = 15;     // 尸亲：邻居死亡累计 ≥
    public static int ProfPatronDonations = 3;     // 恩主：捐纳触发累计 ≥
    public static int ProfButcherMax      = 1;     // 屠者：村里同时最多几个「执刑者」

    // 职业判定的统计（按烙记的位号 0–15 索引）
    public static long[] ProfGrantCount = new long[16];   // 累计授予人次
    public static int[]  ProfFirstTick  = new int[16];    // 第一次授予的 tick（0 = 从未）

    // 诊断：每条法触发 / 致死多少次（按法条 id 索引）
    public static int[] LawFire  = new int[128];
    public static int[] LawKills = new int[128];
    public static void ResetDiagnostics()
    {
        for (int i = 0; i < LawFire.Length; i++) { LawFire[i] = 0; LawKills[i] = 0; }
    }

    public static void NoteGrant(uint bit, int tick)
    {
        int b = 0; uint v = bit;
        while (v > 1) { v >>= 1; b++; }
        if (b < 0 || b >= 16) return;
        ProfGrantCount[b]++;
        if (ProfFirstTick[b] <= 0) ProfFirstTick[b] = tick > 0 ? tick : 1;
    }

    public static void ResetProf()
    {
        for (int i = 0; i < 16; i++) { ProfGrantCount[i] = 0; ProfFirstTick[i] = 0; }
    }

    public static int Reserve(VillageSaveData d)
    {
        switch (ReserveMode)
        {
            case 1: return Math.Max(20, VillageLifecycleService.CountAlive(d) * 2);
            case 2: return Math.Max(20, VillageLifecycleService.CountAlive(d) * 3);
            case 3: return Math.Max(20, VillageLifecycleService.CountAlive(d) * 4);
            case 4: return 20;
            case 5: return Math.Max(5, VillageLifecycleService.CountAlive(d) / 2);
            case 6: return Math.Max(5, VillageLifecycleService.CountAlive(d));
            case 7: return 0;
            default: return 40;
        }
    }

    public static int Production(int pop)
    {
        switch (ProdMode)
        {
            case 1: return pop * 5;
            case 2: return pop * 6;
            case 3: return pop * 3;
            default: return pop * 4;
        }
    }

    public static int InitAge(int i, int seed)
    {
        switch (AgeMode)
        {
            case 1: return 1 + (i * 13 + 7) % 60;
            case 2: return 1 + ((i * 13 + 7 + seed * 11) % 60);
            case 3: return 1 + ((i * 7 + seed * 5) % 55);
            default:   // 原版：一代人
                if (i == 19) return 78;
                if (i == 9) return 82;
                return 18 + (i * 7 + 5) % 38;
        }
    }
}
