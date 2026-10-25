using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 给「目标 / 评分 / 通关」定阈值用的分布扫描。
///
/// 先量出每种玩法在 30 年里的：
///   人年、峰值人口、出生、死亡、平均饥饿/恐惧、饥荒月、年末威信/税库
/// 以及「哪条法杀人最多」（从讣告里按法名统计）。
/// </summary>
public static class Score
{
    const int YEARS = 30;

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
        public int personMonths, peak, fin, births, deaths, famineMonths, samples;
        public long hungerSum, fearSum;
        public int prestige, treasury, granary;
    }

    static Sample One(int seed, int[] ids, Dictionary<string, int> lawDeaths)
    {
        var d = NewGame(seed, ids);
        var r = new Sample();
        for (int y = 0; y < YEARS; y++)
        {
            for (int m = 0; m < 12; m++)
            {
                VillageLifecycleService.Advance(d, 240);
                int pop = VillageLifecycleService.CountAlive(d);
                r.personMonths += pop;
                if (pop > r.peak) r.peak = pop;
                long h = 0, f = 0; int n = 0;
                for (int i = 0; i < d.villagerAlive.Length; i++)
                    if (d.villagerAlive[i]) { h += d.villagerHunger[i]; f += d.villagerFear[i]; n++; }
                if (n > 0)
                {
                    r.samples++;
                    r.hungerSum += h / n; r.fearSum += f / n;
                    if (h / n >= 40) r.famineMonths++;
                }
                if (pop == 0) break;
            }
            if (VillageLifecycleService.CountAlive(d) == 0) break;
        }
        r.fin = VillageLifecycleService.CountAlive(d);
        r.births = d.totalBirths; r.deaths = d.totalDeaths;
        r.prestige = d.prestige; r.treasury = d.treasuryGold; r.granary = d.granaryFood;

        for (int i = 0; i < d.obituaryCount && i < d.obituaryLawNames.Length; i++)
        {
            string nm = d.obituaryLawNames[i];
            if (string.IsNullOrEmpty(nm)) continue;
            if (lawDeaths.ContainsKey(nm)) lawDeaths[nm]++;
            else lawDeaths[nm] = 1;
        }
        return r;
    }

    static string P(List<int> v, int p)
    {
        v.Sort();
        return v[Math.Min(v.Count - 1, Math.Max(0, v.Count * p / 100))].ToString();
    }


    static int Clamp100(int v) { return v < 0 ? 0 : (v > 100 ? 100 : v); }
    static int ScoreOf(int personYears, int avgHunger, int famineMonths, int prestige)
    {
        int life = Clamp100(personYears * 100 / 1200);
        int welfare = Clamp100(100 - avgHunger * 2 - famineMonths / 2);
        int total = (life * 70 + welfare * 30) / 100;
        return total;
    }

    public static void BySet(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 评分对照：每种玩法 30 年后的分数（{seeds} 种子）\n");
        sb.AppendLine("| 玩法 | 人年中位 | 平均饥饿 | 饥荒月 | 威信 | **总评中位** | 灭村 |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var (nm, ids) in SETS)
        {
            var pm = new List<int>(); var hu = new List<int>(); var fa = new List<int>();
            var pr = new List<int>(); var sc = new List<int>(); int dead = 0;
            var ld = new Dictionary<string, int>();
            for (int s = 1; s <= seeds; s++)
            {
                var r = One(s, ids, ld);
                int py = r.personMonths / 12;
                int h = r.samples > 0 ? (int)(r.hungerSum / r.samples) : 0;
                pm.Add(py); hu.Add(h); fa.Add(r.famineMonths); pr.Add(r.prestige);
                sc.Add(ScoreOf(py, h, r.famineMonths, r.prestige));
                if (r.fin == 0) dead++;
            }
            pm.Sort(); hu.Sort(); fa.Sort(); pr.Sort(); sc.Sort();
            sb.AppendLine($"| {nm} | {pm[pm.Count/2]} | {hu[hu.Count/2]} | {fa[fa.Count/2]} | {pr[pr.Count/2]} | **{sc[sc.Count/2]}** | {dead*100.0/seeds:F0}% |");
        }
        File.WriteAllText("scan30-scoreset.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan30-scoreset.txt");
    }
    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 结算指标分布（{seeds} 种子 × 30 年 × {SETS.Length} 玩法）\n");
        sb.AppendLine("**给「目标 / 评分 / 通关」定阈值用。** 每局跑满 30 年或灭村。\n");

        var pm = new List<int>(); var peak = new List<int>(); var fin = new List<int>();
        var births = new List<int>(); var deaths = new List<int>();
        var hunger = new List<int>(); var fear = new List<int>();
        var famine = new List<int>(); var prestige = new List<int>();
        var treasury = new List<int>(); var granary = new List<int>(); var sc = new List<int>();
        var lawDeaths = new Dictionary<string, int>();
        int dead = 0, runs = 0;

        foreach (var (nm, ids) in SETS)
            for (int s = 1; s <= seeds; s++)
            {
                var r = One(s, ids, lawDeaths);
                pm.Add(r.personMonths / 12);          // 人年
                peak.Add(r.peak); fin.Add(r.fin);
                births.Add(r.births); deaths.Add(r.deaths);
                hunger.Add(r.samples > 0 ? (int)(r.hungerSum / r.samples) : 0);
                fear.Add(r.samples > 0 ? (int)(r.fearSum / r.samples) : 0);
                famine.Add(r.famineMonths);
                prestige.Add(r.prestige); treasury.Add(r.treasury); granary.Add(r.granary);
                sc.Add(ScoreOf(r.personMonths / 12, r.samples > 0 ? (int)(r.hungerSum / r.samples) : 0, r.famineMonths, r.prestige));
                if (r.fin == 0) dead++;
                runs++;
            }

        sb.AppendLine($"灭村率：{dead * 100.0 / Math.Max(1, runs):F0}%\n");
        sb.AppendLine("## 一、候选评分指标的分位\n");
        sb.AppendLine("| 指标 | p10 | p25 | p50 | p75 | p90 | max |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        void Row(string name, List<int> v)
        {
            int max = 0; foreach (var x in v) if (x > max) max = x;
            sb.AppendLine($"| {name} | {P(v,10)} | {P(v,25)} | {P(v,50)} | {P(v,75)} | {P(v,90)} | {max} |");
        }
        Row("人年（人·年）", pm);
        Row("峰值人口", peak);
        Row("年末人口", fin);
        Row("累计出生", births);
        Row("累计死亡", deaths);
        Row("平均饥饿", hunger);
        Row("平均恐惧", fear);
        Row("饥荒月数", famine);
        Row("年末威信", prestige);
        Row("年末税库", treasury);
        Row("年末粮仓", granary);
        Row("总评（0-100）", sc);

        sb.AppendLine("\n## 二、哪条法杀人最多（全部局合计）\n");
        sb.AppendLine("| 法 | 致死 |");
        sb.AppendLine("|---|---|");
        var ld = new List<KeyValuePair<string, int>>(lawDeaths);
        ld.Sort((a, b) => b.Value.CompareTo(a.Value));
        for (int i = 0; i < ld.Count && i < 15; i++)
            sb.AppendLine($"| {ld[i].Key} | {ld[i].Value} |");

        File.WriteAllText("scan29-score.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan29-score.txt");
    }
}
