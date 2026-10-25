using System; using System.Collections.Generic;

/// <summary>
/// 14 个事件的**效果层**（第一版只做触发层，没做效果）。
/// 和 Events.cs 用同一张权重表 —— 那边测"会不会触发"，这边测"触发了有没有用"。
/// </summary>
public static class EventFx
{
    public const int Count = 14;

    public static readonly string[] Names = {
        "蝗灾","疫病","边患","民变","贪墨","寒潮","匪患",
        "丰年","商队","乡绅","人口回流","名医","大赦","新生儿潮"
    };

    /// <summary>权重（和《设计》§4.5 一致）</summary>
    public static int Weight(VillageSaveData d, int e) {
        int pop = VillageLifecycleService.CountAlive(d);
        long h = 0, f = 0; int a = 0;
        for (int i = 0; i < d.villagerAlive.Length; i++) if (d.villagerAlive[i]) {
            h += d.villagerHunger[i]; f += d.villagerFear[i]; a++;
        }
        if (a == 0) a = 1;
        int avgHunger = (int)(h / a), avgFear = (int)(f / a);
        switch (e) {
            case 0:  return 1 + avgHunger / 4;                                  // 蝗灾
            case 1:  return 1 + pop / 15 + (d.tick < d.chainPlague ? 30 : 0);    // 疫病（连锁）
            case 2:  return 3;                                                  // 边患
            case 3:  return 1 + (300 - d.prestige) / 12 + avgFear / 3
                      + (d.tick < d.chainMobilize ? 60 : 0);            // 民变（灾后连锁）
            case 4:  return 1 + d.treasuryGold / 100;                           // 贪墨
            case 5:  return 3;                                                  // 寒潮
            case 6:  return 1 + CountPoor(d) / 5;                               // 匪患
            case 7:  return 1 + Math.Max(0, 40 - avgHunger) / 4;                // 丰年
            case 8:  return 1 + d.treasuryGold / 60;                            // 商队
            case 9:  return 1 + d.prestige / 100;                               // 乡绅
            case 10: return 1 + Math.Max(0, 50 - pop) / 10;                     // 人口回流
            case 11: return 1 + d.treasuryGold / 80;                            // 名医
            case 12: return 1 + d.prestige / 120 + avgFear / 3;                 // 大赦
            default: return 1 + Math.Max(0, 15 - avgFear) / 3 + d.granaryFood / 600;// 新生儿潮（降权到 1/3）
        }
    }

    // ★ 对口法律：(lawId, 效果%)。多条同时装 → 相乘。>100 = 反而加重。
    static readonly (int lawId, int pct)[][] LAW_MODS = {
        new[]{ (5, 50), (71, 50) },       //  0 蝗灾   ← 耕作 / 开垦
        new[]{ (12, 50), (13, 40) },      //  1 疫病   ← 医药 / 公共卫生
        new[]{ (15, 0), (48, 150) },      //  2 边患   ← 兵役征召（全免）/ 威权（加重）
        new[]{ (6, 50), (1, 70), (16, 150) }, //  3 民变 ← 安抚/济贫 减轻，连坐 加重
        new[]{ (14, 50) },                //  4 贪墨   ← 盐铁专卖
        new[]{ (30, 50) },                //  5 寒潮   ← 屯粮
        new[]{ (10, 40), (25, 0), (2, 150) }, //  6 匪患 ← 宵禁/雇佣护卫 减轻，饥则抢 加重
        new[]{ (5, 150) },                //  7 丰年   ← 耕作（收益 ×1.5）
        new[]{ (29, 150), (31, 150), (14, 60) }, //  8 商队 ← 平粜/售粮 增益，盐铁专卖 压低
        new[]{ (28, 150) },               //  9 乡绅   ← 捐纳
        new[]{ (17, 150) },               // 10 人口回流 ← 流民法
        new[]{ (12, 0) },                 // 11 名医   ← 医药（免费）
        new[]{ (35, 150), (6, 130) },     // 12 大赦   ← 祭祀 / 安抚
        new[]{ (8, 150), (9, 130) },      // 13 新生儿潮 ← 婚育补贴 / 幼童口粮
    };

    /// <summary>装了哪些对口法律 → 累乘出的百分比（100 = 没装）</summary>
    public static int LawScale(VillageSaveData d, int e) {
        if (Cfg.LawModOn <= 0) return 100;
        int pct = 100;
        foreach (var (lawId, p) in LAW_MODS[e])
            foreach (var id in d.lawIds) if (id == lawId) { pct = pct * p / 100; break; }
        return pct;
    }

