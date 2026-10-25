// ═══════════════════════════════════════════════════════════════════
// Tests/DeepDiag.cs —— 深挖两个问题的根因
//   A. 立法：56 种组合为什么只出少数几种终局？哪些法是「载荷法」、哪些是「装饰法」？
//   B. 恐惧：它被谁读取？它的条件触发率多少？它的方差从哪来？
//
// 【本文件曾经撒过的谎，以及为什么现在不会了】
//   上一版的 A3（标签死信）和 B1（恐惧被谁读取）是**写死的字符串**，
//   不是测量。我修完 World.cs（新增 5 张读恐惧的卡、恐惧→饥饿耦合）之后
//   那些字符串一个字都没改，于是诊断仪开始报告一个已经不存在的世界。
//   现在 A3/B1 全部由 Sim/Diag.cs 的运行时计数器产出 —— 数不出来就是没有。
// ═══════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Text;

namespace SimLit
{
    public static class DeepDiag
    {
        const int SEEDS = 30;
        const int P = World.PRESETS_COUNT;

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════ 深挖诊断 ══════");
            sb.AppendLine($"（全局参数：产出 base{World.PROD_BASE} 拥挤{World.CROWD_K} · 恐惧/饥饿 {World.FEAR_HUNGER_DIV}"
                        + $" · 恐惧惯性 1/{World.FEAR_DECAY_DIV}）\n");
            Console.WriteLine("[进度] A1 组合扫描");

            // ══ A1. 载荷法 vs 装饰法 ══
            var combos = Combos();
            var aliveByCombo = new Dictionary<string, double>();
            foreach (var L in combos)
            {
                double s = 0;
                for (int sdi = 0; sdi < SEEDS; sdi++) s += RunAlive((ulong)(sdi * 101 + 3), L);
                aliveByCombo[Key(L)] = s / SEEDS;
                if (aliveByCombo.Count % 14 == 0) Console.WriteLine($"[进度] A1 {aliveByCombo.Count}/{combos.Count}");
            }
            sb.AppendLine("【A1】每条预设法出现在律法组合里，对终局存活的影响（presence effect）\n");
            sb.AppendLine("  法名       | 含它时均存活 | 不含它时均存活 | 差异 | 判定");
            sb.AppendLine("  -----------|--------------|----------------|------|------");
            int weakCount = 0;
            for (int pi = 0; pi < P; pi++)
            {
                double ws = 0, wos = 0; int wn = 0, won = 0;
                foreach (var L in combos)
                {
                    bool has = Array.IndexOf(L, pi) >= 0;
                    double v = aliveByCombo[Key(L)];
                    if (has) { ws += v; wn++; } else { wos += v; won++; }
                }
                double mw = ws / wn, mo = wos / won, diff = mw - mo;
                string verdict = Math.Abs(diff) < 0.3 ? "❌ 装饰法（几乎不影响结局）"
                               : (Math.Abs(diff) < 1.5 ? "⚠️ 弱载荷" : "✅ 载荷法");
                if (Math.Abs(diff) < 1.5) weakCount++;
                sb.AppendLine($"  {Calib.NAME[pi],-9} |     {mw,5:F2}     |      {mo,5:F2}      | {diff,+5:F2} | {verdict}");
            }
            sb.AppendLine($"\n  → 8 条法里 {8 - weakCount} 条是载荷法（|diff| ≥ 1.5）"
                        + $"{(weakCount == 0 ? "  ✅ 全部承重\n" : $"  ❌ 还有 {weakCount} 条不承重\n")}");

            // ══ A2. 终局分布 ══
            sb.AppendLine("【A2】56 种组合的终局分布（按终局存活数分桶）");
            var hist = new SortedDictionary<int, int>();
            foreach (var kv in aliveByCombo)
            {
                int b = (int)Math.Round(kv.Value);
                hist[b] = hist.GetValueOrDefault(b) + 1;
            }
            sb.AppendLine("  终局存活 | 组合数 | 条形");
            int maxBucket = 0; double total = 0;
            foreach (var kv in hist) { maxBucket = Math.Max(maxBucket, kv.Value); total += kv.Value; }
            foreach (var kv in hist)
                sb.AppendLine($"    {kv.Key,2} 人  |  {kv.Value,3}   | {new string('#', kv.Value)}");
            double polarShare = (hist.GetValueOrDefault(0) + hist.GetValueOrDefault(20)) / total;
            sb.AppendLine($"\n  → 56 种组合落在 {hist.Count} 个不同的终局存活档位");
            sb.AppendLine($"  → 两极（0 人或 20 人）占比 {polarShare * 100:F0}%"
                        + $"{(polarShare > 0.55 ? "  ❌ 过半律法组合结局无法区分" : "  ✅ 中段有实质分布")}\n");

