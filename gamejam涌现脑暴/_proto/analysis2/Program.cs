// 反方实验台 2（干净版）：三张显式法表对照 + 方差分解 + 死条率 + 恐惧专诊
//   ORIG   : 最初版（病灶）阈值
//   CUR    : 当前 refimpl 里的表（平行编辑者的重定向版）
//   NOLAW  : 全空槽
// 全部显式传表，绝不依赖静态 PRESETS，杜绝上一次的静态泄漏。
using System;
using System.Text;
using SimLit;

class Analysis2
{
    const int NSEED = 20;
    static int[][] Subs = Build();
    static int[][] Build()
    {
        var a = new System.Collections.Generic.List<int[]>();
        for (int i = 0; i < 8; i++) for (int j = i + 1; j < 8; j++) for (int k = j + 1; k < 8; k++)
        for (int l = k + 1; l < 8; l++) for (int m = l + 1; m < 8; m++) a.Add(new[] { i, j, k, l, m });
        return a.ToArray();
    }
    static readonly int[][] ORIG = {
        new[]{1,99,0,8,0}, new[]{3,40,1,12,0}, new[]{0,50,4,12,0}, new[]{0,40,2,20,0},
        new[]{1,99,8,20,0}, new[]{2,25,3,15,0}, new[]{2,50,7,25,0}, new[]{4,0,5,0,0} };
    static readonly int[][] CUR = {
        new[]{1,99,0,8,0}, new[]{3,40,1,12,0}, new[]{0,45,4,12,0}, new[]{0,40,2,20,0},
        new[]{1,99,8,20,0}, new[]{2,20,3,15,0}, new[]{2,30,7,25,0}, new[]{4,0,5,0,0} };

    static double[] Feat(World w) { var s = w.Stat(); return new double[] { w.AliveCount(), s.mg, s.mh, s.mf, s.sg, s.sh, s.sf, w.deaths }; }
    static void One(ulong seed, int[] ls, int[][] pr, out int[] fire, out double[] feat)
    {
        var w = new World(seed, ls, pr);
        for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
        fire = new int[World.SLOTS]; feat = Feat(w);
        for (int s = 0; s < World.SLOTS; s++) fire[s] = w.lawHits[s];
    }

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        int L = Subs.Length;
        Console.WriteLine($"法组 {L} × seed {NSEED}；当前 FATIGUE_K={World.FATIGUE_K}，FEAR_HUNGER_DIV={World.FEAR_HUNGER_DIV}\n");

        foreach (var pair in new[] { new[] { "ORIG", "0" }, new[] { "CUR", "1" } })
        {
            var pr = pair[0] == "ORIG" ? ORIG : CUR;
            // 全跑，缓存
            int[] fc = new int[NSEED * L * 5]; double[] xc = new double[NSEED * L * 8];
            for (int si = 0; si < NSEED; si++) for (int li = 0; li < L; li++)
            { int[] fr; double[] ft; One((ulong)si, Subs[li], pr, out fr, out ft); for (int s = 0; s < 5; s++) fc[(si * L + li) * 5 + s] = fr[s]; for (int k = 0; k < 8; k++) xc[(si * L + li) * 8 + k] = ft[k]; }

            // 全局
            double al = 0, mg = 0, mf = 0, sg = 0, sf = 0; int n = 0;
            for (int k = 0; k < NSEED * L; k++) { al += xc[k * 8]; mg += xc[k * 8 + 1]; mf += xc[k * 8 + 3]; sg += xc[k * 8 + 4]; sf += xc[k * 8 + 6]; n++; }

            // 方差分解：由「选哪 5 条」解释的比例
            Console.WriteLine($"══════ 表 {pair[0]} ══════");
            Console.WriteLine($"  全局：存活 {al / n:F1}/20  均金 {mg / n:F1}  均恐 {mf / n:F1}  σ金 {sg / n:F1}  σ恐 {sf / n:F1}");
            Console.Write("  立法解释的方差占比：");
            for (int f = 0; f < 7; f++)
            {
                double grand = 0; for (int k = 0; k < NSEED * L; k++) grand += xc[k * 8 + f]; grand /= (NSEED * L);
                double sst = 0; for (int k = 0; k < NSEED * L; k++) { double d = xc[k * 8 + f] - grand; sst += d * d; }
                double ssl = 0;
                for (int li = 0; li < L; li++) { double m2 = 0; for (int si = 0; si < NSEED; si++) m2 += xc[(si * L + li) * 8 + f]; m2 /= NSEED; ssl += (m2 - grand) * (m2 - grand); }
                ssl *= NSEED;
                string[] nm = { "存活", "均金", "均饥饿", "均恐惧", "σ金", "σ饥饿", "σ恐" };
                Console.Write($"{nm[f]} {(sst > 1e-9 ? 100 * ssl / sst : 0):F0}%  ");
            }
            Console.WriteLine();

            // 每条法：平均命中 + 零命中局占比
            Console.Write("  各法零命中率: ");
            for (int p = 0; p < 8; p++)
            {
                int z = 0, r = 0, hsum = 0;
                for (int k = 0; k < NSEED * L; k++) { int li = k % L; int ix = Array.IndexOf(Subs[li], p); if (ix < 0) continue; int v = fc[k * 5 + ix]; hsum += v; if (v == 0) z++; r++; }
                Console.Write($"P{p}:{100.0 * z / r:F0}%(均{hsum / r}) ");
            }
            Console.WriteLine("\n");
        }

