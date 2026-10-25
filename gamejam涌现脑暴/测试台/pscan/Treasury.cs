using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 税库的**进出账** + **出口**。
///
/// 先回答「钱花得掉吗」，再决定要不要加俸禄/新出口。
/// </summary>
public static class Treasury
{
    const int YEARS = 30;

    static readonly (string nm, int[] ids)[] SETS = {
        ("不装法",   new int[0]),
        ("默认三法", new[]{1, 5, 3}),
        ("平粜",     new[]{1, 5, 3, 29}),
        ("常平",     new[]{1, 5, 3, 51}),
        ("开垦系",   new[]{71, 5, 12}),
        ("苛政",     new[]{54, 39, 3, 7, 2, 16}),
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

    struct Row
    {
        public long inn, outt;
        public int final, mx, months, idleMonths;
    }

    static Row One(int seed, int[] ids)
    {
        Cfg.ResetFlow();
        var d = NewGame(seed, ids);
        var r = new Row();
        for (int y = 0; y < YEARS; y++)
        {
            for (int m = 0; m < 12; m++)
            {
                VillageLifecycleService.Advance(d, 240);
                r.months++;
                if (d.treasuryGold >= 200) r.idleMonths++;
                if (d.treasuryGold > r.mx) r.mx = d.treasuryGold;
                if (VillageLifecycleService.CountAlive(d) == 0) break;
            }
            if (VillageLifecycleService.CountAlive(d) == 0) break;
        }
        r.inn = Cfg.TreasuryIn; r.outt = Cfg.TreasuryOut;
        r.final = d.treasuryGold;
        return r;
    }

    static (long fin, long mx, double idle) Agg(int seedCount, int[] ids)
    {
        long fin = 0, mx = 0, idle = 0, months = 0;
        for (int s = 1; s <= seedCount; s++)
        {
            var r = One(s, ids);
            fin += r.final; mx += r.mx;
            idle += r.idleMonths; months += r.months;
        }
        return (fin / seedCount, mx / seedCount, idle * 100.0 / Math.Max(1, months));
    }

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 税库·进出账与出口（{seeds} 种子 × 30 年）\n");
        sb.AppendLine("税库进项：每 2400 tick += 人口 / 3（《三个资源》的推荐值）。\n");

        Cfg.TreasuryPeriod = 2400;
        Cfg.TreasuryPopDiv = 3;
        Cfg.TreasuryUpkeepDiv = 0;

        sb.AppendLine("## 一、现在钱花得掉吗\n");
        sb.AppendLine("| 玩法 | 累计进 | 累计出 | 净流入 | 年末税库 | 峰值 | **闲置月占比** |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var (nm, ids) in SETS)
        {
            long inn = 0, outt = 0, fin = 0, mx = 0, idle = 0, months = 0;
            for (int s = 1; s <= seeds; s++)
            {
                var r = One(s, ids);
                inn += r.inn; outt += r.outt; fin += r.final;
                mx += r.mx; idle += r.idleMonths; months += r.months;
            }
            sb.AppendLine($"| {nm} | {inn / seeds} | {outt / seeds} | **{(inn - outt) / seeds}** "
                        + $"| {fin / seeds} | {mx / seeds} | {idle * 100.0 / Math.Max(1, months):F0}% |");
        }
        sb.AppendLine("\n「闲置」= 税库 ≥ 200 的月份占比。**这一列就是「出口够不够」的直接答案。**");
        sb.AppendLine("注意：装**平粜**或**常平**之后，出口是够的（闲置 0–2%）——");
        sb.AppendLine("所以问题不是「没有出口」，是「**不装那两条法，钱就一直是死的**」。\n");

        sb.AppendLine("## 二、官吏俸禄：每 2400 tick 税库 − 人口 / N\n");
        sb.AppendLine("让税库**不再无限增长**的常设出口。对照两种玩法：\n");
        sb.AppendLine("| N | 默认三法 年末 / 峰值 / 闲置 | 开垦系 年末 / 峰值 / 闲置 | 能用 60 金买几条稀有法 |");
        sb.AppendLine("|---|---|---|---|");
        foreach (int div in new[]{ 0, 10, 8, 6, 5, 4, 3 })
        {
            Cfg.TreasuryUpkeepDiv = div;
            var a = Agg(seeds, new[]{1, 5, 3});
            var b = Agg(seeds, new[]{71, 5, 12});
            string canBuy = div == 0 ? "—" : $"{a.fin / 60} 条";
            sb.AppendLine($"| {(div == 0 ? "0（原版）" : div.ToString())} "
                        + $"| {a.fin} / {a.mx} / {a.idle:F0}% | {b.fin} / {b.mx} / {b.idle:F0}% | {canBuy} |");
        }

        sb.AppendLine("\n## 三、选定 N=4 之后，应急出口还够用吗\n");
        Cfg.TreasuryUpkeepDiv = 4;
        sb.AppendLine("| 玩法 | 年末税库 | 峰值 | 闲置月占比 |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var (nm, ids) in SETS)
        {
            var a = Agg(seeds, ids);
            sb.AppendLine($"| {nm} | {a.fin} | {a.mx} | {a.idle:F0}% |");
        }

        Cfg.TreasuryUpkeepDiv = 0;
        Cfg.TreasuryPopDiv = 0;

        File.WriteAllText("scan21-treasury.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan21-treasury.txt");
    }
}
