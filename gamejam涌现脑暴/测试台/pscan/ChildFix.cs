using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 「幼童口粮 = 100% 灭村」的修法扫描。
///
/// 根因：劳动力接上之后，10 岁以下产出为 0，而生育类法让小孩变多 →
///       人均产出下降 → 饥荒 → 更多死亡。
///
/// 试的杠杆：**10 岁以下给一点点产出**（拾穗/看家），看灭村率能不能降下来。
/// </summary>
public static class ChildFix
{
    const int YEARS = 30;

    static readonly (string nm, int[] ids)[] SETS = {
        ("只装幼童口粮(9)",   new[]{9}),
        ("生育系(8,9,12)",    new[]{8, 9, 12}),
        ("生育系+童工",       new[]{8, 9, 12, 39}),
        ("默认三法(对照)",    new[]{1, 5, 3}),
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

    static (int med, int min, int dead, int starveMed) One(int seeds, int[] ids)
    {
        var fp = new List<int>(); var st = new List<int>(); int dead = 0;
        for (int s = 1; s <= seeds; s++)
        {
            var d = NewGame(s, ids);
            for (int y = 0; y < YEARS; y++)
            {
                VillageLifecycleService.Advance(d, 2920);
                if (VillageLifecycleService.CountAlive(d) == 0) break;
            }
            int f = VillageLifecycleService.CountAlive(d);
            fp.Add(f); if (f == 0) dead++;
            int n = 0;
            for (int i = 0; i < d.obituaryCount && i < d.obituaryCauses.Length; i++)
                if (d.obituaryCauses[i] == "饥饿") n++;
            st.Add(n);
        }
        fp.Sort(); st.Sort();
        return (fp[fp.Count / 2], fp[0], dead, st[st.Count / 2]);
    }

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 幼童口粮的修法（{seeds} 种子 × 30 年）\n");
        sb.AppendLine("杠杆：**10 岁以下（没装童工）给多少产出**。0 = 当前构建。\n");
        sb.AppendLine("| 10 岁以下产出 | 只装幼童口粮 末人口/灭村 | 生育系 末人口/灭村 | 生育系+童工 末人口/灭村 | 默认三法 末人口/灭村 |");
        sb.AppendLine("|---|---|---|---|---|");

        foreach (int w in new[]{ 0, 10, 15, 20, 25, 33 })
        {
            Cfg.LabUnder10W = w;
            var a = One(seeds, new[]{9});
            var b = One(seeds, new[]{8, 9, 12});
            var c = One(seeds, new[]{8, 9, 12, 39});
            var e = One(seeds, new[]{1, 5, 3});
            sb.AppendLine($"| {(w == 0 ? "0（现在）" : w + "%")} "
                        + $"| {a.med} / {a.dead * 100.0 / seeds:F0}% "
                        + $"| {b.med} / {b.dead * 100.0 / seeds:F0}% "
                        + $"| {c.med} / {c.dead * 100.0 / seeds:F0}% "
                        + $"| {e.med} / {e.dead * 100.0 / seeds:F0}% |");
        }

        Cfg.LabUnder10W = 0;
        File.WriteAllText("scan24-childfix.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan24-childfix.txt");
    }

    /// <summary>逐年打印，看村子是怎么死的。</summary>
    public static void Probe(int seed)
    {
        foreach (var ids in new[]{ new[]{9}, new[]{1,5,3} })
        {
            var d = NewGame(seed, ids);
            Console.WriteLine("=== 法条 " + string.Join(",", ids) + " seed=" + seed + " ===");
            for (int y = 0; y < 30; y++)
            {
                var rep = VillageLifecycleService.Advance(d, 2920);
                int pop = VillageLifecycleService.CountAlive(d);
                long h = 0; int kids = 0, adults = 0;
                for (int i = 0; i < d.villagerAlive.Length; i++)
                    if (d.villagerAlive[i])
                    {
                        h += d.villagerHunger[i];
                        if (d.villagerAges[i] < 16) kids++; else adults++;
                    }
                Console.WriteLine($"y{y+1:00} pop={pop:3} 粮仓={d.granaryFood:3} 饥饿={(pop>0?h/pop:0):2} 小孩={kids:2} 成人={adults:2} 生={rep.births} 死={rep.deaths}");
                if (pop == 0) break;
            }
        }
    }
}
