// ═══════════════════════════════════════════════════════════════════
// 反方实验台：回答「立法到底有没有有效影响」
// 全部离线跑，纯整数模拟复用 refimpl/Sim。
//
// 设计要点：RNG 流只依赖 (seed, tick)，与律法无关（见 Rng.For）。
//   所以「同 seed、换一套法」造成的任何差异，100% 由法贡献。
//   这就是把「立法是否有效」变成一个可判定问题的钥匙。
// ═══════════════════════════════════════════════════════════════════
using System;
using System.Text;
using SimLit;

class Analysis
{
    const int NSEED = 40;

    static double[] Features(World w)
    {
        var s = w.Stat();
        return new double[] {
            w.AliveCount(),      // 0
            s.mg,                // 1 均金币
            s.mh,                // 2 均饥饿
            s.mf,                // 3 均恐惧
            s.sg,                // 4 σ金币
            s.sh,                // 5 σ饥饿
            s.sf,                // 6 σ恐惧
            w.deaths,            // 7 死亡数
            w.st.granary,        // 8 粮仓
        };
    }

    static readonly string[] FName = { "存活数", "均金币", "均饥饿", "均恐惧", "σ金币", "σ饥饿", "σ恐惧", "死亡数", "粮仓" };

    static World Run(ulong seed, int[] lawset, int maxTick, out int suspTicks, out int[] lawFire)
    {
        var w = new World(seed, lawset);
        suspTicks = 0; lawFire = new int[World.SLOTS];
        for (int t = 0; t < maxTick; t++)
        {
            w.Tick();
            if (w.st.suspendedAll) suspTicks++;
            if (w.AliveCount() == 0) break;
        }
        for (int s = 0; s < World.SLOTS; s++) lawFire[s] = w.laws[s].hitsTotal;
        return w;
    }

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        var all = new System.Collections.Generic.List<int[]>();
        for (int a = 0; a < 8; a++) for (int b = a + 1; b < 8; b++) for (int c = b + 1; c < 8; c++)
        for (int d = c + 1; d < 8; d++) for (int e = d + 1; e < 8; e++) all.Add(new[] { a, b, c, d, e });
        Console.WriteLine($"预设法组合数 C(8,5) = {all.Count}，seed 数 = {NSEED}，总跑数 = {all.Count * NSEED}");

        // ═══ 实验 1：方差分解 —— 结局方差里，多少由「选哪 5 条」解释，多少由 seed 解释 ═══
        int L = all.Count;
        var X = new double[NSEED * L][];
        for (int si = 0; si < NSEED; si++)
            for (int li = 0; li < L; li++)
            {
                int dummy1, dummy2; int[] d3;
                var w = Run((ulong)si, all[li], World.MAXT, out dummy1, out d3);
                X[si * L + li] = Features(w);
            }

        int NF = X[0].Length;
        Console.WriteLine("\n══════ 实验1：结局方差分解（40 seed × 56 法组）══════");
        Console.WriteLine($"{"特征",-8}{"总方差",12}{"由法解释",16}{"由seed解释",16}{"法占比",10}");
        for (int f = 0; f < NF; f++)
        {
            double grand = 0; for (int k = 0; k < X.Length; k++) grand += X[k][f]; grand /= X.Length;
            double sst = 0; for (int k = 0; k < X.Length; k++) { double d = X[k][f] - grand; sst += d * d; }
            // lawset 主效应
            double ssl = 0;
            for (int li = 0; li < L; li++)
            {
                double m = 0; for (int si = 0; si < NSEED; si++) m += X[si * L + li][f]; m /= NSEED;
                ssl += (m - grand) * (m - grand);
            }
            ssl *= NSEED;
            // seed 主效应
            double sss = 0;
            for (int si = 0; si < NSEED; si++)
            {
                double m = 0; for (int li = 0; li < L; li++) m += X[si * L + li][f]; m /= L;
                sss += (m - grand) * (m - grand);
            }
            sss *= L;
            double share = sst > 1e-9 ? ssl / sst : 0;
            Console.WriteLine($"{FName[f],-8}{sst,12:F0}{ssl,16:F0}{sss,16:F0}{share * 100,9:F1}%");
        }

