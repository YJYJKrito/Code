using System; using System.IO; using System.Text;

/// <summary>
/// 《三个资源》的**三态对照**：原版 / 旧方案已落地 / 新建议。
///
/// 为什么是三态而不是两态：**旧文档程序已经改完了**，
/// 所以新文档要回答的不是「改了会怎样」，而是「**在你已经改过的基础上，还要改哪几处**」。
///
/// 这三条改动会同时动到恐惧法条（职业）、税库事件、威信事件，
/// 所以每一态都要顺带跑事件覆盖和职业可达。
/// </summary>
public static class ResFix
{
    const int YEARS = 30;

    static readonly (string nm, int[] ids)[] SETS = {
        ("默认三法", new[]{1, 5, 3}),
        ("开垦系",   new[]{71, 5, 12}),
        ("不装法",   new int[0]),
        ("苛政",     new[]{54, 39, 3, 7, 2, 16}),
    };

    // 文档里那 10 条恐惧法条
    static readonly int[] FEAR_LAWS = { 35, 47, 7, 37, 64, 34, 18, 22, 50, 36 };

    // 0 = 原版（未改）
    // 1 = **旧方案已落地**（程序已经按《三个资源接通-方案》改完了）
    // 2 = 新建议（这份文档要求再改的部分）
    static void Apply(int p)
    {
        Cfg.FearThScale      = p == 0 ? 100 : (p == 1 ? 50 : 70);
        Cfg.PrestigePerLaw   = p == 0 ? 3 : 4;
        Cfg.PrestigeFearBase = p == 0 ? 30 : 12;
        Cfg.PrestigeFearDiv  = p == 0 ? 10 : 2;
        Cfg.TreasuryPeriod   = 2400;
        Cfg.TreasuryPopDiv   = p == 0 ? 0 : (p == 1 ? 8 : 3);
        Cfg.PrestigeSoftCap  = p == 2 ? 200 : 0;
        Cfg.ProfOn           = 1;     // 顺带看职业有没有被带崩
        Cfg.EventChance      = 45;    // 顺带看事件覆盖
    }

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

    struct Agg
    {
        public int runs;
        public int[] lawFire;
        public int treasuryMin, treasuryMax; public long treasurySum;
        public int prestigeCap, prestigeZero; public long prestigeSum;
        public int[] eventRuns;
        public int[] profRuns;
    }

    /// <summary>
    /// ★ 一条法只有**装在法槽里**才可能触发。
    /// 所以「门槛改小之后会不会活」必须单独装它跑 —— 混在固定法条组里测是测不出来的。
    /// </summary>
    static (int fires, int avgPop) FearLawFire(int lawId, int seeds)
    {
        int total = 0, pop = 0;
        for (int s = 1; s <= seeds; s++)
        {
            Cfg.ResetDiagnostics();
            var d = NewGame(s, new[]{1, 5, 3, lawId});
            for (int y = 0; y < YEARS; y++)
            {
                VillageLifecycleService.Advance(d, 2920);
                if (VillageLifecycleService.CountAlive(d) == 0) break;
            }
            total += Cfg.LawFire[lawId];
            pop += VillageLifecycleService.CountAlive(d);
        }
        return (total, pop / Math.Max(1, seeds));
    }

    static Agg RunConfig(int seeds)
    {
        var a = new Agg();
        a.lawFire = new int[FEAR_LAWS.Length];
        a.treasuryMin = int.MaxValue;
        a.eventRuns = new int[14];
        a.profRuns = new int[16];

        for (int k = 0; k < FEAR_LAWS.Length; k++)
            a.lawFire[k] = FearLawFire(FEAR_LAWS[k], seeds).fires;

        foreach (var (nm, ids) in SETS)
        {
            for (int s = 1; s <= seeds; s++)
            {
                Cfg.ResetDiagnostics();
                Cfg.ResetProf();
                for (int e = 0; e < 14; e++) Cfg.EventFired[e] = 0;

                var d = NewGame(s, ids);
                for (int y = 0; y < YEARS; y++)
                {
                    VillageLifecycleService.Advance(d, 2920);
                    if (VillageLifecycleService.CountAlive(d) == 0) break;
                }

                a.runs++;

                if (d.treasuryGold < a.treasuryMin) a.treasuryMin = d.treasuryGold;
                if (d.treasuryGold > a.treasuryMax) a.treasuryMax = d.treasuryGold;
                a.treasurySum += d.treasuryGold;

                a.prestigeSum += d.prestige;
                if (d.prestige >= 300) a.prestigeCap++;
                if (d.prestige <= 0) a.prestigeZero++;

                for (int e = 0; e < 14; e++) if (Cfg.EventFired[e] > 0) a.eventRuns[e]++;
                for (int b = 0; b < 16; b++) if (Cfg.ProfGrantCount[b] > 0) a.profRuns[b]++;
            }
        }
        return a;
    }