            // ══ A3. 标签死信检测（真实计数）══
            sb.AppendLine("【A3】标签死信检测 —— 运行时实测的读/写次数");
            sb.AppendLine("     （只写不读 = 写进数据但没有任何机制消费它 = 死信）\n");
            Diag.Reset(); Diag.On = true;
            foreach (var L in combos)
                for (int sdi = 0; sdi < 2; sdi++)
                {
                    var w = new World((ulong)(sdi * 101 + 3), L);
                    for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
                }
            sb.AppendLine("  标签 | 写入次数 | 读取次数 | 读自（初始化/律法/事件卡/物理层）| 判定");
            int deadTags = 0, idleTags = 0;
            for (int k = 0; k < Diag.NTAG; k++)
            {
                long wr = Diag.TagWriteTotal(k), rd = Diag.TagReadTotal(k);
                string srcs = $"{Diag.tagRead[k, 0]}/{Diag.tagRead[k, 1]}/{Diag.tagRead[k, 2]}/{Diag.tagRead[k, 3]}";
                string verdict;
                if (wr == 0 && rd == 0) { verdict = "· 从未出现"; idleTags++; }
                else if (rd == 0) { verdict = "❌ 死信"; deadTags++; }
                else verdict = "✅ 有读者";
                sb.AppendLine($"  {Diag.TAG_NAME[k],-4} | {wr,8} | {rd,8} | {srcs,-30} | {verdict}");
            }
            sb.AppendLine($"\n  → 13 个标签：{13 - deadTags - idleTags} 个有读者，{deadTags} 个死信，{idleTags} 个从未出现"
                        + $"{(deadTags == 0 ? "  ✅ 无死信\n" : $"  ❌ 仍有 {deadTags} 个死信\n")}");
            Diag.On = false;

            // ══ B1. 恐惧被谁读取（真实计数）══
            sb.AppendLine("【B1】恐惧被谁读取 —— 运行时实测");
            sb.AppendLine($"  读取总次数 {Diag.FearReadTotal():N0}，写入总次数 {Diag.FearWriteTotal():N0}");
            sb.AppendLine($"  读自： 初始化 {Diag.fearRead[0]} · 律法条件 {Diag.fearRead[1]} · 事件卡 {Diag.fearRead[2]} · 物理层 {Diag.fearRead[3]}");
            sb.AppendLine($"  写自： 初始化 {Diag.fearWrite[0]} · 律法 {Diag.fearWrite[1]} · 事件卡 {Diag.fearWrite[2]} · 物理层 {Diag.fearWrite[3]}");
            sb.AppendLine(Diag.FearReadTotal() > 0
                ? "  ✅ 恐惧有读者 —— 它是活的\n"
                : "  ❌ 恐惧无人读取 —— 它是死的\n");

            // ══ B2. 恐惧条件的触发率 ══
            sb.AppendLine("【B2】恐惧条件的实际触发率（一局中，恐惧 > N 对多少村民成立）");
            int[] thr = { 15, 25, 35, 45, 55, 65, 75 };
            long[] hitCount = new long[thr.Length];
            long samples = 0;
            var hist2 = new int[101];
            for (int sdi = 0; sdi < 5; sdi++)
            {
                var w = new World((ulong)(sdi * 37 + 11), new[] { 0, 2, 4, 5, 6 });
                for (int t = 0; t < World.MAXT; t++)
                {
                    w.Tick();
                    if (t % 10 != 0) continue;
                    for (int i = 0; i < World.N; i++)
                    {
                        if (!w.v[i].alive) continue;
                        samples++;
                        hist2[w.v[i].fear]++;
                        for (int k = 0; k < thr.Length; k++) if (w.v[i].fear > thr[k]) hitCount[k]++;
                    }
                }
            }
            sb.AppendLine("  阈值 | 触发率 | 判定");
            for (int k = 0; k < thr.Length; k++)
            {
                double p = hitCount[k] * 100.0 / samples;
                string v = p < 5 ? "❌ 死条件" : (p > 95 ? "❌ 恒真" : "✅ 可用");
                sb.AppendLine($"   >{thr[k],2} | {p,5:F1}% | {v}");
            }
            sb.AppendLine("  恐惧分布直方图（每 5 点一桶）：");
            for (int b = 0; b < 20; b++)
            {
                int c = 0; for (int k = b * 5; k < b * 5 + 5 && k <= 100; k++) c += hist2[k];
                double pct = c * 100.0 / samples;
                sb.AppendLine($"   {b * 5,3}-{b * 5 + 4,3} | {pct,5:F1}% | {new string('#', (int)Math.Round(pct))}");
            }
            sb.Append('\n');