        // ═══ 实验 2：单条法隔离效果 —— 每条法单独上，对比「无法」基线 ═══
        Console.WriteLine("\n══════ 实验2：单条法隔离效果（40 seed，[p,-1,-1,-1,-1] vs 全空）══════");
        double[] baseF = new double[NF];
        for (int si = 0; si < NSEED; si++)
        {
            int d1; int[] d3;
            var w = Run((ulong)si, new[] { -1, -1, -1, -1, -1 }, World.MAXT, out d1, out d3);
            var ff = Features(w); for (int f = 0; f < NF; f++) baseF[f] += ff[f];
        }
        for (int f = 0; f < NF; f++) baseF[f] /= NSEED;
        for (int p = 0; p < 8; p++)
        {
            double[] acc = new double[NF]; long hits = 0; int alive = 0;
            for (int si = 0; si < NSEED; si++)
            {
                int d1; int[] d3;
                var w = Run((ulong)si, new[] { p, -1, -1, -1, -1 }, World.MAXT, out d1, out d3);
                var ff = Features(w); for (int f = 0; f < NF; f++) acc[f] += ff[f];
                hits += w.lawHits[0]; alive += w.AliveCount();
            }
            for (int f = 0; f < NF; f++) acc[f] /= NSEED;
            Console.WriteLine($"  法{p}: 命中 {hits,7} (={(double)hits / NSEED:F0}/seed)  存活 {acc[0],5:F1}({(acc[0] - baseF[0]),+5:F1})  "
                + $"金 {acc[1],5:F1}({acc[1] - baseF[1],+5:F1})  饿 {acc[2],5:F1}({acc[2] - baseF[2],+5:F1})  "
                + $"恐 {acc[3],5:F1}({acc[3] - baseF[3],+5:F1})");
        }

        // ═══ 实验 3：饱和 / 疲劳 / 威信停摆 ═══
        Console.WriteLine($"\n══════ 实验3：饱和·疲劳·停摆（无法基线 40 seed）══════");
        long bGold99 = 0, bGoldLo = 0, bFear99 = 0, bHun99 = 0, tot = 0, suspSeed = 0, fatSeed = 0;
        for (int si = 0; si < NSEED; si++)
        {
            int d1; int[] d3;
            var w = Run((ulong)si, new[] { -1, -1, -1, -1, -1 }, World.MAXT, out d1, out d3);
            bool susp = d1 > 0;
            if (susp) suspSeed++;
            for (int i = 0; i < World.N; i++) if (w.v[i].alive)
            {
                tot++;
                if (w.v[i].gold >= 99) bGold99++;
                if (w.v[i].gold <= 5) bGoldLo++;
                if (w.v[i].fear >= 99) bFear99++;
                if (w.v[i].hunger >= 99) bHun99++;
            }
        }
        Console.WriteLine($"  无法基线终局：金=99 占 {100.0 * bGold99 / tot:F1}%  金≤5 占 {100.0 * bGoldLo / tot:F1}%  "
            + $"恐=99 占 {100.0 * bFear99 / tot:F1}%  饿=99 占 {100.0 * bHun99 / tot:F1}%  停摆过的 seed {suspSeed}/{NSEED}");

        // 随机法组里，每条法疲劳的时间 & 停摆
        long fatTotal = 0; long fatTickSum = 0;
        for (int si = 0; si < NSEED; si++) for (int li = 0; li < L; li++)
        {
            var w = new World((ulong)si, all[li]);
            bool[] wasFat = new bool[World.SLOTS];
            for (int t = 0; t < World.MAXT; t++)
            {
                w.Tick();
                for (int s = 0; s < World.SLOTS; s++)
                    if (!wasFat[s] && (w.laws[s].flags & LawFlag.Fatigued) != 0) { wasFat[s] = true; fatTotal++; fatTickSum += t; }
                if (w.AliveCount() == 0) break;
            }
        }
        Console.WriteLine($"  随机法组：共 {fatTotal} 条法触发疲劳，中位触发 tick ≈ {(fatTotal > 0 ? fatTickSum / fatTotal : -1)}（满局 2400）");

