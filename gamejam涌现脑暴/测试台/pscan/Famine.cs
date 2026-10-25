using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 调「饥荒咬不咬人」。
///
/// 判定不能只看平均饥饿 —— 要看**事件级的指标**：
///   ① 有多少局真的经历过饥荒（连续/断点）
///   ② 每局饿死多少人（讣告里的「饥饿」）
///   ③ 人口波动有多大（大起大落 = 咬人）
///   ④ 灭村率（不能为了咬人把村子弄死）
/// </summary>
public static class Famine
{
    const int YEARS = 30;
    const int FAMINE_HUNGER = 40;   // 全村平均饥饿 ≥ 40 记为一个「饥荒月」

    static readonly (string nm, int[] ids)[] SETS = {
        ("不装法",   new int[0]),
        ("默认三法", new[]{1, 5, 3}),
        ("开垦系",   new[]{71, 5, 12}),
        ("苛政",     new[]{54, 39, 3, 7, 2, 16}),
        ("劳役系",   new[]{1, 5, 3, 11, 39}),
        ("兵役系",   new[]{1, 5, 3, 15, 10}),
        ("恐惧系",   new[]{1, 5, 3, 4, 7, 16, 60}),
        ("祭祀系",   new[]{1, 5, 3, 35, 6, 63}),
        ("满槽6条",  new[]{1, 5, 3, 9, 12, 71}),
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

    struct Sample
    {
        public int fin, low, famineMonths, starved, maxHunger;
        public long hungerSum; public int samples;
    }

    static Sample One(int seed, int[] ids)
    {
        var d = NewGame(seed, ids);
        var r = new Sample();
        r.low = 999;
        for (int y = 0; y < YEARS; y++)
        {
            for (int m = 0; m < 12; m++)
            {
                VillageLifecycleService.Advance(d, 240);
                int a = VillageLifecycleService.CountAlive(d);
                if (a < r.low) r.low = a;
                long h = 0; int n = 0, mx = 0;
                for (int i = 0; i < d.villagerAlive.Length; i++)
                    if (d.villagerAlive[i]) { h += d.villagerHunger[i]; n++; if (d.villagerHunger[i] > mx) mx = d.villagerHunger[i]; }
                if (n > 0)
                {
                    int avg = (int)(h / n);
                    r.hungerSum += avg; r.samples++;
                    if (avg >= FAMINE_HUNGER) r.famineMonths++;
                    if (mx > r.maxHunger) r.maxHunger = mx;
                }
                if (a == 0) break;
            }
            if (VillageLifecycleService.CountAlive(d) == 0) break;
        }
        r.fin = VillageLifecycleService.CountAlive(d);
        for (int i = 0; i < d.obituaryCount && i < d.obituaryCauses.Length; i++)
            if (d.obituaryCauses[i] == "饥饿") r.starved++;
        return r;
    }

    struct Agg
    {
        public int runs, dead, runsWithFamine;
        public int finMed, hungerMed, starveMed, famineMonthsAvg;
        public int popIqr;
    }

    static Agg Measure(int seeds)
    {
        var a = new Agg();
        var fin = new List<int>(); var hung = new List<long>();
        var starve = new List<int>(); var fam = new List<int>();
        for (int gi = 0; gi < SETS.Length; gi++)
            for (int s = 1; s <= seeds; s++)
            {
                var r = One(s, SETS[gi].ids);
                a.runs++;
                if (r.fin == 0) a.dead++;
                if (r.famineMonths > 0) a.runsWithFamine++;
                fin.Add(r.fin); starve.Add(r.starved); fam.Add(r.famineMonths);
                hung.Add(r.samples > 0 ? r.hungerSum / r.samples : 0);
            }
        fin.Sort(); hung.Sort(); starve.Sort(); fam.Sort();
        a.finMed = fin[fin.Count / 2];
        a.hungerMed = (int)hung[hung.Count / 2];
        a.starveMed = starve[starve.Count / 2];
        a.famineMonthsAvg = fam[fam.Count / 2];
        a.popIqr = fin[fin.Count * 3 / 4] - fin[fin.Count / 4];
        return a;
    }

    static string Row(string label, Agg a)
    {
        double cap = a.dead * 100.0 / Math.Max(1, a.runs);
        string verdict = cap > 5 ? "❌ 灭村" : (a.runsWithFamine * 100.0 / Math.Max(1,a.runs) < 20 ? "⚠ 不够咬" : "✅");
        return $"| {label} | {a.finMed} | {a.hungerMed} | {a.runsWithFamine * 100.0 / Math.Max(1, a.runs):F0}% "
             + $"| {a.famineMonthsAvg} | {a.starveMed} | {a.popIqr} | {cap:F0}% | {verdict} |";
    }

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 饥荒强度 · 参数扫描（{seeds} 种子 × 30 年 × {SETS.Length} 玩法）\n");
        sb.AppendLine($"「饥荒月」= 全村平均饥饿 ≥ {FAMINE_HUNGER} 的月份。\n");
        sb.AppendLine("目标：**饥荒真的发生、饿得死人、人口大起大落**，但**不灭村**。\n");

        sb.AppendLine("## 一、原版基线\n");
        Cfg.LaborMode = 0;
        var bl = Measure(seeds);
        sb.AppendLine("| 配置 | 末人口中位 | 平均饥饿 | 经历过饥荒的局 | 饥荒月数中位 | 饿死人数中位 | 人口 IQR | 灭村率 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        sb.AppendLine(Row("原版（人口即劳力）", bl));

        sb.AppendLine("\n## 二、劳动力：每人产出 × 饥饿强度\n");
        sb.AppendLine("| 配置 | 末人口中位 | 平均饥饿 | 经历过饥荒的局 | 饥荒月数中位 | 饿死人数中位 | 人口 IQR | 灭村率 | 判定 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (int per in new[]{ 4, 5, 6, 7 })
            foreach (int sc in new[]{ 50, 75, 100 })
            {
                Cfg.LaborMode = 1; Cfg.LabPerWorker = per; Cfg.LabHungerScale = sc;
                Cfg.LabOldW = 50; Cfg.LabStarveFloor = 10;
                sb.AppendLine(Row($"{per} × {sc}%", Measure(seeds)));
            }

        sb.AppendLine("\n## 三、老人和饿鬼的权重（每人 6 · 饥饿 100%）\n");
        sb.AppendLine("| 配置 | 末人口中位 | 平均饥饿 | 经历过饥荒的局 | 饥荒月数中位 | 饿死人数中位 | 人口 IQR | 灭村率 | 判定 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var (ow, fl) in new[]{ (50, 10), (50, 0), (30, 0), (20, 0) })
        {
            Cfg.LaborMode = 1; Cfg.LabPerWorker = 6; Cfg.LabHungerScale = 100;
            Cfg.LabOldW = ow; Cfg.LabStarveFloor = fl;
            sb.AppendLine(Row($"老人 {ow}% · 兜底 {fl}%", Measure(seeds)));
        }

        Cfg.LaborMode = 0; Cfg.LabPerWorker = 7; Cfg.LabHungerScale = 50;
        Cfg.LabOldW = 50; Cfg.LabStarveFloor = 10;

        File.WriteAllText("scan19-famine.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan19-famine.txt");
    }

    /// <summary>40 种子确认：原版 vs 选定的「5 × 50%」。</summary>
    public static void Confirm(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 饥荒强度 · 定稿确认（{seeds} 种子 × 30 年 × {SETS.Length} 玩法）\n");
        sb.AppendLine($"「饥荒月」= 全村平均饥饿 ≥ {FAMINE_HUNGER} 的月份。\n");
        sb.AppendLine("| 配置 | 末人口中位 | 平均饥饿 | 经历过饥荒的局 | 饥荒月数中位 | 饿死人数中位 | 人口 IQR | 灭村率 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");

        Cfg.LaborMode = 0;
        var bl = Measure(seeds);
        sb.AppendLine($"| 原版 | {bl.finMed} | {bl.hungerMed} | {bl.runsWithFamine*100.0/bl.runs:F0}% | {bl.famineMonthsAvg} | {bl.starveMed} | {bl.popIqr} | {bl.dead*100.0/bl.runs:F0}% |");

        Cfg.LaborMode = 1; Cfg.LabPerWorker = 5; Cfg.LabHungerScale = 50;
        Cfg.LabOldW = 50; Cfg.LabStarveFloor = 10;
        var f = Measure(seeds);
        sb.AppendLine($"| **劳动力 5 × 50%** | **{f.finMed}** | **{f.hungerMed}** | **{f.runsWithFamine*100.0/f.runs:F0}%** | **{f.famineMonthsAvg}** | **{f.starveMed}** | **{f.popIqr}** | **{f.dead*100.0/f.runs:F0}%** |");

        Cfg.LaborMode = 1; Cfg.LabPerWorker = 6; Cfg.LabHungerScale = 50;
        var g = Measure(seeds);
        sb.AppendLine($"| 劳动力 6 × 50%（对照） | {g.finMed} | {g.hungerMed} | {g.runsWithFamine*100.0/g.runs:F0}% | {g.famineMonthsAvg} | {g.starveMed} | {g.popIqr} | {g.dead*100.0/g.runs:F0}% |");

        Cfg.LaborMode = 0; Cfg.LabPerWorker = 7; Cfg.LabHungerScale = 50;
        Cfg.LabOldW = 50; Cfg.LabStarveFloor = 10;
        File.WriteAllText("scan20-famineconfirm.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan20-famineconfirm.txt");
    }
}
