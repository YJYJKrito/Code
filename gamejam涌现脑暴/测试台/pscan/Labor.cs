using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 测「劳动力」：产出 = 劳动力 × 每人产出，劳动力随**年龄**和**饥饿**下降。
///
/// 要回答的问题：
///   ① 每人产出要定多少，村子才不会被这一条直接拖死
///   ② 饥饿折产出的强度要定多少，饥荒螺旋才成立但不失控
///   ③ 哪个玩法最敏感
/// </summary>
public static class Labor
{
    const int YEARS = 30;

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

    struct Sample { public int fin, low; public long hungerSum; public int samples; }

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
                long h = 0; int n = 0;
                for (int i = 0; i < d.villagerAlive.Length; i++)
                    if (d.villagerAlive[i]) { h += d.villagerHunger[i]; n++; }
                if (n > 0) { r.hungerSum += h / n; r.samples++; }
                if (a == 0) break;
            }
            if (VillageLifecycleService.CountAlive(d) == 0) break;
        }
        r.fin = VillageLifecycleService.CountAlive(d);
        return r;
    }

    static (List<int> fin, List<int> low, List<long> hung) Collect(int seeds)
    {
        var fin = new List<int>(); var low = new List<int>(); var hung = new List<long>();
        for (int gi = 0; gi < SETS.Length; gi++)
            for (int s = 1; s <= seeds; s++)
            {
                var r = One(s, SETS[gi].ids);
                fin.Add(r.fin); low.Add(r.low);
                hung.Add(r.samples > 0 ? r.hungerSum / r.samples : 0);
            }
        fin.Sort(); low.Sort(); hung.Sort();
        return (fin, low, hung);
    }

    static string Row(string label, List<int> bFin, List<long> bHung,
                      int perWorker, int hungerScale, int seeds)
    {
        Cfg.LaborMode = 1; Cfg.LabPerWorker = perWorker; Cfg.LabHungerScale = hungerScale;
        Cfg.LabOldW = 50; Cfg.LabStarveFloor = 10;
        var c = Collect(seeds);
        int dead = 0; foreach (var v in c.fin) if (v == 0) dead++;
        double cap = dead * 100.0 / c.fin.Count;
        string verdict = cap <= 5 ? "✅" : (cap <= 15 ? "⚠ 偏高" : "❌ 灭村");
        return $"| {label} | {c.fin[0]}/{c.fin[c.fin.Count/2]}/{c.fin[c.fin.Count-1]} | "
             + $"{c.hung[c.hung.Count/2]} | {cap:F0}% | {verdict} |";
    }

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 劳动力 · 实测（{seeds} 种子 × 30 年 × {SETS.Length} 玩法）\n");
        sb.AppendLine("产出从「人口 × 4」改成「劳动力 × 每人产出」，劳动力按年龄和饥饿折算。\n");

        Cfg.LaborMode = 0;
        var bl = Collect(seeds);
        int bdead = 0; foreach (var v in bl.fin) if (v == 0) bdead++;
        sb.AppendLine("**原版基线（人口即劳力）：**"
                    + $" 末人口 {bl.fin[0]}/{bl.fin[bl.fin.Count/2]}/{bl.fin[bl.fin.Count-1]} · "
                    + $"平均饥饿 {bl.hung[bl.hung.Count/2]} · 灭村率 {bdead*100.0/bl.fin.Count:F0}%\n");

        sb.AppendLine("## 一、每人产出定多少（饥饿满强度）\n");
        sb.AppendLine("| 每人产出 | 末人口 最小/中位/最大 | 平均饥饿 | 灭村率 | 判定 |");
        sb.AppendLine("|---|---|---|---|---|");
        sb.AppendLine(Row("4（= 原版）", bl.fin, bl.hung, 4, 100, seeds));
        sb.AppendLine(Row("5", bl.fin, bl.hung, 5, 100, seeds));
        sb.AppendLine(Row("6", bl.fin, bl.hung, 6, 100, seeds));
        sb.AppendLine(Row("7", bl.fin, bl.hung, 7, 100, seeds));
        sb.AppendLine(Row("8", bl.fin, bl.hung, 8, 100, seeds));

        sb.AppendLine("\n## 二、饥饿折产出的强度（每人产出取 6 / 7）\n");
        sb.AppendLine("| 每人产出 · 饥饿强度 | 末人口 最小/中位/最大 | 平均饥饿 | 灭村率 | 判定 |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (int per in new[]{ 6, 7 })
            foreach (int sc in new[]{ 100, 50, 25 })
                sb.AppendLine(Row($"{per} · {sc}%", bl.fin, bl.hung, per, sc, seeds));

        // 选定组合：每人产出 7、饥饿强度 50%
        int bestPer = 5, bestSc = 50;
        Cfg.LaborMode = 1; Cfg.LabPerWorker = bestPer; Cfg.LabHungerScale = bestSc;
        Cfg.LabOldW = 50; Cfg.LabStarveFloor = 10;

        sb.AppendLine($"\n## 三、按玩法拆开（每人产出 {bestPer} · 饥饿强度 {bestSc}%）\n");
        sb.AppendLine("| 玩法 | 原版 末人口 | 平均饥饿 | **劳动力** 末人口 | 平均饥饿 | 灭村率 |");
        sb.AppendLine("|---|---|---|---|---|---|");

        Cfg.LaborMode = 0;
        var baseRows = new List<string>();
        for (int gi = 0; gi < SETS.Length; gi++)
        {
            var fp = new List<int>(); var hu = new List<long>();
            for (int s = 1; s <= seeds; s++) { var r = One(s, SETS[gi].ids); fp.Add(r.fin); hu.Add(r.samples > 0 ? r.hungerSum / r.samples : 0); }
            fp.Sort(); hu.Sort();
            baseRows.Add($"{fp[0]}/{fp[fp.Count/2]}/{fp[fp.Count-1]}|{hu[hu.Count/2]}");
        }

        Cfg.LaborMode = 1;
        for (int gi = 0; gi < SETS.Length; gi++)
        {
            var fp = new List<int>(); var hu = new List<long>(); int dead = 0;
            for (int s = 1; s <= seeds; s++)
            {
                var r = One(s, SETS[gi].ids);
                fp.Add(r.fin); hu.Add(r.samples > 0 ? r.hungerSum / r.samples : 0);
                if (r.fin == 0) dead++;
            }
            fp.Sort(); hu.Sort();
            var b = baseRows[gi].Split('|');
            sb.AppendLine($"| {SETS[gi].nm} | {b[0]} | {b[1]} | **{fp[0]}/{fp[fp.Count/2]}/{fp[fp.Count-1]}** | {hu[hu.Count/2]} | {dead*100.0/seeds:F0}% |");
        }

        Cfg.LaborMode = 0;
        Cfg.LabPerWorker = 4; Cfg.LabHungerScale = 100;
        Cfg.LabOldW = 50; Cfg.LabStarveFloor = 10;

        File.WriteAllText("scan18-labor.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan18-labor.txt");
    }
}
