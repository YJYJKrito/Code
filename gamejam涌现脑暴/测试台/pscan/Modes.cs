using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 两种模式的数值标定。
///
/// A. 五十年固定局：50 年，按人年/温饱评分（满分线要从 1200 调到 2000）
/// B. 高压模式：实时分 V = 健康人口×2，达到目标线并维持 3 个月通关；
///             跌破斩杀线 6 个月判负。这里先量 V 的轨迹和达标时间。
/// </summary>
public static class Modes
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

    struct R
    {
        public int personMonths, samples, famineMonths; public long hungerSum;
        public int vMax, vAt10, vAt20, vAt30, vAt50;
        public int firstT40, firstT50, firstT60, firstT70, firstT80;      // 首次达到 V 的月份（-1 = 从未）
        public int monthsBelow15, monthsAbove50, cur60, max60, cur50, max50, cur40, max40;
        public int fin;
    }

    static int V(VillageSaveData d)
    {
        long s = 0;
        for (int i = 0; i < d.villagerAlive.Length; i++)
            if (d.villagerAlive[i]) s += Math.Max(0, 100 - d.villagerHunger[i]);
        return (int)(s / 100) * 2;      // 健康人口 × 2
    }

    static R One(int seed, int[] ids)
    {
        var d = NewGame(seed, ids);
        var r = new R();
        r.firstT40 = r.firstT50 = r.firstT60 = r.firstT70 = r.firstT80 = -1;
        int month = 0;
        for (int y = 0; y < YEARS; y++)
        {
            for (int m = 0; m < 12; m++)
            {
                VillageLifecycleService.Advance(d, 240);
                month++;
                int pop = VillageLifecycleService.CountAlive(d);
                r.personMonths += pop;
                long h = 0; int n = 0;
                for (int i = 0; i < d.villagerAlive.Length; i++)
                    if (d.villagerAlive[i]) { h += d.villagerHunger[i]; n++; }
                if (n > 0)
                {
                    r.samples++; r.hungerSum += h / n;
                    if (h / n >= 40) r.famineMonths++;
                }
                int v = V(d);
                if (v > r.vMax) r.vMax = v;
                if (v < 15) r.monthsBelow15++;
                if (v >= 50) r.monthsAbove50++;
                r.cur60 = (v >= 60) ? r.cur60 + 1 : 0; if (r.cur60 > r.max60) r.max60 = r.cur60;
                r.cur50 = (v >= 50) ? r.cur50 + 1 : 0; if (r.cur50 > r.max50) r.max50 = r.cur50;
                r.cur40 = (v >= 40) ? r.cur40 + 1 : 0; if (r.cur40 > r.max40) r.max40 = r.cur40;
                if (r.firstT40 < 0 && v >= 40) r.firstT40 = month;
                if (r.firstT50 < 0 && v >= 50) r.firstT50 = month;
                if (r.firstT60 < 0 && v >= 60) r.firstT60 = month;
                if (r.firstT70 < 0 && v >= 70) r.firstT70 = month;
                if (r.firstT80 < 0 && v >= 80) r.firstT80 = month;
                if (month == 120) r.vAt10 = v;
                if (month == 240) r.vAt20 = v;
                if (month == 360) r.vAt30 = v;
                if (pop == 0) break;
            }
            if (VillageLifecycleService.CountAlive(d) == 0) break;
        }
        r.vAt50 = V(d);
        r.fin = VillageLifecycleService.CountAlive(d);
        return r;
    }

    static int Cl100(int v) { return v < 0 ? 0 : (v > 100 ? 100 : v); }
    static int ScoreA(int py, int hunger, int famine, int cap)
    {
        int life = Cl100(py * 100 / cap);
        int wel = Cl100(100 - hunger * 2 - famine / 2);
        return (life * 70 + wel * 30) / 100;
    }

    static string P(List<int> v, int p)
    {
        v.Sort();
        return v[Math.Min(v.Count - 1, Math.Max(0, v.Count * p / 100))].ToString();
    }

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 两种模式 · 数值标定（{seeds} 种子 × {YEARS} 年 × {SETS.Length} 玩法）\n");

        var score1200 = new List<int>(); var score2000 = new List<int>();
        var vmax = new List<int>(); var t40 = new List<int>(); var t50 = new List<int>(); var t60 = new List<int>();

        sb.AppendLine("## 一、五十年固定局：每种玩法\n");
        sb.AppendLine("| 玩法 | 人年 | 饥饿 | 饥荒月 | 总评(1200) | **总评(2000)** | 灭村 |");
        sb.AppendLine("|---|---|---|---|---|---|---|");

        foreach (var (nm, ids) in SETS)
        {
            var py = new List<int>(); var hu = new List<int>(); var fa = new List<int>();
            var s12 = new List<int>(); var s20 = new List<int>(); int dead = 0;
            for (int s = 1; s <= seeds; s++)
            {
                var r = One(s, ids);
                int years = r.personMonths / 12;
                int hunger = r.samples > 0 ? (int)(r.hungerSum / r.samples) : 0;
                py.Add(years); hu.Add(hunger); fa.Add(r.famineMonths);
                s12.Add(ScoreA(years, hunger, r.famineMonths, 1200));
                s20.Add(ScoreA(years, hunger, r.famineMonths, 2000));
                score1200.Add(ScoreA(years, hunger, r.famineMonths, 1200));
                score2000.Add(ScoreA(years, hunger, r.famineMonths, 2000));
                if (r.fin == 0) dead++;
            }
            py.Sort(); hu.Sort(); fa.Sort(); s12.Sort(); s20.Sort();
            sb.AppendLine($"| {nm} | {py[py.Count/2]} | {hu[hu.Count/2]} | {fa[fa.Count/2]} | {s12[s12.Count/2]} | **{s20[s20.Count/2]}** | {dead*100.0/seeds:F0}% |");
        }

        sb.AppendLine("\n### 分数分位\n");
        sb.AppendLine("| 指标 | p10 | p25 | p50 | p75 | p90 | max |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        void Row(string n, List<int> v)
        {
            int m = 0; foreach (var x in v) if (x > m) m = x;
            sb.AppendLine($"| {n} | {P(v,10)} | {P(v,25)} | {P(v,50)} | {P(v,75)} | {P(v,90)} | {m} |");
        }
        Row("总评（1200 满线）", score1200);
        Row("总评（2000 满线）", score2000);

        // 高压模式：V 的轨迹
        var vt = new List<int>();
        sb.AppendLine("\n## 二、高压模式的实时分 V = 健康人口 × 2\n");
        var setsV = new List<(string, List<int>, List<int>, List<int>, List<int>)>();
        var t70All = new List<int>(); var t80All = new List<int>();
        foreach (var (nm, ids) in SETS)
        {
            var vmaxS = new List<int>(); var t40S = new List<int>(); var t50S = new List<int>(); var t60S = new List<int>();
            var t70S = new List<int>(); var t80S = new List<int>(); var v10 = new List<int>(); var v30 = new List<int>();
            var m40 = new List<int>(); var m50 = new List<int>(); var m60 = new List<int>();
            for (int s = 1; s <= seeds; s++)
            {
                var r = One(s, ids);
                vmaxS.Add(r.vMax);
                if (r.firstT40 > 0) t40S.Add(r.firstT40 / 12 + 1);
                if (r.firstT50 > 0) t50S.Add(r.firstT50 / 12 + 1);
                if (r.firstT60 > 0) t60S.Add(r.firstT60 / 12 + 1);
                if (r.firstT70 > 0) t70S.Add(r.firstT70 / 12 + 1);
                if (r.firstT80 > 0) t80S.Add(r.firstT80 / 12 + 1);
                v10.Add(r.vAt10); v30.Add(r.vAt30);
                m40.Add(r.max40 / 12); m50.Add(r.max50 / 12); m60.Add(r.max60 / 12);
                vt.Add(r.vMax);
            }
            vmaxS.Sort();
            v10.Sort(); v30.Sort(); m40.Sort(); m50.Sort(); m60.Sort();
            setsV.Add((nm, vmaxS, m40, m50, m60));
        }
        sb.AppendLine("| 玩法 | V 峰值 | **V≥40 最长连击(年)** | V≥50 | V≥60 |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var (nm, vm, m40, m50, m60) in setsV)
        {
            sb.AppendLine($"| {nm} | {vm[vm.Count/2]} | **{m40[m40.Count/2]}** | {m50[m50.Count/2]} | {m60[m60.Count/2]} |");
        }

        File.WriteAllText("scan31-modes.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan31-modes.txt");
    }
}
