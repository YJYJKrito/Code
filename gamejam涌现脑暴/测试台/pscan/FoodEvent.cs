using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 事件 × 个人存粮。
///
/// 第一轮测出来的结论是：**事件抢私粮没有任何效果** ——
/// 因为私粮均值只有 2–4，上限就是 8（几天就吃完了），抢走一半也就是 1 个单位。
///
/// 所以要分两步：
///   ① 先把私粮仓做大（上调上限），看村子会不会变
///   ② 仓做大之后，事件抢它才有意义
/// </summary>
public static class FoodEvent
{
    const int YEARS = 30;
    const int FAMINE_HUNGER = 40;

    static readonly (string nm, int[] ids)[] SETS = {
        ("不装法",   new int[0]),
        ("默认三法", new[]{1, 5, 3}),
        ("屯粮",     new[]{1, 5, 3, 30}),
        ("苛政",     new[]{54, 39, 3, 7, 2, 16}),
        ("开垦系",   new[]{71, 5, 12}),
        ("恐惧系",   new[]{1, 5, 3, 4, 7, 16, 60}),
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
        public int fin, starved, famineMonths;
        public long hungerSum, foodSum; public int samples;
    }

    static Sample One(int seed, int[] ids)
    {
        var d = NewGame(seed, ids);
        var r = new Sample();
        for (int y = 0; y < YEARS; y++)
        {
            for (int m = 0; m < 12; m++)
            {
                VillageLifecycleService.Advance(d, 240);
                long h = 0, f = 0; int n = 0;
                for (int i = 0; i < d.villagerAlive.Length; i++)
                    if (d.villagerAlive[i]) { h += d.villagerHunger[i]; f += d.villagerFood[i]; n++; }
                if (n > 0)
                {
                    r.samples++;
                    r.hungerSum += h / n; r.foodSum += f / n;
                    if (h / n >= FAMINE_HUNGER) r.famineMonths++;
                }
                if (VillageLifecycleService.CountAlive(d) == 0) break;
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
        public int finMed, hungerMed, foodMed, starveMed, famineMed;
    }

    static Agg Measure(int seeds)
    {
        var a = new Agg();
        var fin = new List<int>(); var hung = new List<long>(); var food = new List<long>();
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
                food.Add(r.samples > 0 ? r.foodSum / r.samples : 0);
            }
        fin.Sort(); hung.Sort(); food.Sort(); starve.Sort(); fam.Sort();
        a.finMed = fin[fin.Count / 2]; a.hungerMed = (int)hung[hung.Count / 2];
        a.foodMed = (int)food[food.Count / 2]; a.starveMed = starve[starve.Count / 2];
        a.famineMed = fam[fam.Count / 2];
        return a;
    }

    static string Row(string label, Agg a, int seeds)
    {
        double cap = a.dead * 100.0 / Math.Max(1, a.runs);
        double fr = a.runsWithFamine * 100.0 / Math.Max(1, a.runs);
        return $"| {label} | {a.finMed} | {a.foodMed} | {a.hungerMed} | {fr:F0}% | {a.famineMed} | {a.starveMed} | {cap:F0}% |";
    }

    static void SetupBase()
    {
        Cfg.EventChance = 45; Cfg.EventNegScale = 100; Cfg.ForceEvent = -1; Cfg.ForceHelper = 0;
        Cfg.LawModOn = 1; Cfg.ProfOn = 0;
        Cfg.TreasuryPeriod = 2400; Cfg.TreasuryPopDiv = 3; Cfg.TreasuryUpkeepDiv = 4;
        Cfg.BanditStealPct = 50; Cfg.RiotStealPct = 30; Cfg.ColdSpoilPct = 20; Cfg.HarvestFill = 4;
        // ★ 在**要上线的经济**里测：劳动力 5 × 50%
        Cfg.LaborMode = 1; Cfg.LabPerWorker = 5; Cfg.LabHungerScale = 50;
        Cfg.LabOldW = 50; Cfg.LabStarveFloor = 10;
    }

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 事件 × 个人存粮（{seeds} 种子 × 30 年 × {SETS.Length} 玩法）\n");
        sb.AppendLine("**第一轮结论：事件抢私粮没有任何效果** —— 私粮均值只有 2–4，上限 8，抢一半就是 1 个单位。");
        sb.AppendLine("所以这份表分两步：先把仓做大，再谈抢。\n");

        SetupBase();

        sb.AppendLine("## 一、先把私粮仓做大（事件关）\n");
        sb.AppendLine("私粮上限从 8 提到 X 之后，村子会不会变？\n");
        sb.AppendLine("| 上限 | 末人口中位 | 私粮均值 | 平均饥饿 | 经历过饥荒的局 | 饿死人中位 | 灭村率 |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        Cfg.EventChance = 0; Cfg.EventFoodHit = 0;
        foreach (int cap in new[]{ 8, 12, 20, 30, 40 })
        {
            Cfg.FoodCapAdult = cap; Cfg.FoodCapChild = Math.Max(4, cap / 2);
            var a = Measure(seeds);
            double capRate = a.dead * 100.0 / Math.Max(1, a.runs);
            double fr = a.runsWithFamine * 100.0 / Math.Max(1, a.runs);
            sb.AppendLine($"| {(cap == 8 ? "8（原版）" : cap.ToString())} | {a.finMed} | {a.foodMed} | {a.hungerMed} | {fr:F0}% | {a.starveMed} | {capRate:F0}% |");
        }

        sb.AppendLine("\n## 二、仓做大之后，事件抢它才有意义（事件 45%）\n");
        sb.AppendLine("| 上限 | 事件不碰私粮 | **事件碰私粮** |");
        sb.AppendLine("|---|---|---|");
        Cfg.EventChance = 45;
        foreach (int cap in new[]{ 8, 20, 30 })
        {
            Cfg.FoodCapAdult = cap; Cfg.FoodCapChild = Math.Max(4, cap / 2);
            Cfg.EventFoodHit = 0; var off = Measure(seeds);
            Cfg.EventFoodHit = 1; var on = Measure(seeds);
            sb.AppendLine($"| {cap} | {off.finMed} 人 / 饥饿 {off.hungerMed} / 饿死 {off.starveMed} "
                        + $"| **{on.finMed} 人 / 饥饿 {on.hungerMed} / 饿死 {on.starveMed}** |");
        }

        sb.AppendLine("\n## 三、按玩法拆开（上限 30 · 事件 45%）\n");
        sb.AppendLine("| 玩法 | 末人口 关 / 开 | 私粮 关 / 开 | 平均饥饿 关 / 开 | 饿死人 关 / 开 |");
        sb.AppendLine("|---|---|---|---|---|");
        Cfg.FoodCapAdult = 30; Cfg.FoodCapChild = 15;
        for (int gi = 0; gi < SETS.Length; gi++)
        {
            var rows = new (int fin, int starve, long hung, long food)[2];
            for (int mode = 0; mode < 2; mode++)
            {
                Cfg.EventFoodHit = mode;
                long hung = 0, food = 0; int fin = 0, starve = 0, runs = 0;
                for (int s = 1; s <= seeds; s++)
                {
                    var r = One(s, SETS[gi].ids);
                    fin += r.fin; starve += r.starved; runs++;
                    hung += r.samples > 0 ? r.hungerSum / r.samples : 0;
                    food += r.samples > 0 ? r.foodSum / r.samples : 0;
                }
                rows[mode] = (fin / runs, starve / runs, hung / runs, food / runs);
            }
            sb.AppendLine($"| {SETS[gi].nm} | {rows[0].fin} / **{rows[1].fin}** "
                        + $"| {rows[0].food} / **{rows[1].food}** "
                        + $"| {rows[0].hung} / **{rows[1].hung}** "
                        + $"| {rows[0].starve} / **{rows[1].starve}** |");
        }

        sb.AppendLine("\n## 四、极端测试：把匪患拉满（每 0.6 年一次）\n");
        sb.AppendLine("上面的表是自然频率（30 年约 8 次抢粮）。这一节把匪患强制成每 1800 tick 一次，");
        sb.AppendLine("看「私粮被抢」这件事**到底能不能改变结局**。\n");
        sb.AppendLine("| 上限 | 事件不碰私粮 | **事件碰私粮（每次抢 100%）** |");
        sb.AppendLine("|---|---|---|");
        Cfg.EventChance = 100; Cfg.ForceEvent = 6; Cfg.BanditStealPct = 100;
        foreach (int cap in new[]{ 8, 30 })
        {
            Cfg.FoodCapAdult = cap; Cfg.FoodCapChild = Math.Max(4, cap / 2);
            Cfg.EventFoodHit = 0; var off = Measure(seeds);
            Cfg.EventFoodHit = 1; var on = Measure(seeds);
            sb.AppendLine($"| {cap} | {off.finMed} 人 / 饥饿 {off.hungerMed} / 饿死 {off.starveMed} / 灭村 {off.dead * 100.0 / off.runs:F0}% "
                        + $"| **{on.finMed} 人 / 饥饿 {on.hungerMed} / 饿死 {on.starveMed} / 灭村 {on.dead * 100.0 / on.runs:F0}%** |");
        }
        Cfg.ForceEvent = -1; Cfg.BanditStealPct = 50;

        Cfg.EventFoodHit = 0; Cfg.EventChance = 0; Cfg.TreasuryUpkeepDiv = 0; Cfg.TreasuryPopDiv = 0;
        Cfg.FoodCapAdult = 8; Cfg.FoodCapChild = 4;
        Cfg.LaborMode = 0; Cfg.LabPerWorker = 4; Cfg.LabHungerScale = 100;

        File.WriteAllText("scan22-foodevent.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan22-foodevent.txt");
    }
}