        // 恐惧专诊（CUR 表）
        Console.WriteLine("══════ 恐惧专诊（CUR 表，全村民·全 tick）══════");
        long fsum = 0, fcnt = 0, f0 = 0, fgt20 = 0, fgt35 = 0, fgt50 = 0, fhk = 0, allk = 0; int fmax = 0;
        var hist = new int[100];
        for (int si = 0; si < NSEED; si++) foreach (var ls in Subs)
        {
            var w = new World((ulong)si, ls, CUR);
            for (int t = 0; t < World.MAXT; t++)
            {
                w.Tick();
                for (int i = 0; i < World.N; i++) if (w.v[i].alive)
                { int fr = w.v[i].fear; fsum += fr; fcnt++; hist[fr]; if (fr == 0) f0++; if (fr > 20) fgt20++; if (fr > 35) fgt35++; if (fr > 50) fgt50++; if (fr > fmax) fmax = fr; }
                if (w.AliveCount() == 0) break;
            }
            fhk += w.fearDeaths; allk += w.deaths;
        }
        Console.WriteLine($"  均恐惧 {(double)fsum / fcnt:F1}  max {fmax}  fear=0 占 {100.0 * f0 / fcnt:F1}%");
        Console.WriteLine($"  fear>20 {100.0 * fgt20 / fcnt:F1}%   >35 {100.0 * fgt35 / fcnt:F1}%   >50 {100.0 * fgt50 / fcnt:F1}%");
        Console.Write("  直方(每5): "); for (int b = 0; b < 20; b++) { int s = 0; for (int k = b * 5; k < b * 5 + 5; k++) s += hist[k]; Console.Write($"{b * 5}:{100.0 * s / fcnt:F0}% "); }
        Console.WriteLine($"\n  fearDeaths/deaths = {fhk}/{allk}（应 <100%，现在若 =100% 是占位 bug）");

        // 疲劳扫描
        Console.WriteLine("\n══════ 疲劳阈值扫描（CUR 表）══════");
        foreach (int K in new[] { 100, 400, 100000 })
        {
            World.FATIGUE_K = K;
            double al = 0, sg = 0, sf = 0; int n = 0; long fat = 0;
            for (int si = 0; si < NSEED; si++) foreach (var ls in Subs)
            { int[] fr; double[] ft; One((ulong)si, ls, CUR, out fr, out ft); al += ft[0]; sg += ft[4]; sf += ft[6]; n++;
              var w = new World((ulong)si, ls, CUR); bool f = false; for (int t = 0; t < World.MAXT && !f; t++) { w.Tick(); for (int s = 0; s < 5; s++) if ((w.laws[s].flags & LawFlag.Fatigued) != 0) f = true; if (w.AliveCount() == 0) break; } if (f) fat++; }
            Console.WriteLine($"  K={K,7}: 疲劳局 {100.0 * fat / n:F0}%  存活 {al / n:F1}  σ金 {sg / n:F1}  σ恐 {sf / n:F1}");
        }
        World.FATIGUE_K = 100;
    }
}
