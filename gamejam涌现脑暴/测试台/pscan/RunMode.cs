using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 测构建里的两种模式（RunRules）：
///   runMode = 0  五十年（tick >= 146104 结束）
///   runMode = 1  高压（村力 ≥45 维持 240 个月 / 掉到 20 以下 6 个月判负 / 同样 50 年上限）
/// </summary>
public static class RunMode
{
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
    };

    static VillageSaveData NewGame(int seed, int[] ids, int mode)
    {
        var d = Bootstrap.CreateNewGame(0, seed * 7919 + 13);
        for (int k = 0; k < d.lawIds.Length; k++) { d.lawIds[k] = 0; d.lawNames[k] = null; }
        for (int k = 0; k < ids.Length && k < d.lawIds.Length; k++) {
            d.lawIds[k] = ids[k];
            d.lawNames[k] = LawCatalog.FindById(ids[k])?.name;
        }
        d.runMode = mode;
        return d;
    }

    struct R { public int endKind, grade, clearTick, fin, personYears, score; public bool tinyEnd; }

    static R One(int seed, int[] ids, int mode)
    {
        var d = NewGame(seed, ids, mode);
        // 50 年 = 146,104 tick → 609 个月；多跑几个月的余量
        for (int m = 0; m < 620; m++)
        {
            VillageLifecycleService.Advance(d, 240);
            if (d.endKind != 0) break;
        }
        return new R {
            endKind = d.endKind, grade = d.grade,
            clearTick = d.clearMonth,
            fin = VillageLifecycleService.CountAlive(d),
            personYears = d.personMonths / 12,
            score = RunRules.TotalScore(d),
            tinyEnd = (d.endKind == 4 && d.tinyStreak >= 24)
        };
    }

    static string KindName(int k)
    {
        switch (k) { case 1: return "通关"; case 2: return "绝户"; case 3: return "罢黜"; case 4: return "期满/名存"; default: return "未结束"; }
    }

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 两种模式 · 构建实测（{seeds} 种子 × {SETS.Length} 玩法）\n");
        sb.AppendLine("> runMode 0 = 五十年；runMode 1 = 高压。两者都有 50 年上限（tick 146104）。\n");

        foreach (int mode in new[]{ 0, 1 })
        {
            sb.AppendLine($"## runMode = {mode}（{(mode == 0 ? "五十年" : "高压")}）\n");
            sb.AppendLine("| 玩法 | 结束 | 等级 | 用时（年） | 人年 | 总评 |");
            sb.AppendLine("|---|---|---|---|---|---|");

            var kinds = new int[5]; int runs = 0, tiny = 0, complete = 0;

            foreach (var (nm, ids) in SETS)
            {
                var ks = new int[5]; var years = new List<int>();
                int gy = 0, py = 0, sc = 0;
                for (int s = 1; s <= seeds; s++)
                {
                    var r = One(s, ids, mode);
                    if (r.endKind >= 0 && r.endKind < 5) ks[r.endKind]++;
                    if (r.endKind == 4) { if (r.tinyEnd) tiny++; else complete++; }
                    kinds[r.endKind]++;
                    gy += r.grade; py += r.personYears; sc += r.score;
                    if (r.clearTick > 0) years.Add(r.clearTick / 2920 + 1);
                    runs++;
                }
                int major = 0; for (int i = 1; i < 5; i++) if (ks[i] > ks[major]) major = i;
                years.Sort();
                string cy = years.Count > 0 ? Med(years).ToString() : "—";
                sb.AppendLine($"| {nm} | {KindName(major)} ({ks[major]}/{seeds}) | {gy / seeds} | {cy} | {py / seeds} | {sc / seeds} |");
            }

            if (mode == 0) sb.AppendLine($"\n**endKind=4 里：期满 {complete} 局 · 名存实亡 {tiny} 局**");
            sb.AppendLine("\n**结束方式分布：**");
            for (int i = 1; i < 5; i++) if (kinds[i] > 0) sb.AppendLine($"- {KindName(i)}：{kinds[i]} 局 / {runs}");
            sb.AppendLine();
        }

        File.WriteAllText("scan35-runmode.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan35-runmode.txt");
    }

    static int Med(List<int> l) { l.Sort(); return l[l.Count / 2]; }

    /// <summary>单元测试：名存实亡应该得到 endKind = 5（不是 4）。</summary>
    public static void TestTiny(int dummy)
    {
        var d = Bootstrap.CreateNewGame(0, 777);
        d.runMode = 0;
        int keep = 0;
        for (int i = 0; i < d.villagerAlive.Length; i++)
        {
            if (!d.villagerAlive[i]) continue;
            keep++;
            if (keep > 3) d.villagerAlive[i] = false;
        }
        d.tinyStreak = 23;
        VillageLifecycleService.Advance(d, 240);
        VillageLifecycleService.Advance(d, 240);
        Console.WriteLine($"人口={VillageLifecycleService.CountAlive(d)} tinyStreak={d.tinyStreak} endKind={d.endKind}");
        Console.WriteLine(d.endKind == 5 ? "✅ 名存实亡 = endKind 5" : "❌ 还是 " + d.endKind);
    }
}
