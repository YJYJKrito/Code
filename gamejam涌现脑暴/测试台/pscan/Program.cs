using System; using System.Collections.Generic; using System.IO; using System.Text;

public static class Program
{
    static readonly (string,int[])[] SETS = {
        ("不装法",   new int[0]),
        ("默认",     new[]{1,5,3}),
        ("生育系",   new[]{8,9,12}),
        ("开垦系",   new[]{71,5,12}),
        ("苛政",     new[]{54,39,3}),
        ("恐惧系",   new[]{4,7,16}),
        ("流民法",   new[]{17}),
        ("流民+开垦",new[]{17,71}),
        ("满槽6条",  new[]{1,5,3,9,12,71}),
        ("幼童口粮单条", new[]{9}),
        ("幼童+童工",   new[]{9,39}),
        ("生育系+童工", new[]{8,9,12,39}),
        ("童工单条",    new[]{39}),
    };
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
    static int RunToEnd(VillageSaveData d) {
        for (int y = 0; y < YEARS; y++) {
            VillageLifecycleService.Advance(d, 2920);
            if (VillageLifecycleService.CountAlive(d) == 0) break;
        }
        return VillageLifecycleService.CountAlive(d);
    }

    // ────────── 扫描 1：参数活动范围 ──────────
    static void ScanParams(int seeds) {
        var sb = new StringBuilder();
        var names = new[]{"人口","粮仓","威信","税库","讣告数","累计出生","累计死亡","耕地格数",
                          "平均饥饿","最高饥饿","平均恐惧","最高恐惧","平均金币","最高金币","平均私粮","平均年龄","子女总数"};
        var mins = new long[names.Length]; var maxs = new long[names.Length];
        var sums = new double[names.Length]; long n = 0;
        var floors = new long[]{-1,0,0,-1,-1,-1,-1,-1,0,0,0,0,0,0,0,-1,-1};
        var ceils  = new long[]{120,500,300,-1,-1,-1,-1,-1,99,99,99,99,99,99,40,-1,-1};
        var touchF = new long[names.Length]; var touchC = new long[names.Length];
        for (int i=0;i<names.Length;i++){mins[i]=long.MaxValue;maxs[i]=long.MinValue;}
        int diedOut = 0; var finals = new List<int>();
        for (int s = 1; s <= seeds; s++) {
            var d = NewGame(s, new[]{1,5,3});
            for (int y = 0; y < YEARS; y++) {
                for (int m = 0; m < 12; m++) {
                    VillageLifecycleService.Advance(d, 240);
                    int alive = VillageLifecycleService.CountAlive(d);
                    long hs=0,fs=0,gs=0,fos=0,as_=0,ch=0; int a=0, mh=0, mf=0, mg=0;
                    for (int i=0;i<d.villagerAlive.Length;i++) if (d.villagerAlive[i]) {
                        hs+=d.villagerHunger[i]; if(d.villagerHunger[i]>mh)mh=d.villagerHunger[i];
                        fs+=d.villagerFear[i];   if(d.villagerFear[i]>mf)mf=d.villagerFear[i];
                        gs+=d.villagerGold[i];   if(d.villagerGold[i]>mg)mg=d.villagerGold[i];
                        fos+=d.villagerFood[i]; as_+=d.villagerAges[i]; ch+=d.villagerChildren[i]; a++;
                    }
                    if (a==0) a=1;
                    var vals = new long[]{alive, d.granaryFood, d.prestige, d.treasuryGold, d.obituaryCount,
                        d.totalBirths, d.totalDeaths, Farmland.CountFarmland(d),
                        hs/a, mh, fs/a, mf, gs/a, mg, fos/a, as_/a, ch};
                    for (int i=0;i<names.Length;i++) {
                        long v = vals[i];
                        if (v<mins[i]) mins[i]=v; if (v>maxs[i]) maxs[i]=v;
                        sums[i]+=v;
                        if (floors[i]>=0 && v<=floors[i]) touchF[i]++;
                        if (ceils[i]>=0  && v>=ceils[i])  touchC[i]++;
                    }
                    n++;
                    if (alive==0) break;
                }
                if (VillageLifecycleService.CountAlive(d)==0) break;
            }
            int f = VillageLifecycleService.CountAlive(d); finals.Add(f); if (f==0) diedOut++;
        }
        finals.Sort();
        sb.AppendLine($"# 扫描 1 · 参数活动范围（默认三法 · {seeds} 种子 × 30 年 · 每月采样 {n} 点）\n");
        sb.AppendLine($"30 年末人口：最小 {finals[0]} · 中位 {finals[finals.Count/2]} · 最大 {finals[finals.Count-1]} · 灭村 {diedOut*100.0/seeds:F0}%\n");
        sb.AppendLine("| 参数 | 最小 | 最大 | 平均 | 触底 | 触顶 | 判定 |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        for (int i=0;i<names.Length;i++) {
            bool flat = (maxs[i]-mins[i]) <= Math.Max(1, maxs[i]/20);
            string verdict = flat ? "**★ 基本不变**" : (touchC[i]*100.0/n > 30 ? "⚠ 常顶上限" : "会动");
            string fb = floors[i]<0?"—":$"{touchF[i]*100.0/n:F0}%";
            string cb = ceils[i]<0 ?"—":$"{touchC[i]*100.0/n:F0}%";
            sb.AppendLine($"| {names[i]} | {mins[i]} | {maxs[i]} | {sums[i]/n:F1} | {fb} | {cb} | {verdict} |");
        }
        Out("scan1-params.txt", sb);
    }

