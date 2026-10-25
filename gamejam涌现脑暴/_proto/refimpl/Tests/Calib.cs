// ═══════════════════════════════════════════════════════════════════
// Tests/Calib.cs —— 参数标定：找出「村庄走在刀刃上」的那个格子。
//
// 为什么需要它：承载力模型有两根互相纠缠的轴（人均产出上限 PROD_BASE、
// 拥挤系数 CROWD_K），它们共同决定平衡人口 P* = (PROD_BASE/1000 - c) / (CROWD_K/1000)。
// 手调是在二维面上瞎撞。这里改成扫描 + 多指标联合判分。
//
// 判分（五项，全部要过）：
//   ① 存活均值落在 [6,14]      —— 不偏乐观也不偏灾难
//   ② 跨组合存活标准差 ≥ 4.0    —— 律法选择真的造成差异
//   ③ 终局档位数 ≥ 10           —— 不是非 0 即 20 的两极
//   ④ 最弱法的 presence |diff| ≥ 1.5 —— 8 条法全是载荷法
//   ⑤ 恐惧>35 的时长占比在 5%~95% —— 恐惧是可用的活寄存器
// ═══════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Text;

namespace SimLit
{
    public static class Calib
    {
        const int P = World.PRESETS_COUNT;
        const int SWEEP_SEEDS = 6;      // 扫描阶段用少 seed 提速
        public static readonly string[] NAME = { "福利", "税收", "饥则抢", "济贫", "耕作", "恐惧蔓延", "安抚", "私刑倾向" };

        static List<int[]> _combos;

        static List<int[]> Combos()
        {
            if (_combos != null) return _combos;
            var list = new List<int[]>();
            for (int a = 0; a < P; a++)
                for (int b = a + 1; b < P; b++)
                    for (int c = b + 1; c < P; c++)
                        for (int d = c + 1; d < P; d++)
                            for (int e = d + 1; e < P; e++)
                                list.Add(new[] { a, b, c, d, e });
            _combos = list;
            return list;
        }

        public struct Score
        {
            public double meanAlive, sdAlive, minPresence, fear35;
            public int buckets;
            public double polar;      // 结局落在两极（0 人或 20 人）的比例
            public bool Pass => meanAlive >= 6 && meanAlive <= 14 && sdAlive >= 4.0
                             && buckets >= 10 && minPresence >= 1.5 && fear35 >= 0.05 && fear35 <= 0.95;
            public string Line() =>
                $"均值{meanAlive,5:F1} σ{sdAlive,4:F1} 档位{buckets,3} 两极{polar * 100,3:F0}% 最弱法{minPresence,5:F2} 恐惧>35 {fear35 * 100,5:F1}% {(Pass ? "✅" : "")}";
        }

        /// <summary>对一个参数格子跑完整记分卡。seeds 越多越准也越慢。</summary>
        public static Score Eval(int prodBase, int crowdK, int fhd, int fdd, int seeds)
        {
            int savePB = World.PROD_BASE, saveCK = World.CROWD_K, saveFH = World.FEAR_HUNGER_DIV, saveFD = World.FEAR_DECAY_DIV;
            World.PROD_BASE = prodBase; World.CROWD_K = crowdK;
            World.FEAR_HUNGER_DIV = fhd; World.FEAR_DECAY_DIV = fdd;

            var combos = Combos();
            var byCombo = new double[combos.Count];
            var bucketSet = new HashSet<int>();
            int polarN = 0, totalN = 0;
            for (int ci = 0; ci < combos.Count; ci++)
            {
                double s = 0;
                for (int si = 0; si < seeds; si++)
                {
                    var w = new World((ulong)(si * 101 + 3), combos[ci]);
                    for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
                    int a = w.AliveCount();
                    s += a;
                    bucketSet.Add(a);
                    totalN++;
                    if (a == 0 || a == World.N) polarN++;
                }
                byCombo[ci] = s / seeds;
            }

            double mean = 0; for (int i = 0; i < byCombo.Length; i++) mean += byCombo[i]; mean /= byCombo.Length;
            double var = 0; for (int i = 0; i < byCombo.Length; i++) { double d = byCombo[i] - mean; var += d * d; }
            double sd = Math.Sqrt(var / byCombo.Length);

            // 逐法 presence effect
            double minPres = double.MaxValue;
            for (int pi = 0; pi < P; pi++)
            {
                double ws = 0, wos = 0; int wn = 0, won = 0;
                for (int ci = 0; ci < combos.Count; ci++)
                {
                    bool has = Array.IndexOf(combos[ci], pi) >= 0;
                    if (has) { ws += byCombo[ci]; wn++; } else { wos += byCombo[ci]; won++; }
                }
                if (wn == 0 || won == 0) continue;
                double diff = Math.Abs(ws / wn - wos / won);
                if (diff < minPres) minPres = diff;
            }
            if (minPres == double.MaxValue) minPres = 0;

            // 恐惧 >35 的时长占比
            var w2 = new World(7, new[] { 0, 2, 4, 5, 6 });
            long hit = 0, tot = 0;
            for (int t = 0; t < World.MAXT; t++)
            {
                w2.Tick();
                if (t % 20 != 0) continue;
                for (int i = 0; i < World.N; i++) if (w2.v[i].alive) { tot++; if (w2.v[i].fear > 35) hit++; }
            }
            double f35 = tot == 0 ? 0 : hit / (double)tot;

            World.PROD_BASE = savePB; World.CROWD_K = saveCK;
            World.FEAR_HUNGER_DIV = saveFH; World.FEAR_DECAY_DIV = saveFD;
            return new Score
            {
                meanAlive = mean, sdAlive = sd, buckets = bucketSet.Count,
                minPresence = minPres, fear35 = f35, polar = polarN / (double)totalN
            };
        }