    static int CountPoor(VillageSaveData d) {
        int n = 0;
        for (int i = 0; i < d.villagerAlive.Length; i++)
            if (d.villagerAlive[i] && d.villagerGold[i] < 10) n++;
        return n;
    }

    /// <summary>村里有某职业（烙记）的人数</summary>
    static int Marked(VillageSaveData d, uint bit) {
        int n = 0;
        for (int i = 0; i < d.villagerAlive.Length; i++)
            if (d.villagerAlive[i] && Traits.HasMark(d, i, bit)) n++;
        return n;
    }

    /// <summary>职业修正：有「耕者」= 勤勉词条的人，效果 ×50%</summary>
    static bool HasHelper(VillageSaveData d, int e) {
        if (Cfg.ForceHelper > 0) return true;
        switch (e) {
            case 0: case 7:  return MarkedInnate(d, 16u) > 0;   // 蝗灾/丰年 ← 耕者（勤勉）
            case 1: case 11: return Marked(d, 32768u) > 0;      // 疫病/名医 ← 医师（巫祝位暂代）
            case 2: case 6:  return MarkedInnate(d, 2u) > 0;    // 边患/匪患 ← 游侠（果决）
            case 3: case 9:  return Marked(d, 16384u) > 0;      // 民变/乡绅 ← 里正（恩主）
            case 4:          return MarkedInnate(d, 1024u) > 0; // 贪墨 ← 账房（节俭）
            case 5:          return Marked(d, 1024u) > 0;       // 寒潮 ← 役夫（劳损）
            case 8:          return Marked(d, 64u) > 0;         // 商队 ← 粮商（债户位暂代）
            case 10:         return Marked(d, 128u) > 0;        // 回流 ← 徙民
            case 12:         return Marked(d, 32768u) > 0;      // 大赦 ← 巫祝
            default:         return Marked(d, 8u) > 0;          // 新生儿潮 ← 稳婆
        }
    }
    static int MarkedInnate(VillageSaveData d, uint bit) {
        int n = 0;
        for (int i = 0; i < d.villagerAlive.Length; i++)
            if (d.villagerAlive[i] && Traits.HasInnate(d, i, bit)) n++;
        return n;
    }

    static int Neg(int v) { return v * Cfg.EventNegScale / 100; }

    /// <summary>事件抢走每户的私粮（匪患 / 民变）。</summary>
    static void StealFood(VillageSaveData d, int pct, int hpMul, int lawPct)
    {
        if (Cfg.EventFoodHit <= 0) return;
        int p = pct * hpMul / 100 * lawPct / 100;
        if (p <= 0) return;
        for (int i = 0; i < d.villagerAlive.Length; i++)
            if (d.villagerAlive[i]) d.villagerFood[i] -= d.villagerFood[i] * p / 100;
    }

    /// <summary>事件损耗每户的私粮（蝗灾 / 寒潮：虫吃、冻坏）。</summary>
    static void SpoilFood(VillageSaveData d, int pct, int hpMul, int lawPct)
    {
        if (Cfg.EventFoodHit <= 0) return;
        int p = pct * hpMul / 100 * lawPct / 100;
        if (p <= 0) return;
        for (int i = 0; i < d.villagerAlive.Length; i++)
            if (d.villagerAlive[i]) d.villagerFood[i] -= d.villagerFood[i] * p / 100;
    }

    /// <summary>事件往每户补私粮（丰年：收成进了家门）。</summary>
    static void FillFood(VillageSaveData d, int n, int lawPct)
    {
        if (Cfg.EventFoodHit <= 0) return;
        int add = n * lawPct / 100;
        if (add <= 0) return;
        for (int i = 0; i < d.villagerAlive.Length; i++)
            if (d.villagerAlive[i]) d.villagerFood[i] = Math.Min(Cfg.FoodCapAdult, d.villagerFood[i] + add);
    }

    static int _ScaleKills(int baseKills, int hpMul, int lawPct) {
        int v = baseKills * hpMul / 100 * lawPct / 100;
        return v < 0 ? 0 : v;
    }

    static void AllFear(VillageSaveData d, int delta) {
        for (int i = 0; i < d.villagerAlive.Length; i++)
            if (d.villagerAlive[i])
                d.villagerFear[i] = Math.Max(0, Math.Min(99, d.villagerFear[i] + delta));
    }

