using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>开垦新机制：优先开荒 · 荒地双倍粮 · 花税库金币。扫金币量。</summary>
public static class Farmcheck
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
    static int[] Terrain(byte[] t) {
        var c = new int[8];
        foreach (var b in t) if (b < 8) c[b]++;
        return c;
    }
    static int Med(List<int> v) { v.Sort(); return v[v.Count/2]; }

    // 跑一局：返回 逐年耕地 / 荒地末 / 开垦次数 / 其中开荒 / 末税库 / 税库峰值 / 末人口 / 人口峰值
    static (int[] by, int wEnd, long till, long tillW, int tre, int trePeak, int pop, int peak) One(int seed, int[] ids) {
        var d = NewGame(seed, ids);
        var by = new int[31];
        Cfg.TillCount = 0; Cfg.TillWasteCount = 0;
        int trePeak = 0, peak = 0;
        for (int y = 1; y <= YEARS; y++) {
            VillageLifecycleService.Advance(d, 2920);
            by[y] = Farmland.CountFarmland(d);
            if (d.treasuryGold > trePeak) trePeak = d.treasuryGold;
            int a = VillageLifecycleService.CountAlive(d);
            if (a > peak) peak = a;
            if (a == 0) break;
        }
        return (by, Terrain(d.terrain)[4], Cfg.TillCount, Cfg.TillWasteCount, d.treasuryGold, trePeak,
                VillageLifecycleService.CountAlive(d), peak);
    }

    public static void Run(int seeds) {
        var sb = new StringBuilder();
        sb.AppendLine($"# 开垦新机制 · 扫描（{seeds} 种子 × 30 年）\n");
        sb.AppendLine("规则：**优先开垦荒地** · 草地 15 粮 · 荒地 30 粮（2×）· 都花税库金币");
        sb.AppendLine("上限：耕地 < 开局基线 × 3（≈ 246 格）");
        sb.AppendLine("税库进项：每 2400 tick += 人口/8（来自《三个资源接通》§2）\n");

                sb.AppendLine("## ★ 开垦成本扫描（进项固定：每 2400 tick += 人口/2，30 年≈720）");
        sb.AppendLine("");
        sb.AppendLine("| 草地金 | 荒地金 | 草地粮 | 荒地粮 | 末耕地 | 荒地减少 | 开垦次数 | 其中开荒 | 末税库 | 税库达60的年份 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        var cv = new (int g, int gw, int f, int fw)[]{
            (0,0,15,30), (1,2,15,30), (2,4,15,30), (3,6,15,30), (2,4,10,20), (1,2,10,20), (0,0,10,20),
        };
        foreach (var (g, gw, fg, fw2) in cv) {
            Cfg.TillWasteFirst = 1; Cfg.TillGoldGrass = g; Cfg.TillGoldWaste = gw;
            Cfg.TillGrainGrass = fg; Cfg.TillGrainWaste = fw2;
            Cfg.TillK = 3; Cfg.TreasuryPeriod = 2400; Cfg.TreasuryPopDiv = 2;
            var f=new List<int>(); var w=new List<int>(); var dw=new List<int>();
            var t=new List<int>(); var tw=new List<int>(); var tr=new List<int>(); var y60=new List<int>();
            for (int s=1;s<=seeds;s++) {
                var d = NewGame(s, new[]{71,5,12});
                Cfg.TillCount=0; Cfg.TillWasteCount=0;
                int hit = 0;
                for (int y=1;y<=YEARS;y++) {
                    VillageLifecycleService.Advance(d, 2920);
                    if (hit==0 && d.treasuryGold >= 60) hit = y;
                    if (VillageLifecycleService.CountAlive(d)==0) break;
                }
                f.Add(Farmland.CountFarmland(d)); w.Add(Terrain(d.terrain)[4]); dw.Add(743 - Terrain(d.terrain)[4]);
                t.Add((int)Math.Min(int.MaxValue, Cfg.TillCount)); tw.Add((int)Math.Min(int.MaxValue, Cfg.TillWasteCount));
                tr.Add(d.treasuryGold); y60.Add(hit);
            }
            sb.AppendLine($"| {g} | {gw} | {fg} | {fw2} | {Med(f)} | **-{Med(dw)}** | {Med(t)} | **{Med(tw)}** | {Med(tr)} | 第 {Med(y60)} 年 |");
        }
        Cfg.TillWasteFirst=0; Cfg.TillGoldGrass=0; Cfg.TillGoldWaste=0; Cfg.TillGrainGrass=15; Cfg.TillGrainWaste=15; Cfg.TillK=0;
        Cfg.TreasuryPopDiv = 0; Cfg.TreasuryPeriod = 240;

// 逐年轨迹（挑一个推荐档）
        sb.AppendLine("\n## 逐年轨迹（推荐档：草地 2 金 / 荒地 4 金）\n");
        Cfg.TillWasteFirst = 1; Cfg.TillGoldGrass = 2; Cfg.TillGoldWaste = 4; Cfg.TillK = 3;
        Cfg.TreasuryPeriod = 2400; Cfg.TreasuryPopDiv = 8;
        sb.AppendLine("| 年 | 耕地 | 荒地 | 税库 |");
        sb.AppendLine("|---|---|---|---|");
        var byF = new List<int>[31]; var byW = new List<int>[31]; var byT = new List<int>[31];
        for (int k=0;k<31;k++) { byF[k]=new List<int>(); byW[k]=new List<int>(); byT[k]=new List<int>(); }
        for (int s=1;s<=seeds;s++) {
            var d = NewGame(s, new[]{71,5,12});
            for (int y=1;y<=YEARS;y++) {
                VillageLifecycleService.Advance(d, 2920);
                if (y==1||y==3||y==5||y==10||y==15||y==20||y==25||y==30) {
                    byF[y].Add(Farmland.CountFarmland(d)); byW[y].Add(Terrain(d.terrain)[4]); byT[y].Add(d.treasuryGold);
                }
                if (VillageLifecycleService.CountAlive(d)==0) break;
            }
        }
        foreach (int y in new[]{1,3,5,10,15,20,25,30})
            sb.AppendLine($"| {y} | {Med(byF[y])} | {Med(byW[y])} | {Med(byT[y])} |");
        Cfg.TillWasteFirst = 0; Cfg.TillGoldGrass = 0; Cfg.TillGoldWaste = 0; Cfg.TillK = 0;
        Cfg.TreasuryPopDiv = 0; Cfg.TreasuryPeriod = 240;

        // 平衡
        sb.AppendLine("\n## 平衡影响（推荐档）\n");
        Cfg.TillWasteFirst = 1; Cfg.TillGoldGrass = 2; Cfg.TillGoldWaste = 4; Cfg.TillK = 3;
        Cfg.TreasuryPeriod = 2400; Cfg.TreasuryPopDiv = 8;
        int alive = 0;
        sb.AppendLine("| 法条组 | 末人口 最小/中位/最大 | 末耕地 | 末荒地 | 末税库 |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var (nm, ids) in new (string,int[])[]{
            ("不装法", new int[0]), ("默认", new[]{1,5,3}), ("生育系", new[]{8,9,12}),
            ("开垦系", new[]{71,5,12}), ("苛政", new[]{54,39,3}), ("恐惧系", new[]{4,7,16}),
            ("满槽6条", new[]{1,5,3,9,12,71}) }) {
            var fp=new List<int>(); var ff=new List<int>(); var fw=new List<int>(); var ft=new List<int>();
            for (int s=1;s<=seeds;s++) {
                var r = One(s, ids);
                fp.Add(r.pop); ff.Add(r.by[30]); fw.Add(r.wEnd); ft.Add(r.tre);
                if (r.pop >= 5) alive++;
            }
            fp.Sort();
            sb.AppendLine($"| {nm} | {fp[0]}/{fp[fp.Count/2]}/{fp[fp.Count-1]} | {Med(ff)} | {Med(fw)} | {Med(ft)} |");
        }
        sb.AppendLine($"\n（{alive} / {seeds*7} 局活下来）");
        Cfg.TillWasteFirst = 0; Cfg.TillGoldGrass = 0; Cfg.TillGoldWaste = 0; Cfg.TillK = 0;
        Cfg.TreasuryPopDiv = 0; Cfg.TreasuryPeriod = 240;

        File.WriteAllText("scan10-till.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan10-till.txt");
    }
}