    // ────────── 扫描 2：法条组对比 ──────────
    static void ScanLawSets(int seeds) {
        var sb = new StringBuilder();
        sb.AppendLine($"# 扫描 2 · 法条组对比（{seeds} 种子 × 30 年）\n");
        sb.AppendLine("**「活下来」= 末人口 >= 5。这是最关键的一栏。**\n");
        sb.AppendLine("| 法条组 | 末人口 最小/中位/最大 | 灭村率 | 末人口=1 占比 | 全程最低人口 中位 |");
        sb.AppendLine("|---|---|---|---|---|");
        int aliveSets = 0;
        foreach (var (nm, ids) in SETS) {
            var fp = new List<int>(); var lo = new List<int>(); int dead=0, one=0;
            for (int s = 1; s <= seeds; s++) {
                var d = NewGame(s, ids);
                int low = 999;
                for (int y = 0; y < YEARS; y++) {
                    VillageLifecycleService.Advance(d, 2920);
                    int a = VillageLifecycleService.CountAlive(d); if (a<low) low=a;
                    if (a==0) break;
                }
                int fin = VillageLifecycleService.CountAlive(d);
                fp.Add(fin); lo.Add(low); if (fin==0) dead++; if (fin==1) one++;
            }
            fp.Sort(); lo.Sort();
            if (fp[fp.Count/2] >= 5) aliveSets++;
            sb.AppendLine($"| {nm} | {fp[0]}/{fp[fp.Count/2]}/{fp[fp.Count-1]} | {dead*100.0/seeds:F0}% | {one*100.0/seeds:F0}% | {lo[lo.Count/2]} |");
        }
        sb.AppendLine($"\n## ★ 活下来的法条组：**{aliveSets} / {SETS.Length}**");
        Out("scan2-lawsets.txt", sb);
    }

    // ────────── 扫描 3：单条法有效性 ──────────
    static void ScanSingle(int seeds) {
        var sb = new StringBuilder();
        sb.AppendLine($"# 扫描 3 · 单条法各自运行（{seeds} 种子 × 30 年）\n");
        sb.AppendLine("**基线 = 不装任何法。任何一条法与基线相同 = 这条法是摆设。**\n");
        int baseMin, baseMed, baseMax;
        {
            var fp = new List<int>();
            for (int s=1;s<=seeds;s++) fp.Add(RunToEnd(NewGame(s, new int[0])));
            fp.Sort();
            baseMin = fp[0]; baseMed = fp[fp.Count/2]; baseMax = fp[fp.Count-1];
        }
        var ids = new int[]{1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,26,27,28,29,30,31,33,37,38,39,50,51,52,54,58,59,71};
        sb.AppendLine("| 法条 | 末人口 最小/中位/最大 | 灭村率 | 与基线不同？ |");
        sb.AppendLine("|---|---|---|---|");
        int effective = 0;
        foreach (int lid in ids) {
            var fp = new List<int>(); int dead=0;
            for (int s=1;s<=seeds;s++) {
                var d = NewGame(s, new[]{lid});
                int f = RunToEnd(d); fp.Add(f); if (f==0) dead++;
            }
            fp.Sort();
            bool diff = !(fp[0]==baseMin && fp[fp.Count/2]==baseMed && fp[fp.Count-1]==baseMax);
            if (diff) effective++;
            string nm = LawCatalog.FindById(lid)?.name ?? ("id"+lid);
            sb.AppendLine($"| {lid} {nm} | {fp[0]}/{fp[fp.Count/2]}/{fp[fp.Count-1]} | {dead*100.0/seeds:F0}% | {(diff?"✅ 有影响":"❌ 与不装法相同")} |");
        }
        sb.AppendLine($"\n## ★ 有效的法条：**{effective} / {ids.Length}**");
        sb.AppendLine($"\n（基线「不装任何法」= {baseMin}/{baseMed}/{baseMax}。与这三个数**完全相同的判为「无影响」**）");
        Out("scan3-single.txt", sb);
    }

