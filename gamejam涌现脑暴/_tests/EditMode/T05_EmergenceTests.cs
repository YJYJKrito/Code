// ═══════════════════════════════════════════════════════════════════
// T05 · 涌现（主题的守护测试）★ 最重要的一批 ★
//
// 这一批测试保护的不是"代码对不对"，而是**"这游戏还配不配叫涌现"**。
//
// 为什么必须有：技术验证报告 §2 记录了四层递进的数值崩溃 ——
//   修完五个 bug 之后，游戏仍然不可玩：村庄活着，但所有寄存器收敛到边界，
//   10 个律法条件里只有 2 个可用（其余全是死条件或恒真）。
//   这种失败**不报错、不崩溃**，只会让玩家觉得"这游戏没反应"。
//
//   更糟的是：同样的症状（σ→0）在验证中出现过四次，由**完全相反**的参数引起
//   （金币只进不出 / 恐惧只有汇没有源 / 加正反馈 / 恐惧源太强）。
//   也就是说，靠直觉调参必然反复踩回去。
//
//   → 唯一可靠的办法是让这几条数字**常驻在测试里**。它们红了，就是这一版不能发。
//
// 判据来源：Holland / Bedau 的弱涌现五条自测题（抗删减 / 反馈环 / 路径未被写死 /
//   非线性 / 可读性）。下面每条测试括号里标出它守的是哪一条。
// ═══════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SimLit.Tests
{
    [TestFixture]
    public class T05_EmergenceTests : SimTestBase
    {
        // ── T05_01 反事实分岔（判据 3：路径未被写死）★ 核心可交付物 ──
        // 「同一个 seed，只改一条法的一个参数，跑出两个完全不同的村子」
        // 这是要说给评委听的那句话。它拿不出来，主题就只是口号。
        [Test]
        [Category("Fast")]
        public void T05_01_同seed只改一条法的阈值必须分岔()
        {
            const int SEEDS = 40;
            int diverged = 0;
            var uniq = new HashSet<ulong>();

            for (ulong s = 0; s < SEEDS; s++)
            {
                ulong h70 = RunWithParam(s, 70);
                ulong h40 = RunWithParam(s, 40);
                if (h70 != h40) diverged++;
                uniq.Add(h70); uniq.Add(h40);
            }

            Assert.AreEqual(SEEDS, diverged,
                $"只有 {diverged}/{SEEDS} 个 seed 会因一个参数而分岔 —— " +
                "「只改一个参数跑出两个村子」这句话不成立，给评委的核心证明拿不出来");
            Assert.Greater(uniq.Count, SEEDS,
                "两套参数跑出来的哈希种类太少 —— 参数改了但结局没怎么动");
        }

        static ulong RunWithParam(ulong seed, int param)
        {
            var w = new World(seed);
            w.laws[0].condType = 0;      // 饥饿 > N
            w.laws[0].effType = 4;       // 抢最近的邻居
            w.laws[0].effParam = 12;
            w.laws[0].condParam = param;
            for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
            return w.HashState();
        }

        // ── T05_02 抗删减 / 非线性：结局必须分化，不能收敛到同一个不动点 ──
        // （判据 1 + 4）纯加法系统会收敛到不动点，没有历史、没有故事。
        [Test]
        [Category("Slow")]
        public void T05_02_随机律法组合的终局形态必须分化()
        {
            const int N = 200;
            var shapes = new HashSet<int>();
            for (ulong s = 0; s < N; s++)
            {
                var w = RunWorld(s);
                int tags = 0;
                for (int i = 0; i < World.N; i++)
                    tags += System.Numerics.BitOperations.PopCount((uint)w.v[i].tags);
                shapes.Add(w.AliveCount() * 1000 + tags / 4 * 10 + Math.Min(w.deaths, 19) / 2);
            }
            Assert.GreaterOrEqual(shapes.Count, 3,
                $"{N} 个 seed 只跑出 {shapes.Count} 种终局形态 —— 系统退化到不动点了，" +
                "这时候写 20 张事件卡也救不回来（技术验证报告 §2.1 第四层）");
        }

        // ── T05_03 结局必须是一条梯度，不能非 0 即 20 ──
        // 这条守的是一个**结构性**的错误：如果产出与消耗都严格按人头走，
        //   收支比值就与人口无关 → 中间态不稳定 → 结局只能两极。
        //   修法是承载力模型（人均产出随人口递减）。这条测试防止它被改回去。
        [Test]
        [Category("Slow")]
        public void T05_03_结局是梯度而不是两极()
        {
            const int N = 200;
            int polar = 0;
            var hist = new int[World.N + 1];
            for (ulong s = 0; s < N; s++)
            {
                int a = RunAlive(s);
                hist[a]++;
                if (a == 0 || a == World.N) polar++;
            }
            int buckets = 0; for (int k = 0; k <= World.N; k++) if (hist[k] > 0) buckets++;

            double polarShare = polar / (double)N;
            Assert.LessOrEqual(polarShare, 0.55,
                $"两极（0 人或 20 人）占 {polarShare * 100:F0}% —— 超过一半的律法组合结局无法区分，" +
                "玩家的选择等于抛硬币。检查承载力模型（PROD_BASE / CROWD_K）是否被改坏");
            Assert.GreaterOrEqual(buckets, 8,
                $"终局只有 {buckets} 个档位 —— 结局太聚集");
        }

        // ── T05_04 三个寄存器都必须活着（σ > 0）──
        // （判据 2：反馈环）σ≈0 意味着该寄存器已死，所有读它的条件都变成死条件，
        //   而且**不会有任何报错**。技术验证报告 §2.6 明确警告过这类 bug 出现过四次。
        [Test]
        [Category("Fast")]
        public void T05_04_存活村庄的三寄存器必须有方差()
        {
            const int N = 60;
            double sg = 0, sh = 0, sf = 0; int living = 0;
            for (ulong s = 0; s < N; s++)
            {
                var w = RunWorld(s);
                if (w.AliveCount() < 3) continue;
                var st = w.Stat();
                sg += st.sg; sh += st.sh; sf += st.sf;
                living++;
            }
            Assert.Greater(living, N / 3, $"{N} 个村庄里只有 {living} 个活到终局 —— 生存率崩了，先查这个");
            sg /= living; sh /= living; sf /= living;

            Assert.Greater(sg, 8.0, $"金币 σ={sg:F1} 太低 —— 金币已经被推到边界锁死，所有金币条件会失效");
            Assert.Greater(sh, 5.0, $"饥饿 σ={sh:F1} 太低 —— 饥饿被钳在一个常数上");
            Assert.Greater(sf, 3.0, $"恐惧 σ={sf:F1} 太低 —— 恐惧已死，所有恐惧条件都是死条件");
        }

        // ── T05_05 ★ 8 条律法必须全部承重（这条守的是"立法有效"）★ ──
        // 判据：某条法的 presence effect（含它的组合 vs 不含它的组合，终局存活之差）
        //   绝对值必须 ≥ 1.5。低于这个数，说明这条法对结局**没有可测量的影响** ——
        //   玩家把它选进去和不选它，村子一样。那就是装饰。
        //
        // 实测教训：修之前 8 条里只有 2 条承重，"税收"的边际影响是 0.07（等于零）。
        [Test]
        [Category("Slow")]
        public void T05_05_八条预设法必须全部是载荷法()
        {
            var lawsets = AllLawsets();
            const int SEEDS = 16;
            var byCombo = new double[lawsets.Length];
            for (int ci = 0; ci < lawsets.Length; ci++)
            {
                double sum = 0;
                for (int si = 0; si < SEEDS; si++) sum += RunAlive((ulong)(si * 101 + 3), lawsets[ci]);
                byCombo[ci] = sum / SEEDS;
            }

            var weak = new List<string>();
            var report = new List<string>();
            for (int pi = 0; pi < World.PRESETS_COUNT; pi++)
            {
                double ws = 0, wos = 0; int wn = 0, won = 0;
                for (int ci = 0; ci < lawsets.Length; ci++)
                {
                    bool has = Array.IndexOf(lawsets[ci], pi) >= 0;
                    if (has) { ws += byCombo[ci]; wn++; } else { wos += byCombo[ci]; won++; }
                }
                double diff = ws / wn - wos / won;
                report.Add($"#{pi} {CalibNames[pi]} {diff:+0.00;-0.00}");
                if (Math.Abs(diff) < 1.5) weak.Add($"#{pi} {CalibNames[pi]} (|{diff:F2}|)");
            }

            Assert.IsEmpty(weak,
                "以下律法对结局的影响小于 1.5，等于装饰法：\n  " + string.Join("\n  ", weak) +
                "\n全部边际影响：" + string.Join("  ", report));
        }

        static readonly string[] CalibNames =
        { "福利", "税收", "饥则抢", "济贫", "耕作", "恐惧蔓延", "安抚", "私刑倾向" };

        // ── T05_06 恐惧必须是活寄存器（有人读、有动态范围）──
        // 修之前：恐惧是"只进不出的死胡同寄存器"—— 事件卡零读取、
        //   关系/饥饿/死亡都不受影响、>35 的触发率 0.1%。
        [Test]
        [Category("Fast")]
        public void T05_06_恐惧必须是活寄存器()
        {
            Diag.Reset(); Diag.On = true;
            var lawsets = AllLawsets();
            for (int ci = 0; ci < 8; ci++)
                RunWorld((ulong)(ci * 101 + 3), lawsets[ci * 7 % lawsets.Length]);
            Diag.On = false;

            long reads = Diag.FearReadTotal(), writes = Diag.FearWriteTotal();
            Assert.Greater(reads, 0, "恐惧没有任何读者 —— 它是个死胡同寄存器");
            Assert.Greater(writes, 0, "恐惧没有任何写入者 —— 它恒为 0");
            Assert.Greater(reads, writes / 10,
                $"恐惧被读 {reads} 次、写 {writes} 次 —— 读太少，说明它基本没参与决策");

            // 动态范围：恐惧分布必须有可用的尾部，否则所有恐惧阈值都是死条件
            var hist = new int[101];
            long samples = 0;
            for (int sdi = 0; sdi < 4; sdi++)
            {
                var w = new World((ulong)(sdi * 37 + 11), new[] { 0, 2, 4, 5, 6 });
                for (int t = 0; t < World.MAXT; t++)
                {
                    w.Tick();
                    if (t % 20 != 0) continue;
                    for (int i = 0; i < World.N; i++) if (w.v[i].alive) { hist[w.v[i].fear]++; samples++; }
                }
            }
            int p50 = Pct(hist, samples, 0.50), p95 = Pct(hist, samples, 0.95);
            Assert.GreaterOrEqual(p95 - p50, 6,
                $"恐惧的 p50={p50}、p95={p95}，分布太窄 —— 恐惧阈值几乎全挤在同一个触发率上，条件失去区分力");
        }

        static int Pct(int[] hist, long total, double q)
        {
            long acc = 0, target = (long)(total * q);
            for (int k = 0; k <= 100; k++) { acc += hist[k]; if (acc >= target) return k; }
            return 100;
        }
    }
}
