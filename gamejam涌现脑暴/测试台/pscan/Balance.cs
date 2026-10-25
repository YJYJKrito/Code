using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 两件事：
///   ① 50 年局的平衡：试几个杠杆，把灭村率压下来
///   ② 高压模式的时长：扫 T（目标线）× H（维持年数），看通关用时能不能 > 30 年（≈10 分钟）
/// </summary>
public static class Balance
{
    const int YEARS = 50;

    static readonly (string nm, int[] ids)[] SETS = {
        ("不装法",   new int[0]),
        ("默认三法", new[]{1, 5, 3}),
        ("开垦系",   new[]{71, 5, 12}),
        ("苛政",     new[]{54, 39, 3, 7, 2, 16}),
        ("生育系",   new[]{8, 9, 12}),
        ("恐惧系",   new[]{1, 5, 3, 4, 7, 16, 60}),
        ("劳役系",   new[]{1, 5, 3, 11, 39}),
        ("满槽6条",  new[]{1, 5, 3, 9, 12, 71}),
        ("流民法",   new[]{17}),
        ("医耕",     new[]{5, 12, 3}),
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

    static int V(VillageSaveData d)
    {
        long s = 0;
        for (int i = 0; i < d.villagerAlive.Length; i++)
            if (d.villagerAlive[i]) s += Math.Max(0, 100 - d.villagerHunger[i]);
        return (int)(s / 100) * 2;
    }

    struct Res { public int fin, personYears, hungerAvg; }

    static Res Sim(int seed, int[] ids, int tTarget, int holdMonths, int killV, out int clearMonth, out int failMonth)
    {
        clearMonth = 0; failMonth = 0;
        var d = NewGame(seed, ids);
        var r = new Res();
        int month = 0, streak = 0, low = 0;
        for (int y = 0; y < YEARS; y++)
        {
            for (int m = 0; m < 12; m++)
            {
                VillageLifecycleService.Advance(d, 240);
                month++;
                int pop = VillageLifecycleService.CountAlive(d);
                r.personYears += pop;
                long h = 0; int n = 0;
                for (int i = 0; i < d.villagerAlive.Length; i++)
                    if (d.villagerAlive[i]) { h += d.villagerHunger[i]; n++; }
                if (n > 0) r.hungerAvg += (int)(h / n);

                int v = V(d);
                streak = (v >= tTarget) ? streak + 1 : 0;
                low = (v < killV) ? low + 1 : 0;
                if (streak >= holdMonths) { clearMonth = month; goto done; }
                if (low >= 6) { failMonth = month; goto done; }
                if (pop == 0) { failMonth = month; goto done; }
            }
        }
        done:
        r.fin = VillageLifecycleService.CountAlive(d);
        r.personYears /= 12;
        if (month > 0) r.hungerAvg /= month;
        return r;
    }


    /// <summary>目标线每年上涨：T(year) = base + slope*y；掉线 6 个月即结束。</summary>
    static int Rising(int seed, int[] ids, int baseT, int slope10)
    {
        var d = NewGame(seed, ids);
        int month = 0, low = 0;
        for (int y = 0; y < YEARS; y++)
        {
            int t = baseT + slope10 * (y + 1) / 10;
            for (int m = 0; m < 12; m++)
            {
                VillageLifecycleService.Advance(d, 240);
                month++;
                int v = V(d);
                low = (v < t) ? low + 1 : 0;
                if (low >= 6) return month / 12 + 1;
                if (VillageLifecycleService.CountAlive(d) == 0) return month / 12 + 1;
            }
        }
        return YEARS + 1;
    }

    public static void Rising(int seeds, int dummy)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 高压模式 · 目标线逐年上涨（{seeds} 种子）\n");
        sb.AppendLine("T(y) = 起点 + 斜率 × 年；V 连续 6 个月低于 T → 结束。用时 = 撑到第几年。\n");
        sb.AppendLine("| 退化率 | 起点 | 每年涨 | 撑到（中位年） | 撑满 50 年的局 |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (int deg in new[]{ 2, 1 })
        foreach (int baseT in new[]{ 25, 30, 35 })
            foreach (int slope in new[]{ 3, 5, 8 })
            {
                Cfg.DegradePct = deg;
                var years = new List<int>(); int full = 0, runs = 0;
                foreach (var (snm, ids) in SETS)
                    for (int s = 1; s <= seeds; s++)
                    {
                        int y = Rising(s, ids, baseT, slope);
                        years.Add(y); runs++;
                        if (y > YEARS) full++;
                    }
                years.Sort();
                sb.AppendLine($"| {deg}% | {baseT} | +{slope/10.0:F1} | **{years[years.Count/2]}** | {full*100.0/runs:F0}% |");
            }
        Cfg.DegradePct = 2;
        File.WriteAllText("scan33-rising.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan33-rising.txt");
    }
    static int Med(List<int> l) { l.Sort(); return l[l.Count / 2]; }

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 平衡与高压时长（{seeds} 种子）\n");

        // ── ① 50 年平衡 ──
        sb.AppendLine("## 一、50 年局：调哪个杠杆能把灭村率压下来\n");
        sb.AppendLine("| 配置 | 灭村率 | 人年中位 | 平均饥饿 |");
        sb.AppendLine("|---|---|---|---|");
        var configs = new (string, Action)[] {
            ("现在（退化 2% · 5 粮/劳力）", () => { Cfg.DegradePct = 2; Cfg.LabPerWorker = 5; }),
            ("退化 2% → 1%",               () => { Cfg.DegradePct = 1; Cfg.LabPerWorker = 5; }),
            ("退化 2% → 0%",               () => { Cfg.DegradePct = 0; Cfg.LabPerWorker = 5; }),
            ("5 → 6 粮/劳力",              () => { Cfg.DegradePct = 2; Cfg.LabPerWorker = 6; }),
            ("退化 1% + 6 粮/劳力",         () => { Cfg.DegradePct = 1; Cfg.LabPerWorker = 6; }),
        };
        foreach (var (nm, apply) in configs)
        {
            apply();
            int dead = 0, runs = 0; var py = new List<int>(); var hu = new List<int>();
            foreach (var (snm, ids) in SETS)
                for (int s = 1; s <= seeds; s++)
                {
                    var r = Sim(s, ids, 999, 99999, -1, out _, out _);
                    if (r.fin == 0) dead++;
                    py.Add(r.personYears); hu.Add(r.hungerAvg); runs++;
                }
            sb.AppendLine($"| {nm} | **{dead * 100.0 / runs:F0}%** | {Med(py)} | {Med(hu)} |");
        }
        Cfg.DegradePct = 2; Cfg.LabPerWorker = 5;

        // ── ② 高压模式时长 ──
        sb.AppendLine("\n## 二、高压模式：T × H 的用时\n");
        sb.AppendLine("规则：V≥T 连续 H 年 → 通关，用时 = 完成维持的月份；V<20 连续 6 个月 → 失败。\n");
        sb.AppendLine("| 退化 | 目标线 T | 维持 H | 通关率 | 通关用时中位（年） | 失败用时中位 |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (int deg in new[]{ 2, 1 })
        foreach (int t in new[]{ 40, 45, 50 })
            foreach (int h in new[]{ 10, 15, 20 })
            {
                Cfg.DegradePct = deg;
                int clear = 0, runs = 0; var clearY = new List<int>(); var failY = new List<int>();
                foreach (var (snm, ids) in SETS)
                    for (int s = 1; s <= seeds; s++)
                    {
                        var r = Sim(s, ids, t, h * 12, 20, out int cm, out int fm);
                        runs++;
                        if (cm > 0) { clear++; clearY.Add(cm / 12 + 1); }
                        else failY.Add((fm > 0 ? fm : 600) / 12 + 1);
                    }
                string c = clearY.Count > 0 ? Med(clearY).ToString() : "—";
                string f = failY.Count > 0 ? Med(failY).ToString() : "—";
                sb.AppendLine($"| {deg}% | {t} | {h} 年 | {clear * 100.0 / runs:F0}% | {c} | {f} |");
            }

        Cfg.DegradePct = 2;
        File.WriteAllText("scan32-balance.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan32-balance.txt");
    }
}
