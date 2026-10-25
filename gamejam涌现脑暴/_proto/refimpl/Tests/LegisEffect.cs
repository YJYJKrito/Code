// ═══════════════════════════════════════════════════════════════════
// Tests/LegisEffect.cs —— 决定性实验：玩家的立法选择，到底有没有用？
//
// 实验优势：本实现的 PRNG 是**无状态**的 Rng.For(seed, tick, channel, seq)。
//   所以「同一个 seed + 不同律法」时，随机序列完全一致 ——
//   唯一变量就是律法。任何结局差异都能 100% 归因于立法。
//
// 四组测量：
//   ① 律法是否开火（最低要求）
//   ② 换一套法，结局是否变化（中等要求）+ 与「纯噪声」对比
//   ③ 律法造成的位移 vs 跨 seed 的自然散布（信噪比）
//   ④ 律法是否只改变「速度」而不改变「形态」
using System;
using System.Collections.Generic;
using System.Text;

namespace SimLit
{
    public static class LegisEffect
    {
        const int SEEDS = 40;
        const int SLOTS = World.SLOTS;
        const int P = World.PRESETS_COUNT;   // 8

        struct Outcome
        {
            public int alive, deaths, tagBits;
            public double gold, hunger, fear;
            public int lawHits;
            public double[] Vec()
            {
                return new double[] { alive, deaths, tagBits, gold, hunger, fear };
            }
        }

        static Outcome Run(ulong seed, int[] lawset)
        {
            var w = new World(seed, lawset);
            for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
            var st = w.Stat();
            int tb = 0;
            for (int i = 0; i < World.N; i++) tb += System.Numerics.BitOperations.PopCount((uint)w.v[i].tags);
            int hits = 0;
            for (int s = 0; s < SLOTS; s++) hits += w.lawHits[s];
            return new Outcome { alive = w.AliveCount(), deaths = w.deaths, tagBits = tb, gold = st.mg, hunger = st.mh, fear = st.mf, lawHits = hits };
        }