    static (int mn, int avg, int mx, double ge60) TreasuryMetrics(int seeds)
    {
        int mn = int.MaxValue, mx = 0, runs = 0, ge = 0; long sum = 0;
        foreach (var (nm, ids) in SETS)
            for (int s = 1; s <= seeds; s++)
            {
                var d = NewGame(s, ids);
                for (int y = 0; y < YEARS; y++)
                {
                    VillageLifecycleService.Advance(d, 2920);
                    if (VillageLifecycleService.CountAlive(d) == 0) break;
                }
                runs++;
                if (d.treasuryGold < mn) mn = d.treasuryGold;
                if (d.treasuryGold > mx) mx = d.treasuryGold;
                if (d.treasuryGold >= 60) ge++;
                sum += d.treasuryGold;
            }
        return (mn, (int)(sum / Math.Max(1, runs)), mx, ge * 100.0 / Math.Max(1, runs));
    }

    static (double cap, double zero, int avg) PrestigeMetrics(int seeds)
    {
        int runs = 0, cap = 0, zero = 0; long sum = 0;
        foreach (var (nm, ids) in SETS)
            for (int s = 1; s <= seeds; s++)
            {
                var d = NewGame(s, ids);
                for (int y = 0; y < YEARS; y++)
                {
                    VillageLifecycleService.Advance(d, 2920);
                    if (VillageLifecycleService.CountAlive(d) == 0) break;
                }
                runs++;
                if (d.prestige >= 300) cap++;
                if (d.prestige <= 0) zero++;
                sum += d.prestige;
            }
        return (cap * 100.0 / Math.Max(1, runs), zero * 100.0 / Math.Max(1, runs), (int)(sum / Math.Max(1, runs)));
    }

    static string Pct(int n, int runs) { return (n * 100.0 / Math.Max(1, runs)).ToString("F0") + "%"; }

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 三个资源 · 三态对照（{seeds} 种子 × 30 年 × {SETS.Length} 玩法）\n");
        sb.AppendLine("| 列 | 含义 |");
        sb.AppendLine("|---|---|");
        sb.AppendLine("| **原版** | 程序员改之前 |");
        sb.AppendLine("| **旧方案** | 程序**已经按旧文档改完**的状态（×50 / 人口÷8 / 4条+有符号）|");
        sb.AppendLine("| **新建议** | 这份文档要求再改的部分（×70 / 人口÷3 / 加软上限）|\n");

        Apply(0); var baseline = RunConfig(seeds);
        Apply(1); var oldfix   = RunConfig(seeds);
        Apply(2); var newfix   = RunConfig(seeds);

        sb.AppendLine("## 一、10 条恐惧法条（逐条单独装）\n");
        sb.AppendLine("| 法条 | 原版 | **旧方案（已落地）** | **新建议** |");
        sb.AppendLine("|---|---|---|---|");
        for (int k = 0; k < FEAR_LAWS.Length; k++)
            sb.AppendLine($"| {LawCatalog.FindById(FEAR_LAWS[k])?.name}（{FEAR_LAWS[k]}） "
                        + $"| {baseline.lawFire[k]} | {oldfix.lawFire[k]} | **{newfix.lawFire[k]}** |");
        long f0 = 0, f1 = 0, f2 = 0;
        for (int k = 0; k < FEAR_LAWS.Length; k++)
        { f0 += baseline.lawFire[k]; f1 += oldfix.lawFire[k]; f2 += newfix.lawFire[k]; }
        sb.AppendLine($"| **合计** | {f0} | {f1} | **{f2}** |\n");