    public static void Fire(VillageSaveData d) {
        int total = 0;
        var w = new int[Count];
        for (int e = 0; e < Count; e++) { w[e] = Math.Max(1, Weight(d, e)); total += w[e]; }
        int roll = (int)(Rng.For(d.rngSeed, d.tick, 61, 0) % (uint)total);
        int acc = 0, pick = 0;
        for (int e = 0; e < Count; e++) { acc += w[e]; if (roll < acc) { pick = e; break; } }
        if (Cfg.ForceEvent >= 0) pick = Cfg.ForceEvent;

        Cfg.EventFired[pick]++;
        Cfg.EventScaleLog[pick] += LawScale(d, pick);
        bool hp = HasHelper(d, pick);                 // 有对应职业 → 效果减半
        int lawPct = LawScale(d, pick);               // ★ 对口法律
        if (!hp && lawPct == 100) { }                 // 两个都不生效
        int tick = d.tick;
        int _g0 = d.granaryFood; int _d0 = d.totalDeaths;
        // 职业和法律的折扣叠加：先职业（减半），再法律
        int hpMul = hp ? 50 : 100;

        switch (pick) {
            case 0: d.granaryFood = Math.Max(0, d.granaryFood - d.granaryFood * Neg(60) / 100 * hpMul / 100 * lawPct / 100);
                    SpoilFood(d, Cfg.ColdSpoilPct, hpMul, lawPct);
                    d.chainMobilize = tick + 2 * 2920; break;                  // 蝗灾 → 民变窗口
            case 1: { int n = Neg(5) * hpMul / 100 * lawPct / 100;
                      VillageLifecycleService.EventKillYoungest(d, n);
                      AllFear(d, Neg(30) * hpMul / 100 * lawPct / 100);
                      d.chainPlague = tick + 2920; break; }                              // 疫病 → 二次爆发
            case 2: if (d.treasuryGold >= 150) d.treasuryGold = Math.Max(0, d.treasuryGold - Neg(250) * lawPct / 100);
                    else { VillageLifecycleService.EventKillAny(d, _ScaleKills(Neg(4), hpMul, lawPct), tick, "边患"); AllFear(d, Neg(25) * hpMul / 100 * lawPct / 100); }
                    break;                                                                 // 边患
            case 3: VillageLifecycleService.EventKillAny(d, _ScaleKills(Neg(5), hpMul, lawPct), tick, "民变");
                    StealFood(d, Cfg.RiotStealPct, hpMul, lawPct);
                    d.prestige = Math.Max(0, d.prestige - 30); break;                      // 民变
            case 4: d.treasuryGold = Math.Max(0, d.treasuryGold - Neg(250) * hpMul / 100 * lawPct / 100);
                    d.prestige = Math.Max(0, d.prestige - 25); break;                      // 贪墨
            case 5: d.granaryFood = Math.Max(0, d.granaryFood - d.granaryFood * Neg(50) / 100 * lawPct / 100);
                    SpoilFood(d, Cfg.ColdSpoilPct, hpMul, lawPct);
                    d.chainMobilize = tick + 2 * 2920; break;                  // 寒潮 → 民变窗口
            case 6: StealFood(d, Cfg.BanditStealPct, hpMul, lawPct);
                    AllFear(d, Neg(20) * hpMul / 100 * lawPct / 100); break;                                        // 匪患
            case 7: d.granaryFood = Math.Min(d.granaryCapacity, d.granaryFood + (d.granaryCapacity - d.granaryFood) * 35 / 100 * lawPct / 100);
                    FillFood(d, Cfg.HarvestFill, lawPct); break; // 丰年
            case 8: d.treasuryGold = Math.Max(0, d.treasuryGold - 50);
                    d.granaryFood = Math.Min(d.granaryCapacity, d.granaryFood + (d.granaryCapacity - d.granaryFood) * 30 / 100 * lawPct / 100); break; // 商队
            case 9: d.prestige = Math.Max(0, d.prestige - (hp ? 15 : 30));
                    d.granaryFood = Math.Min(d.granaryCapacity, d.granaryFood + (d.granaryCapacity - d.granaryFood) * 25 / 100 * lawPct / 100); break; // 乡绅
            case 10: VillageLifecycleService.AppendImmigrants(d, 4 * lawPct / 100); break;        // 人口回流
            case 11: d.treasuryGold = Math.Max(0, d.treasuryGold - (hp ? 0 : 80)); break;   // 名医
            case 12: AllFear(d, -(hp ? 40 : 30)); d.prestige = Math.Max(0, d.prestige - 40); break; // 大赦
            default: AllFear(d, -5); break;                                                 // 新生儿潮（简化为降恐惧）
        }
        Cfg.EventKills[pick] += Math.Max(0, d.totalDeaths - _d0);
        Cfg.EventGranary[pick] += Math.Max(0, _g0 - d.granaryFood);
    }
}
