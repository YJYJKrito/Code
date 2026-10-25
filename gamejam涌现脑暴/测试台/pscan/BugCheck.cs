using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 10.18 包体三个 bug 的**修前 / 修后对照**。
/// Cfg.BugFixes = 0 → 当前包体；1 → 三个修复全开。
/// </summary>
public static class BugCheck
{
    const int YEARS = 30;

    static readonly (string nm, int[] ids)[] SETS = {
        ("只装幼童口粮(9)", new[]{9}),
        ("生育系(8,9,12)",  new[]{8, 9, 12}),
        ("生育系+童工",     new[]{8, 9, 12, 39}),
        ("默认三法",        new[]{1, 5, 3}),
        ("苛政",            new[]{54, 39, 3, 7, 2, 16}),
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

    static (int med, int dead, int bitter, int prestigeMin) One(int seeds, int[] ids)
    {
        var fp = new List<int>(); int dead = 0, bitter = 0, pmin = 999;
        for (int s = 1; s <= seeds; s++)
        {
            var d = NewGame(s, ids);
            for (int y = 0; y < YEARS; y++)
            {
                var rep = VillageLifecycleService.Advance(d, 2920);
                if (d.prestige < pmin) pmin = d.prestige;
                if (VillageLifecycleService.CountAlive(d) == 0) break;
            }
            int f = VillageLifecycleService.CountAlive(d);
            fp.Add(f); if (f == 0) dead++;
            bool hasBitter = false;
            for (int i = 0; i < d.villagerAlive.Length; i++)
                if (d.villagerAlive[i] && (d.villagerMarks[i] & 2048u) != 0) hasBitter = true;
            if (hasBitter) bitter++;
        }
        fp.Sort();
        return (fp[fp.Count / 2], dead, bitter, pmin);
    }

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 10.18 包体 · 三个 bug 的修前/修后（{seeds} 种子 × 30 年）\n");
        sb.AppendLine("`Cfg.BugFixes = 0` = 当前包体 · `1` = 三个修复全开\n");
        sb.AppendLine("| 玩法 | 修前 末人口/灭村 | **修后 末人口/灭村** |");
        sb.AppendLine("|---|---|---|");

        var before = new List<(string, int, int)>();
        var after  = new List<(string, int, int)>();

        Cfg.BugFixes = 0;
        foreach (var (nm, ids) in SETS) { var r = One(seeds, ids); before.Add((nm, r.med, r.dead)); }
        Cfg.BugFixes = 1;
        foreach (var (nm, ids) in SETS) { var r = One(seeds, ids); after.Add((nm, r.med, r.dead)); }

        for (int i = 0; i < SETS.Length; i++)
            sb.AppendLine($"| {SETS[i].nm} | {before[i].Item2} / {before[i].Item3 * 100.0 / seeds:F0}% "
                        + $"| **{after[i].Item2} / {after[i].Item3 * 100.0 / seeds:F0}%** |");

        // 积怨 / 威信
        Cfg.BugFixes = 0; var b0 = One(seeds, new[]{1, 5, 3});
        Cfg.BugFixes = 1; var b1 = One(seeds, new[]{1, 5, 3});
        sb.AppendLine($"\n| 指标（默认三法） | 修前 | **修后** |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine($"| 积怨出现率 | {b0.bitter * 100.0 / seeds:F0}% | **{b1.bitter * 100.0 / seeds:F0}%** |");
        sb.AppendLine($"| 威信最小值 | {b0.prestigeMin} | **{b1.prestigeMin}** |");

        Cfg.BugFixes = 0;
        File.WriteAllText("scan25-bugcheck.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan25-bugcheck.txt");
    }
}