    // ────────── 扫描 4：逐年机制插桩 ──────────
    static void ScanYearly(int seed) {
        var sb = new StringBuilder();
        sb.AppendLine($"# 扫描 4 · 逐年机制插桩（种子 {seed}）\n");
        foreach (var (nm, ids) in new (string,int[])[]{
            ("不装法", new int[0]), ("默认", new[]{1,5,3}) }) {
            sb.AppendLine($"\n## {nm}\n");
            sb.AppendLine("| 年 | 人口 | 生 | 死 | **死因** | 粮仓 | 产出/56t | 公共消耗 | 均饥饿 | 最高饥饿 | **合格母亲** | 耕地 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            var d = NewGame(seed, ids);
            int pb=0, pd=0, pOb=0;
            for (int y=1;y<=25;y++) {
                VillageLifecycleService.Advance(d, 2920);
                int alive = VillageLifecycleService.CountAlive(d);
                var cnt = new Dictionary<string,int>();
                for (int i=pOb;i<d.obituaryCount && i<d.obituaryCauses.Length;i++) {
                    string c = d.obituaryCauses[i] ?? "?";
                    cnt[c] = cnt.ContainsKey(c) ? cnt[c]+1 : 1;
                }
                pOb = d.obituaryCount;
                var parts = new List<string>(); foreach (var kv in cnt) parts.Add($"{kv.Key}{kv.Value}");
                long hs=0; int a=0, hm=0, mothers=0;
                for (int i=0;i<d.villagerAlive.Length;i++) if (d.villagerAlive[i]) {
                    hs+=d.villagerHunger[i]; a++; if(d.villagerHunger[i]>hm)hm=d.villagerHunger[i];
                    if (d.villagerGenders[i]==0 && d.villagerAges[i]>=18 && d.villagerAges[i]<=42
                        && d.villagerBirthCooldown[i]<=0 && d.villagerChildren[i]<Cfg.BirthChildCap
                        && d.villagerHunger[i]<60 && d.villagerFear[i]<70) mothers++;
                }
                if (a==0) a=1;
                sb.AppendLine($"| {y} | {alive} | +{d.totalBirths-pb} | -{d.totalDeaths-pd} | {string.Join(" ",parts)} | {d.granaryFood} | {d.lastProduction} | {d.lastConsumption} | {hs/a:F0} | {hm} | **{mothers}** | {Farmland.CountFarmland(d)} |");
                pb=d.totalBirths; pd=d.totalDeaths;
                if (alive==0) { sb.AppendLine("| — | **0** | | | **灭村** | | | | | | | |"); break; }
            }
        }
        Out("scan4-yearly.txt", sb);
    }


    // ────────── 扫描 5：三个资源的分布（给新机制定阈值用）──────────
    static void ScanResources(int seeds) {
        var sb = new StringBuilder();
        sb.AppendLine($"# 扫描 5 · 恐惧 / 税库 / 威信 的分布（{seeds} 种子 × 30 年）");

        // ── 恐惧：逐村民逐月采样，算分位 ──
        var fear = new List<int>();
        var fearAlive = new List<int>();
        foreach (var ids in new[]{ new[]{1,5,3}, new[]{4,7,16}, new[]{13,35,37,60,64} }) {
            for (int s=1;s<=seeds;s++) {
                var d = NewGame(s, ids);
                for (int y=0;y<YEARS;y++) {
                    for (int m=0;m<12;m++) {
                        VillageLifecycleService.Advance(d, 240);
                        for (int i=0;i<d.villagerAlive.Length;i++) if (d.villagerAlive[i]) {
                            fear.Add(d.villagerFear[i]);
                            if (d.villagerAlive[i]) fearAlive.Add(d.villagerFear[i]);
                        }
                    }
                    if (VillageLifecycleService.CountAlive(d)==0) break;
                }
            }
        }
        fear.Sort();
        int P(double q) => fear[(int)(fear.Count*q)];
        sb.AppendLine("## 恐惧（个人值，全部采样的合并分布）");
        sb.AppendLine($"采样点 {fear.Count}");
        sb.AppendLine("| 分位 | 值 |");
        sb.AppendLine("|---|---|");
        foreach (var q in new[]{0.05,0.25,0.50,0.75,0.90,0.95,0.99}) sb.AppendLine($"| p{q*100:F0} | {P(q)} |");
        sb.AppendLine($"| max | {fear[fear.Count-1]} |");

        // ── 威信 / 税库：按法条数分组 ──
        sb.AppendLine("");
        sb.AppendLine("## 威信 / 税库 / 恐惧（按装几条法分组）");
        sb.AppendLine("| 法条组 | 威信 最小/中位/最大 | 顶300占比 | 税库 最小/中位/最大 | 平均恐惧 |");
        sb.AppendLine("|---|---|---|---|---|");
        var groups = new (string,int[])[]{
            ("2条", new[]{1,5}), ("3条", new[]{1,5,3}), ("4条", new[]{1,5,3,12}),
            ("5条", new[]{1,5,3,12,9}), ("6条", new[]{1,5,3,12,9,71}) };
        foreach (var (nm, ids) in groups) {
            var pr = new List<int>(); var tr = new List<int>(); long fsum=0; long fn=0; int peak=0;
            for (int s=1;s<=seeds;s++) {
                var d = NewGame(s, ids);
                for (int y=0;y<YEARS;y++) {
                    for (int m=0;m<12;m++) {
                        VillageLifecycleService.Advance(d, 240);
                        for (int i=0;i<d.villagerAlive.Length;i++) if (d.villagerAlive[i]) { fsum+=d.villagerFear[i]; fn++; }
                    }
                    if (VillageLifecycleService.CountAlive(d)==0) break;
                }
                pr.Add(d.prestige); tr.Add(d.treasuryGold);
                if (d.prestige >= 300) peak++;
            }
            pr.Sort(); tr.Sort();
            sb.AppendLine($"| {nm} | {pr[0]}/{pr[pr.Count/2]}/{pr[pr.Count-1]} | {peak*100.0/seeds:F0}% | {tr[0]}/{tr[tr.Count/2]}/{tr[tr.Count-1]} | {(fn>0?(double)fsum/fn:0):F1} |");
        }
        Out("scan5-resources.txt", sb);
    }

    static void Out(string file, StringBuilder sb) {
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));   // 写到当前目录
        Console.WriteLine($"  -> {file}");
    }

    public static void Main(string[] args) {
        try { Console.OutputEncoding = Encoding.UTF8; } catch {}
        string what = args.Length > 0 ? args[0] : "all";
        int seeds = args.Length > 1 ? int.Parse(args[1]) : 20;
        Console.WriteLine($"scan = {what}   seeds = {seeds}");
        if (what=="params" || what=="all") ScanParams(what=="all" ? 120 : seeds);
        if (what=="lawsets"|| what=="all") ScanLawSets(seeds);
        if (what=="single" || what=="all") ScanSingle(seeds);
        if (what=="yearly" || what=="all") ScanYearly(7929);
        if (what=="res"    || what=="all") ScanResources(seeds);
        if (what=="fixes"  || what=="all") Fixes.Run2(seeds);
        if (what=="farm"   || what=="all") Farmcheck.Run(seeds);
        if (what=="profdiag") ProfDiag.Run(seeds);
        if (what=="resfix") ResFix.Run(seeds);
        if (what=="labor") Labor.Run(seeds);
        if (what=="famine") Famine.Run(seeds);
        if (what=="famineconfirm") Famine.Confirm(seeds);
        if (what=="treasury") Treasury.Run(seeds);
        if (what=="foodevent") FoodEvent.Run(seeds);
        if (what=="native") Native.Run(seeds);
        if (what=="childfix") ChildFix.Run(seeds);
        if (what=="bugcheck") BugCheck.Run(seeds);
        if (what=="evlaw") EvLaw.Run(seeds);
        if (what=="chain") EvLaw.Chain(seeds);
        if (what=="audit") Audit.Run(seeds);
        if (what=="score") Score.Run(seeds);
        if (what=="scoreset") Score.BySet(seeds);
        if (what=="modes") Modes.Run(seeds);
        if (what=="balance") Balance.Run(seeds);
        if (what=="obit") Obit.Run(seeds);
        if (what=="mostlethal") Obit.MostLethal(seeds);
        if (what=="runmode") RunMode.Run(seeds);
        if (what=="tinytest") RunMode.TestTiny(seeds);
        if (what=="rising") Balance.Rising(seeds, 0);
        if (what=="childprobe") ChildFix.Probe(seeds);
        Console.WriteLine("done");
    }
}