        sb.AppendLine("### 一·补 为什么从 ×50 退到 ×70：触发次数 / 30 年末人口\n");
        sb.AppendLine("| 法条 | ×100（原版） | ×70（新建议） | ×50（已落地） |");
        sb.AppendLine("|---|---|---|---|");
        foreach (int lid in FEAR_LAWS)
        {
            Cfg.FearThScale = 100; var r100 = FearLawFire(lid, seeds);
            Cfg.FearThScale = 70;  var r70  = FearLawFire(lid, seeds);
            Cfg.FearThScale = 50;  var r50  = FearLawFire(lid, seeds);
            sb.AppendLine($"| {LawCatalog.FindById(lid)?.name}（{lid}） "
                        + $"| {r100.fires} / {r100.avgPop} | **{r70.fires} / {r70.avgPop}** | {r50.fires} / {r50.avgPop} |");
        }
        Cfg.FearThScale = 70;
        sb.AppendLine();

        sb.AppendLine("## 二、税库 / 威信\n");
        sb.AppendLine("| 指标 | 原版 | **旧方案（已落地）** | **新建议** | 目标 |");
        sb.AppendLine("|---|---|---|---|---|");
        sb.AppendLine("| 税库 最小 / 平均 / 最大 "
                      + $"| {baseline.treasuryMin} / {baseline.treasurySum / Math.Max(1, baseline.runs)} / {baseline.treasuryMax} "
                      + $"| {oldfix.treasuryMin} / {oldfix.treasurySum / Math.Max(1, oldfix.runs)} / {oldfix.treasuryMax} "
                      + $"| **{newfix.treasuryMin} / {newfix.treasurySum / Math.Max(1, newfix.runs)} / {newfix.treasuryMax}** | 100–300 |");
        sb.AppendLine("| 威信 平均 "
                      + $"| {baseline.prestigeSum / Math.Max(1, baseline.runs)} "
                      + $"| {oldfix.prestigeSum / Math.Max(1, oldfix.runs)} "
                      + $"| **{newfix.prestigeSum / Math.Max(1, newfix.runs)}** | — |");
        sb.AppendLine("| 威信顶 300 占比 "
                      + $"| {Pct(baseline.prestigeCap, baseline.runs)} "
                      + $"| {Pct(oldfix.prestigeCap, oldfix.runs)} "
                      + $"| **{Pct(newfix.prestigeCap, newfix.runs)}** | ≤ 25% |");
        sb.AppendLine("| 威信归零（法律停摆）占比 "
                      + $"| {Pct(baseline.prestigeZero, baseline.runs)} "
                      + $"| {Pct(oldfix.prestigeZero, oldfix.runs)} "
                      + $"| **{Pct(newfix.prestigeZero, newfix.runs)}** | ≤ 10% |");

        // ── 参数扫描 ──
        sb.AppendLine("\n## 二·补 参数扫描（定最终数值用）\n");
        sb.AppendLine("### 税库进项：每 2400 tick += 人口 / N\n");
        sb.AppendLine("| N | 30 年末 最小 / 平均 / 最大 | 能买到 ≥60 金的局的占比 | 判定 |");
        sb.AppendLine("|---|---|---|---|");
        foreach (int div in new[]{ 2, 3, 4, 6, 8 })
        {
            Cfg.TreasuryPopDiv = div;
            var (mn, avg, mx, ge) = TreasuryMetrics(seeds);
            string verdict = (avg >= 100 && avg <= 300) ? "✅ 落在 100–300" : (avg < 100 ? "⚠ 偏紧" : "⚠ 偏宽");
            string tag = div == 8 ? " ← 已落地" : (div == 3 ? " ← 新建议" : "");
            sb.AppendLine($"| {div}{tag} | {mn} / {avg} / {mx} | {ge:F0}% | {verdict} |");
        }

        sb.AppendLine("\n### 威信：维持费 / 条 · 恐惧项 (基准 − 恐惧) / 除数\n");
        sb.AppendLine("| 维持费 | 恐惧项 | 顶 300 占比 | 归零占比 | 平均威信 | 判定 |");
        sb.AppendLine("|---|---|---|---|---|---|");
        var cand = new (int per, int b, int d, int soft, string tag)[]{
            (3, 30, 10, 0, " 原版"), (4, 12, 2, 0, " **已落地**"),
            (5, 12, 2, 0, ""), (6, 12, 2, 0, ""),
            (4, 12, 2, 200, " **新建议**"), (4, 12, 2, 150, ""),
        };
        foreach (var (per, b, d, soft, tag) in cand)
        {
            Cfg.PrestigePerLaw = per; Cfg.PrestigeFearBase = b; Cfg.PrestigeFearDiv = d;
            Cfg.PrestigeSoftCap = soft;
            var (cap, zero, pavg) = PrestigeMetrics(seeds);
            string verdict = cap <= 25 && zero <= 10 ? "✅" : (cap > 25 ? "⚠ 顶上限" : "⚠ 太多归零");
            string softS = soft > 0 ? $" · 软上限 {soft}" : "";
            sb.AppendLine($"| {per}/条{tag} | ({b}−恐惧)/{d}{softS} | {cap:F0}% | {zero:F0}% | {pavg} | {verdict} |");
        }
        Cfg.PrestigeSoftCap = 0;