        static List<int[]> Combos()
        {
            var list = new List<int[]>();
            for (int a = 0; a < P; a++)
                for (int b = a + 1; b < P; b++)
                    for (int c = b + 1; c < P; c++)
                        for (int d = c + 1; d < P; d++)
                            for (int e = d + 1; e < P; e++)
                                list.Add(new[] { a, b, c, d, e });
            return list;
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            var combos = Combos();
            sb.AppendLine("══════ 决定性实验：立法是否有效影响村庄进程 ══════");
            sb.AppendLine($"配置：{SEEDS} 个 seed × {combos.Count} 种律法组合（8 条预设法选 5）");
            sb.AppendLine("实验优势：PRNG 无状态 → 同 seed 下随机序列完全一致，唯一变量是律法\n");

            // ── 基准：先把每个 seed 的「自然散布」量出来 ──
            // 同一套法（固定用组合 0）跨 seed 的散布 = 噪声地板
            var baseLaw = combos[0];
            var bySeed = new List<Outcome>();
            for (int s = 0; s < SEEDS; s++) bySeed.Add(Run((ulong)s, baseLaw));

            // 每一维的自然标准差（作为归一化尺度）
            double[] meanB = new double[6], sdB = new double[6];
            for (int d = 0; d < 6; d++)
            {
                double m = 0; for (int s = 0; s < SEEDS; s++) m += bySeed[s].Vec()[d]; m /= SEEDS;
                double v = 0; for (int s = 0; s < SEEDS; s++) { double x = bySeed[s].Vec()[d] - m; v += x * x; }
                meanB[d] = m; sdB[d] = Math.Sqrt(v / SEEDS);
            }
            sb.AppendLine("【噪声地板】固定一套法，跨 40 个 seed 的自然散布（标准差）：");
            sb.AppendLine($"  存活 σ={sdB[0]:F2}  死亡 σ={sdB[1]:F2}  标签 σ={sdB[2]:F2}  金币 σ={sdB[3]:F1}  饥饿 σ={sdB[4]:F1}  恐惧 σ={sdB[5]:F1}\n");

            // ── 实验 ① 律法是否开火 ──
            long hitSum = 0; int hitMin = int.MaxValue;
            for (int s = 0; s < SEEDS; s++) { hitSum += bySeed[s].lawHits; if (bySeed[s].lawHits < hitMin) hitMin = bySeed[s].lawHits; }
            sb.AppendLine("【实验①】律法开火率");
            sb.AppendLine($"  一局内 5 条法合计命中：平均 {hitSum / (double)SEEDS:F0} 次，最少 {hitMin} 次");
            sb.AppendLine(hitMin > 100 ? "  ✅ 律法确实在开火\n" : "  ❌ 律法几乎不开火 —— 问题出在条件层\n");

            // ── 实验 ② 换一套法，结局变不变（同 seed 受控）──
            // 对每个 seed，遍历 56 种组合，测「结局向量到基准的距离」
            double[] norm = new double[6];
            for (int d = 0; d < 6; d++) norm[d] = sdB[d] > 1e-6 ? sdB[d] : 1.0;

            double distSum = 0; int n = 0;
            int minAlive = int.MaxValue, maxAlive = int.MinValue;
            var distinctBucket = new HashSet<int>();
            for (int s = 0; s < SEEDS; s++)
            {
                var o0 = Run((ulong)s, new[] { -1, -1, -1, -1, -1 });   // 无法状态基准
                foreach (var L in combos)
                {
                    var o = Run((ulong)s, L);
                    double d2 = 0;
                    var a = o.Vec(); var b = o0.Vec();
                    for (int k = 0; k < 6; k++) { double x = (a[k] - b[k]) / norm[k]; d2 += x * x; }
                    distSum += Math.Sqrt(d2); n++;
                    if (o.alive < minAlive) minAlive = o.alive;
                    if (o.alive > maxAlive) maxAlive = o.alive;
                    distinctBucket.Add((o.alive / 3) * 100 + Math.Min(o.tagBits, 99) + o.deaths / 3 * 10000);
                }
            }
            double meanDist = distSum / n;
            sb.AppendLine("【实验②】同一 seed 下，换一套律法 vs 无法状态基准的结局位移");
            sb.AppendLine($"  归一化位移（每维除以自然 σ 后求欧氏距离）：平均 {meanDist:F3}");
            sb.AppendLine($"  存活数的全域范围：{minAlive} 到 {maxAlive}（共 20 人）");
            sb.AppendLine($"  去重后的结局形态数：{distinctBucket.Count}");
            string verdict2 = meanDist > 2.0 ? "✅ 律法确实改变结局" : (meanDist > 0.8 ? "⚠️ 律法有影响但偏弱" : "❌ 律法被噪声淹没");
            sb.AppendLine($"  → {verdict2}\n");

            // ── 实验 ③ 信噪比：律法造成的位移 vs 随机造成的散布 ──
            // 同一套法、同 seed 换法造成的位移，对比跨 seed 的自然散布（=1.0 归一化单位）
            sb.AppendLine("【实验③】信噪比（同一个 seed 内 56 种律法的结局散布 / 跨 seed 的自然散布）");
            double ratioSum = 0;
            for (int s = 0; s < SEEDS; s++)
            {
                var outs = new List<Outcome>();
                foreach (var L in combos) outs.Add(Run((ulong)s, L));
                double m = 0; for (int i = 0; i < outs.Count; i++) m += outs[i].alive; m /= outs.Count;
                double v = 0; for (int i = 0; i < outs.Count; i++) { double x = outs[i].alive - m; v += x * x; }
                double sdLaw = Math.Sqrt(v / outs.Count);
                ratioSum += sdLaw / norm[0];
            }
            double ratio = ratioSum / SEEDS;
            sb.AppendLine($"  比值 {ratio:F3}（>1 = 律法的解释力超过 seed 的随机性；<0.3 = 律法只是噪声）");
            string verdict3 = ratio > 1.0 ? "✅ 立法是主导因素" : (ratio > 0.3 ? "⚠️ 立法与随机性同量级" : "❌ 随机性淹没了立法");
            sb.AppendLine($"  → {verdict3}\n");

            // ── 实验 ④ 律法改变的是「形态」还是只是「速度」──
            // 如果所有组合最终都收敛到同一个终局，只是早晚不同，那就是「只改速度」
            sb.AppendLine("【实验④】律法改变的是形态还是只是速度");
            var endStates = new HashSet<int>();
            foreach (var L in combos)
            {
                var o = Run(7, L);
                endStates.Add((o.alive / 2) * 1000 + (o.tagBits / 4) * 10 + o.deaths / 2);
            }
            sb.AppendLine($"  seed=7 下 56 种律法的终局去重形态数：{endStates.Count} / {combos.Count}");
            sb.AppendLine(endStates.Count > 20 ? "  ✅ 形态多样，不是只改速度" : "  ⚠️ 大量律法收敛到同一终局 —— 可能只是改变了进程速度\n");

            // ── 逐条法的边际影响力 ──
            sb.AppendLine("【附加】8 条预设法各自的边际影响力（把它加进一套固定基准法后的结局位移）");
            var baseSet = new[] { 0, 1, 2, 3, 4 };
            var baseOut = new Dictionary<int, Outcome>();
            for (int s = 0; s < SEEDS; s++) baseOut[s] = Run((ulong)s, baseSet);
            for (int pi = 0; pi < P; pi++)
            {
                if (Array.IndexOf(baseSet, pi) >= 0) continue;
                double dsum = 0;
                for (int s = 0; s < SEEDS; s++)
                {
                    var L = (int[])baseSet.Clone();
                    L[2] = pi;                        // 用它替换掉第 3 条法
                    var o = Run((ulong)s, L);
                    double d2 = 0; var a = o.Vec(); var b = baseOut[s].Vec();
                    for (int k = 0; k < 6; k++) { double x = (a[k] - b[k]) / norm[k]; d2 += x * x; }
                    dsum += Math.Sqrt(d2);
                }
                string name = new[] { "福利", "税收", "饥则抢", "济贫", "耕作", "恐惧蔓延", "安抚", "私刑倾向" }[pi];
                sb.AppendLine($"    #{pi} {name,-6} 边际位移 {dsum / SEEDS:F3}");
            }

            return sb.ToString();
        }
    }
}