            // ══ B2b. 三个寄存器的「可用带」实测 ══
            // 存在的理由：World.UsableConditions() 里写死了 10 个探测阈值（f>15 / f>60 等）。
            // 动力学一改，那些阈值就落到带外，于是它按**已经不存在的世界**打分 —— 又一台撒谎的仪器。
            // 这里把三个寄存器的经验分位数直接量出来，谁要写新法就照这个带子选阈值。
            sb.AppendLine("【B2b】三个寄存器的实测可用带（写新法时按这个带子选阈值）");
            sb.AppendLine("  寄存器 |   p5 |  p50 |  p95 | 可用阈值区间（触发率 5%~95%）");
            var hg = new int[101]; var hh = new int[101]; var hf = new int[101];
            long n2 = 0;
            for (int sdi = 0; sdi < 5; sdi++)
            {
                var w = new World((ulong)(sdi * 37 + 11), new[] { 0, 2, 4, 5, 6 });
                for (int t = 0; t < World.MAXT; t++)
                {
                    w.Tick();
                    if (t % 10 != 0) continue;
                    for (int i = 0; i < World.N; i++)
                        if (w.v[i].alive) { hg[w.v[i].gold]++; hh[w.v[i].hunger]++; hf[w.v[i].fear]++; n2++; }
                }
            }
            Band(sb, "金币", hg, n2);
            Band(sb, "饥饿", hh, n2);
            Band(sb, "恐惧", hf, n2);
            sb.Append('\n');

            // ══ B3. 恐惧的方差从哪来 ══
            sb.AppendLine("【B3】恐惧的方差从哪来");
            var w2 = new World(3, new[] { 0, 1, 2, 3, 4 });
            double sumF = 0, sumF2 = 0, sumWithin = 0; long n = 0, snaps = 0;
            for (int t = 0; t < World.MAXT; t++)
            {
                w2.Tick();
                if (t % 20 != 0) continue;
                int al = 0; double m = 0;
                for (int i = 0; i < World.N; i++) if (w2.v[i].alive) { al++; m += w2.v[i].fear; }
                if (al == 0) continue;
                m /= al; snaps++;
                double v = 0;
                for (int i = 0; i < World.N; i++) if (w2.v[i].alive)
                    { double d = w2.v[i].fear - m; v += d * d; sumF += w2.v[i].fear; sumF2 += w2.v[i].fear * w2.v[i].fear; n++; }
                sumWithin += Math.Sqrt(v / al);
            }
            double within = sumWithin / snaps;
            double overall = Math.Sqrt(sumF2 / n - (sumF / n) * (sumF / n));
            sb.AppendLine($"  同一时刻村民之间的平均标准差（组内）：{within:F2}");
            sb.AppendLine($"  全体样本的总体标准差：{overall:F2}");
            sb.AppendLine($"  组内 / 总体 = {within / overall:F2}");
            sb.AppendLine(within / overall < 0.75
                ? "  → 组内 σ 明显小于总体 σ：恐惧主要是**时间上**的集体起伏，同一时刻大家差不多\n"
                : "  → 组内 σ 与总体 σ 同量级：同一时刻村民之间确有实质差异，'少数人恐慌引发雪崩'有土壤\n");

            return sb.ToString();
        }

        /// <summary>打印一个寄存器的 p5/p50/p95，以及「条件下界/上界」——
        /// 即让「值 &gt; N」的触发率恰好落在 95% / 5% 的那两个 N。</summary>
        static void Band(StringBuilder sb, string name, int[] hist, long total)
        {
            int Pct(double q)
            {
                long acc = 0; long target = (long)(total * q);
                for (int k = 0; k <= 100; k++) { acc += hist[k]; if (acc >= target) return k; }
                return 100;
            }
            int lo = Pct(0.05), mid = Pct(0.50), hi = Pct(0.95);
            // 「值 > N」的触发率 = 1 - CDF(N)，要落在 5%~95% ⟺ N ∈ (p5, p95)
            sb.AppendLine($"  {name}   | {lo,4} | {mid,4} | {hi,4} | 「{name} > N」的 N ∈ ({lo}, {hi})");
        }

        static int RunAlive(ulong seed, int[] lawset)
        {
            var w = new World(seed, lawset);
            for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
            return w.AliveCount();
        }

        static string Key(int[] L) => string.Join(",", L);

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
    }
}