        sb.AppendLine("\n## 三、回归：14 个事件\n");
        sb.AppendLine("| 事件 | 原版 | 旧方案 | **新建议** |");
        sb.AppendLine("|---|---|---|---|");
        int ev0 = 0, ev1 = 0, ev2 = 0;
        string[] EV_NAMES = {"蝗灾","疫病","边患","民变","贪墨","寒潮","匪患","丰年","商队","乡绅","人口回流","名医","大赦","新生儿潮"};
        for (int e = 0; e < 14; e++)
        {
            double a = baseline.eventRuns[e] * 100.0 / Math.Max(1, baseline.runs);
            double b = oldfix.eventRuns[e] * 100.0 / Math.Max(1, oldfix.runs);
            double c = newfix.eventRuns[e] * 100.0 / Math.Max(1, newfix.runs);
            if (a >= 1) ev0++; if (b >= 1) ev1++; if (c >= 1) ev2++;
            sb.AppendLine($"| {EV_NAMES[e]} | {a:F0}% | {b:F0}% | **{c:F0}%** |");
        }
        sb.AppendLine($"\n**有触发：原版 {ev0}/14 · 旧方案 {ev1}/14 · 新建议 {ev2}/14**\n");

        sb.AppendLine("## 四、回归：13 个职业（4 玩法粗测）\n");
        sb.AppendLine("| 职业 | 原版 | 旧方案 | **新建议** |");
        sb.AppendLine("|---|---|---|---|");
        string[] bits = { "罪人","劫匪","逃兵","绝户","光棍","债户","徙民","积怨","劳损",
                          "屠者","尸亲","恩主","巫祝" };
        int[] bitIdx = { 0,1,2,3,5,6,7,11,10,12,13,14,15 };
        int pr0 = 0, pr1 = 0, pr2 = 0;
        for (int k = 0; k < bits.Length; k++)
        {
            int b = bitIdx[k];
            double x = baseline.profRuns[b] * 100.0 / Math.Max(1, baseline.runs);
            double y = oldfix.profRuns[b]   * 100.0 / Math.Max(1, oldfix.runs);
            double z = newfix.profRuns[b]   * 100.0 / Math.Max(1, newfix.runs);
            if (x >= 1) pr0++; if (y >= 1) pr1++; if (z >= 1) pr2++;
            sb.AppendLine($"| {bits[k]} | {x:F0}% | {y:F0}% | **{z:F0}%** |");
        }
        sb.AppendLine($"\n**拿得到：原版 {pr0}/13 · 旧方案 {pr1}/13 · 新建议 {pr2}/13**\n");

        sb.AppendLine("## 五、结论\n");
        sb.AppendLine($"- 恐惧法条合计触发：{f0} → {f1}（旧方案）→ **{f2}**（新建议）");
        sb.AppendLine($"- 税库平均：{baseline.treasurySum / Math.Max(1, baseline.runs)} → {oldfix.treasurySum / Math.Max(1, oldfix.runs)} → **{newfix.treasurySum / Math.Max(1, newfix.runs)}**");
        sb.AppendLine($"- 威信顶 300：{Pct(baseline.prestigeCap, baseline.runs)} → {Pct(oldfix.prestigeCap, oldfix.runs)} → **{Pct(newfix.prestigeCap, newfix.runs)}**");
        sb.AppendLine($"- 事件：{ev0}/14 → {ev1}/14 → **{ev2}/14**");
        sb.AppendLine($"- 职业：{pr0}/13 → {pr1}/13 → **{pr2}/13**");

        // 测完恢复成「当前构建」
        Apply(0);
        Cfg.ProfOn = 0; Cfg.EventChance = 0;

        File.WriteAllText("scan17-resfix.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan17-resfix.txt");
    }
}