        // ═══ 实验 4：恐惧专诊 —— 恐惧条件法到底能不能开火 ═══
        Console.WriteLine("\n══════ 实验4：恐惧专诊 ══════");
        // 全村民·全 tick 的 fear 分布（随机法组）
        long fsum = 0, fcnt = 0, fgt25 = 0, fgt50 = 0, fgt35 = 0; int fmax = 0, fmin = 99;
        var hist = new int[100];
        for (int si = 0; si < NSEED; si++)
        {
            var w = new World((ulong)si, all[si % L]);
            for (int t = 0; t < World.MAXT; t++)
            {
                w.Tick();
                for (int i = 0; i < World.N; i++) if (w.v[i].alive)
                {
                    int fr = w.v[i].fear; fsum += fr; fcnt++; hist[fr < 99 ? fr : 99]++;
                    if (fr > 25) fgt25++; if (fr > 35) fgt35++; if (fr > 50) fgt50++;
                    if (fr > fmax) fmax = fr; if (fr < fmin) fmin = fr;
                }
                if (w.AliveCount() == 0) break;
            }
        }
        Console.WriteLine($"  全村民·全 tick：均恐惧 {(double)fsum / fcnt:F1}  min {fmin} max {fmax}");
        Console.WriteLine($"    fear>25 占 {100.0 * fgt25 / fcnt:F1}%   >35 占 {100.0 * fgt35 / fcnt:F1}%   >50 占 {100.0 * fgt50 / fcnt:F1}%");
        Console.Write("    分布直方(每5格): ");
        for (int b = 0; b < 20; b++) { int s = 0; for (int k = b * 5; k < b * 5 + 5 && k < 100; k++) s += hist[k]; Console.Write($"{b * 5}:{100.0 * s / fcnt:F0}% "); }
        Console.WriteLine();

        // 两条恐惧法（P5 fear>25→+15, P6 fear>50→-25）单独上，命中率
        for (int p = 5; p <= 6; p++)
        {
            long h = 0; for (int si = 0; si < NSEED; si++) { int d1; int[] d3; var w = Run((ulong)si, new[] { p, -1, -1, -1, -1 }, World.MAXT, out d1, out d3); h += w.lawHits[0]; }
            Console.WriteLine($"    法{p} 单独上：共命中 {h}，均 {h / (double)NSEED:F0}/seed （满局 2400 tick）");
        }

        // ═══ 实验 5：同一 seed 内，换法带来的「结局差距」是否达到可观水平 ═══
        Console.WriteLine("\n══════ 实验5：同 seed 换法 → 终局差距（选一个 seed 全 56 法组）══════");
        for (int si = 0; si < 3; si++)
        {
            double amin = 1e9, amax = -1e9, mfmin = 1e9, mfmax = -1e9, gmin = 1e9, gmax = -1e9;
            for (int li = 0; li < L; li++) { var ff = X[si * L + li]; amin = Math.Min(amin, ff[0]); amax = Math.Max(amax, ff[0]); gmin = Math.Min(gmin, ff[1]); gmax = Math.Max(gmax, ff[1]); mfmin = Math.Min(mfmin, ff[3]); mfmax = Math.Max(mfmax, ff[3]); }
            Console.WriteLine($"  seed {si}: 存活 [{amin:F0},{amax:F0}]  均金 [{gmin:F0},{gmax:F0}]  均恐 [{mfmin:F0},{mfmax:F0}]");
        }
        Console.WriteLine("\n  （对照：seed 之间）");
        for (int f = 0; f < NF; f++)
        {
            double mn = 1e9, mx = -1e9; for (int k = 0; k < X.Length; k++) { if (X[k][f] < mn) mn = X[k][f]; if (X[k][f] > mx) mx = X[k][f]; }
            Console.Write($"{FName[f]}[{mn:F0},{mx:F0}]  ");
        }
        Console.WriteLine();
    }
}