        static int Rank(Score sc) =>
            (sc.Pass ? 100 : 0) + (sc.buckets >= 10 ? 1 : 0) + (sc.minPresence >= 1.5 ? 1 : 0)
            + (sc.fear35 >= 0.05 ? 1 : 0) + (sc.polar <= 0.5 ? 1 : 0);

        static string Sweep(string title, int[] bases, int[] crowds, int[] fdds, int fhd, out string bestKey, out Score best)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"══════ {title} ══════");
            sb.AppendLine($"扫描 {SWEEP_SEEDS} seed × 56 律法组合 / 格\n");
            bestKey = null; best = default; int bestScore = -1;
            sb.AppendLine("人均产出上限 | 拥挤系数 | 恐惧惯性 | 记分卡");
            sb.AppendLine("-------------|----------|----------|----------------------------------");
            foreach (int b in bases)
                foreach (int c in crowds)
                    foreach (int fdd in fdds)
                    {
                        var sc = Eval(b, c, fhd, fdd, SWEEP_SEEDS);
                        sb.AppendLine($"    {b,4}     |   {c,3}    |    {fdd,3}   | {sc.Line()}");
                        int s = Rank(sc);
                        if (s > bestScore) { bestScore = s; best = sc; bestKey = $"{b} {c} {fdd}"; }
                    }
            sb.AppendLine($"\n→ 最佳格：base/crowd/fdd = {bestKey}");
            sb.AppendLine($"  {best.Line()}");
            if (bestKey != null)
            {
                var tk = bestKey.Split(' ');
                int bb = int.Parse(tk[0]), cc = int.Parse(tk[1]), dd = int.Parse(tk[2]);
                sb.AppendLine("\n【最佳格的逐法 presence effect】（30 seed 精测）");
                sb.AppendLine(BuildPresenceTable(bb, cc, fhd, dd, 30));
            }
            return sb.ToString();
        }

        public static string Run()
        {
            int[] bases = { 1650, 1750, 1850, 1950, 2050 };
            int[] crowds = { 50, 70, 90 };
            int[] fdds = { 3, 4, 5 };
            return Sweep("参数标定扫描（粗）", bases, crowds, fdds, 45, out _, out _);
        }

        public static string RunFine()
        {
            int[] bases = { 1450, 1500, 1550, 1600, 1650, 1700 };
            int[] crowds = { 60, 70, 80 };
            int[] fdds = { 3 };
            return Sweep("参数细扫", bases, crowds, fdds, 45, out _, out _);
        }

        static string BuildPresenceTable(int prodBase, int crowdK, int fhd, int fdd, int seeds)
        {
            var sb = new StringBuilder();
            World.PROD_BASE = prodBase; World.CROWD_K = crowdK;
            World.FEAR_HUNGER_DIV = fhd; World.FEAR_DECAY_DIV = fdd;
            var combos = Combos();
            var byCombo = new double[combos.Count];
            for (int ci = 0; ci < combos.Count; ci++)
            {
                double s = 0;
                for (int si = 0; si < seeds; si++)
                {
                    var w = new World((ulong)(si * 101 + 3), combos[ci]);
                    for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
                    s += w.AliveCount();
                }
                byCombo[ci] = s / seeds;
            }
            sb.AppendLine("  法名       | 含它 | 不含 |  差异 | 判定");
            for (int pi = 0; pi < P; pi++)
            {
                double ws = 0, wos = 0; int wn = 0, won = 0;
                for (int ci = 0; ci < combos.Count; ci++)
                {
                    bool has = Array.IndexOf(combos[ci], pi) >= 0;
                    if (has) { ws += byCombo[ci]; wn++; } else { wos += byCombo[ci]; won++; }
                }
                double mw = ws / wn, mo = wos / won, diff = mw - mo;
                string v = Math.Abs(diff) < 0.3 ? "❌ 装饰法" : (Math.Abs(diff) < 1.5 ? "⚠️ 弱载荷" : "✅ 载荷法");
                sb.AppendLine($"  {NAME[pi],-9} | {mw,5:F2} | {mo,5:F2} | {diff,+5:F2} | {v}");
            }
            return sb.ToString();
        }
    }
}
