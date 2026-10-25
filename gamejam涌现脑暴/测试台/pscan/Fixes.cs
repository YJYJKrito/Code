using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>三个资源"接管子"的第二轮：把上一轮暴露出来的问题定量化。</summary>
public static class Fixes
{
    const int YEARS = 30;

    static VillageSaveData NewGame(int seed, int[] ids) {
        var d = Bootstrap.CreateNewGame(0, seed * 7919 + 13);
        for (int k = 0; k < d.lawIds.Length; k++) { d.lawIds[k] = 0; d.lawNames[k] = null; }
        for (int k = 0; k < ids.Length && k < d.lawIds.Length; k++) {
            d.lawIds[k] = ids[k];
            d.lawNames[k] = LawCatalog.FindById(ids[k])?.name;
        }
        return d;
    }

    // 跑一局，返回 (末威信, 顶300的采样占比, 末税库, 税库峰值)
    static (int pre, double peak, int tre, int trePeak) One(int seed, int[] ids) {
        var d = NewGame(seed, ids);
        int peak = 0, n = 0, trePeak = 0;
        for (int y = 0; y < YEARS; y++) {
            for (int m = 0; m < 12; m++) {
                VillageLifecycleService.Advance(d, 240);
                n++; if (d.prestige >= 300) peak++;
                if (d.treasuryGold > trePeak) trePeak = d.treasuryGold;
            }
            if (VillageLifecycleService.CountAlive(d) == 0) break;
        }
        return (d.prestige, n > 0 ? peak * 100.0 / n : 0, d.treasuryGold, trePeak);
    }

    public static void Run2(int seeds) {
        var sb = new StringBuilder();
        sb.AppendLine($"# 三个资源「接管子」· 第二轮实测（{seeds} 种子 × 30 年）\n");

        // ══════ ② 税库：找一个合适的档 ══════
        sb.AppendLine("## ② 税库周期进项 —— 上一轮所有档都太猛，这次往小扫\n");
        sb.AppendLine("设计目标：30 年攒到 **100–300**（够买两条稀有法 40+60，还要有点余量）\n");
        sb.AppendLine("| 周期 | 进项 | 30 年末 税库 最小/中位/最大 | 峰值中位 |");
        sb.AppendLine("|---|---|---|---|");
        var tvar = new (string, int period, int div)[]{
            ("每 240 tick", 240, 16), ("每 240 tick", 240, 32), ("每 240 tick", 240, 64),
            ("每 2400 tick", 2400, 4), ("每 2400 tick", 2400, 8), ("每 2400 tick", 2400, 16),
        };
        foreach (var (nm, period, div) in tvar) {
            Cfg.TreasuryPeriod = period; Cfg.TreasuryPopDiv = div;
            var t = new List<int>(); var pk = new List<int>();
            for (int s = 1; s <= seeds; s++) { var r = One(s, new[]{1,5,3}); t.Add(r.tre); pk.Add(r.trePeak); }
            t.Sort(); pk.Sort();
            sb.AppendLine($"| {nm} | 人口/{div} | {t[0]}/{t[t.Count/2]}/{t[t.Count-1]} | {pk[pk.Count/2]} |");
        }
        Cfg.TreasuryPeriod = 240; Cfg.TreasuryPopDiv = 0;

        // ══════ ③ 威信：固定费 vs 人口相对费 ══════
        sb.AppendLine("\n## ③ 威信维持费 —— 固定值 vs 人口相对\n");
        sb.AppendLine("现状 `−3 × 法条数`（固定）：人口涨上去之后收入碾压维持费，**95% 顶 300**");
        sb.AppendLine("改成 `− 法条数 × 人口/N`（人口相对）之后，收支才同量纲\n");
        sb.AppendLine("| 维持费 · 恐惧项 | 顶300占比 | 末威信 中位 | 归零占比 |");
        sb.AppendLine("|---|---|---|---|");
        var pvar = new (string, int perLaw, int fMid, int fDiv)[]{
            ("现状：3/条 · max(0,30−恐惧)/10", 3, 30, 10),
            ("4/条 · max(0,30−恐惧)/10",      4, 30, 10),
            ("3/条 · (12−恐惧)/2  有符号",     3, 12,  2),
            ("4/条 · (12−恐惧)/2  有符号",     4, 12,  2),
            ("3/条 · (15−恐惧)/3  有符号",     3, 15,  3),
            ("4/条 · (15−恐惧)/3  有符号",     4, 15,  3),
        };
        foreach (var (nm, pl, fmid, fd) in pvar) {
            Cfg.PrestigePerLaw = pl; Cfg.PrestigePerLawDiv = 0;
            Cfg.PrestigeFearBase = fmid; Cfg.PrestigeFearDiv = fd;
            var pre = new List<int>(); double peak = 0; int susp = 0;
            var lowFear = new List<int>(); var hiFear = new List<int>();
            foreach (var ids in new[]{ new[]{1,5,3}, new[]{1,5,3,9,12,71} }) {
                for (int s = 1; s <= seeds; s++) {
                    var r = One(s, ids); pre.Add(r.pre); peak += r.peak;
                    if (r.pre <= 0) susp++;
                }
            }
            pre.Sort();
            sb.AppendLine($"| {nm} | **{peak/(seeds*2):F0}%** | {pre[pre.Count/2]} | {susp*100.0/(seeds*2):F0}% |");
        }
        Cfg.PrestigePerLaw = 3; Cfg.PrestigePerLawDiv = 0;
        Cfg.PrestigeFearBase = 30; Cfg.PrestigeFearDiv = 10; Cfg.PrestigeCapV = 300;
        Cfg.PrestigePerLaw = 3; Cfg.PrestigePerLawDiv = 0; Cfg.PrestigeFearDiv = 10; Cfg.PrestigeCapV = 300;
        Cfg.PrestigePerLaw = 3; Cfg.PrestigePerLawDiv = 0; Cfg.PrestigeCapV = 300;

        sb.AppendLine("\n## ① 恐惧门槛 —— 上一轮结论\n");
        sb.AppendLine("缩放 **50%**（门槛 50/60/65/70/80/85 → **25/30/32/35/40/42**）时，");
        sb.AppendLine("那 10 条被挡住的法条里 **5 条**开始有影响（原样 0 条）。缩到 40% 没有更好。");

        File.WriteAllText("scan6-fixes.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan6-fixes.txt");
    }
}
