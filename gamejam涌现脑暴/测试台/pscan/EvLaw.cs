using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 对口法律（事件 × 法条）的隔离测试。
///
/// 用 Cfg.ForceEvent 把同一个事件反复触发，只改「装不装对口法」。
/// 差值完全来自律法改写，不含法条自身的其他效果。
/// </summary>
public static class EvLaw
{
    const int YEARS = 20;

    static readonly (string ev, int id, int law, string lawName, int baseLaw)[] CASES = {
        ("蝗灾",  0, 5,  "耕作",   0),
        ("疫病",  1, 12, "医药",   0),
        ("边患",  2, 15, "兵役征召", 0),
        ("民变",  3, 6,  "安抚",   0),
        ("寒潮",  5, 30, "屯粮",   0),
        ("匪患",  6, 10, "宵禁",   0),
    };

    static VillageSaveData NewGame(int seed, int[] ids)
    {
        var d = Bootstrap.CreateNewGame(0, seed * 7919 + 13);
        for (int k = 0; k < d.lawIds.Length; k++) { d.lawIds[k] = 0; d.lawNames[k] = null; }
        for (int k = 0; k < ids.Length && k < d.lawIds.Length; k++) {
            d.lawIds[k] = ids[k];
            d.lawNames[k] = LawCatalog.FindById(ids[k])?.name;
        }
        return d;
    }

    struct R { public int fin, deaths, granaryAvg, firings, damage; }

    static R Force(int ev, int[] ids, int seeds)
    {
        Cfg.ForceEvent = ev;
        Cfg.NativeEventChance = 45;
        long g = 0; int n = 0, fin = 0, deaths = 0, firings = 0, damage = 0;
        for (int s = 1; s <= seeds; s++)
        {
            for (int e = 0; e < 14; e++) { Cfg.EventFired[e] = 0; Cfg.EventGranary[e] = 0; }
            var d = NewGame(s, ids);
            for (int y = 0; y < YEARS; y++)
            {
                for (int m = 0; m < 12; m++)
                {
                    VillageLifecycleService.Advance(d, 240);
                    g += d.granaryFood; n++;
                    if (VillageLifecycleService.CountAlive(d) == 0) break;
                }
                if (VillageLifecycleService.CountAlive(d) == 0) break;
            }
            fin += VillageLifecycleService.CountAlive(d);
            deaths += d.totalDeaths;
            firings += Cfg.EventFired[ev];
            damage += Cfg.EventGranary[ev];
        }
        Cfg.ForceEvent = -1;
        Cfg.NativeEventChance = 45;
        return new R { fin = fin / seeds, deaths = deaths / seeds, granaryAvg = (int)(g / Math.Max(1, n)), firings = firings / seeds, damage = damage / seeds };
    }

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 对口法律 · 隔离测试（{seeds} 种子 × {YEARS} 年 · 每 1800 tick 强制触发同一事件）\n");
        sb.AppendLine("| 事件 | 对口法 | 触发/局 | 末人口 没装/装了 | 死亡 没装/装了 | 平均粮仓 没装/装了 | 判定 |");
        sb.AppendLine("|---|---|---|---|---|---|---|");

        foreach (var (ev, id, law, lawName, _) in CASES)
        {
            var a = Force(id, new int[0], seeds);
            var b = Force(id, new[]{law}, seeds);
            bool ok;
            string why;
            if (ev == "蝗灾" || ev == "寒潮") { ok = b.damage < a.damage; why = "粮损"; }
            else { ok = b.deaths < a.deaths || b.fin > a.fin; why = "死亡/人口"; }
            sb.AppendLine($"| {ev} | {lawName}({law}) | {a.firings} | {a.fin} / **{b.fin}** "
                        + $"| {a.deaths} / **{b.deaths}** | 粮损 {a.damage} / **{b.damage}** "
                        + $"| {(ok ? "✅ " + why + "改善" : "**❌ 没改善**")} |");
        }

        File.WriteAllText("scan26-evlaw.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan26-evlaw.txt");
    }

    /// <summary>连锁 A/B：正常事件下，民变/疫病的触发次数。</summary>
    public static void Chain(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 事件连锁 · A/B（{seeds} 种子 × 30 年 × 10 玩法）\n");
        sb.AppendLine("| 连锁 | 民变 每局 | 疫病 每局 | 全部事件 每局 |");
        sb.AppendLine("|---|---|---|---|");
        foreach (int on in new[]{ 0, 1 })
        {
            Cfg.ChainOn = (on > 0) ? 1 : 0;
            Cfg.ChainMode = on;
            long a = 0, b = 0, all = 0; int runs = 0;
            var sets = new (string, int[])[]{
                ("不装法",new int[0]),("默认三法",new[]{1,5,3}),("开垦系",new[]{71,5,12}),
                ("苛政",new[]{54,39,3,7,2,16}),("生育系",new[]{8,9,12}),("劳役系",new[]{1,5,3,11,39}),
                ("兵役系",new[]{1,5,3,15,10}),("恐惧系",new[]{1,5,3,4,7,16,60}),
                ("祭祀系",new[]{1,5,3,35,6,63}),("满槽6条",new[]{1,5,3,9,12,71}) };
            foreach (var (nm, ids) in sets)
                for (int s = 1; s <= seeds; s++)
                {
                    for (int e = 0; e < 14; e++) Cfg.EventFired[e] = 0;
                    var d = NewGame(s, ids);
                    for (int y = 0; y < 30; y++)
                    {
                        VillageLifecycleService.Advance(d, 2920);
                        if (VillageLifecycleService.CountAlive(d) == 0) break;
                    }
                    a += Cfg.EventFired[3]; b += Cfg.EventFired[1];
                    for (int e = 0; e < 14; e++) all += Cfg.EventFired[e];
                    runs++;
                }
            string lbl = on == 0 ? "关" : (on == 1 ? "**权重 +60**" : "**直接排民变**");
            sb.AppendLine($"| {lbl} | {a * 1.0 / runs:F2} | {b * 1.0 / runs:F2} | {all * 1.0 / runs:F1} |");
        }
        Cfg.ChainOn = 1; Cfg.ChainMode = 1;
        File.WriteAllText("scan27-chain.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan27-chain.txt");
    }
}
