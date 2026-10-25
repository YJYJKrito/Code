// ═══════════════════════════════════════════════════════════════════
// T01 · 确定性随机（Rng）
//
// 保护的东西：整个项目的**可复现性**。同 seed 双端逐位一致、反事实对照实验、
//   分享码、"只改一条法跑出两个村子" —— 全部建立在这一层上。
//
// 最重要的一条是 T01_03（seq 隔离）。它是 Bug 2 的回归测试：
//   原设计 RngFor(seed, tick, channel) 没有 seq，同一 tick 内 8 个优先级段
//   只能产出 1 个不同值，加权随机静默退化成"高权重卡被系统性偏好"。
//   **这个 bug 不崩溃、不报错，只有这条测试能抓住它。**
// ═══════════════════════════════════════════════════════════════════
using NUnit.Framework;

namespace SimLit.Tests
{
    [TestFixture]
    [Category("Fast")]
    public class T01_RngTests : SimTestBase
    {
        // ── T01_01 纯函数：同参数永远同值 ──
        [Test]
        public void T01_01_For_是纯函数()
        {
            for (int k = 0; k < 1000; k++)
            {
                ulong a = Rng.For((ulong)k, k * 7, Rng.CH_DEAL, k);
                ulong b = Rng.For((ulong)k, k * 7, Rng.CH_DEAL, k);
                Assert.AreEqual(a, b, "同一组参数两次调用结果不同 —— Rng 里有隐藏状态");
            }
        }

        // ── T01_02 channel 隔离：不同 channel 必须产出不同值 ──
        [Test]
        public void T01_02_不同channel产出不同值()
        {
            int[] chs = { Rng.CH_DEAL, Rng.CH_WOBBLE, Rng.CH_EPILOG, Rng.CH_AUTH,
                          Rng.CH_CONSTITUTION, Rng.CH_PRESENTATION, Rng.CH_VILLAGER };
            var seen = new System.Collections.Generic.HashSet<ulong>();
            for (int i = 0; i < chs.Length; i++)
            {
                ulong v = Rng.For(12345UL, 100, chs[i], 0);
                Assert.IsTrue(seen.Add(v), $"channel {chs[i]} 与前面的 channel 撞了值 —— channel 没有真正参与混合");
            }
        }

        // ── T01_03 ★★ 核心回归测试：seq 隔离（Bug 2）★★ ──
        // 判决：同一 tick 同一 channel 下，seq=0..7 必须给出 8 个**互不相同**的值。
        // 原设计（无 seq）在这一条上是 1/8 —— 这就是 Bug 2。
        [Test]
        public void T01_03_同一tick内序列号必须隔离_Bug2回归()
        {
            const int SEGS = 8;
            var seen = new System.Collections.Generic.HashSet<ulong>();
            for (int seq = 0; seq < SEGS; seq++)
                seen.Add(Rng.For(999UL, 42, Rng.CH_DEAL, seq));

            Assert.AreEqual(SEGS, seen.Count,
                $"同一 tick 同一 channel 下 {SEGS} 个 seq 只产出 {seen.Count} 个不同值。" +
                "这正是 Bug 2：抽卡在各优先级段之间完全相关，加权随机退化成固定偏好。");

            // 反证：把 seq 固定成常量，就只剩 1 个值 —— 证明这条测试真的在测 seq
            var noSeq = new System.Collections.Generic.HashSet<ulong>();
            for (int seg = 0; seg < SEGS; seg++)
                noSeq.Add(Rng.For(999UL, 42, Rng.CH_DEAL, 0));
            Assert.AreEqual(1, noSeq.Count, "对照组失效 —— 测试本身写错了");
        }

        // ── T01_04 tick 隔离 ──
        [Test]
        public void T01_04_不同tick产出不同值()
        {
            var seen = new System.Collections.Generic.HashSet<ulong>();
            for (int t = 0; t < 500; t++) seen.Add(Rng.For(7UL, t, Rng.CH_DEAL, 0));
            Assert.AreEqual(500, seen.Count, "500 个不同 tick 出现了重复值 —— tick 没有充分参与混合");
        }

        // ── T01_05 seed 隔离 ──
        [Test]
        public void T01_05_不同seed产出不同值()
        {
            var seen = new System.Collections.Generic.HashSet<ulong>();
            for (ulong s = 0; s < 500; s++) seen.Add(Rng.For(s, 0, Rng.CH_DEAL, 0));
            Assert.AreEqual(500, seen.Count, "seed=0..499 出现重复 —— seed 0 不能自锁");
        }

        // ── T01_06 Range 值域与边界 ──
        [Test]
        public void T01_06_Range永远落在区间内()
        {
            for (int k = 0; k < 5000; k++)
            {
                int v = Rng.Range((ulong)k, k, Rng.CH_VILLAGER, k % 17, 5, 45);
                Assert.GreaterOrEqual(v, 5);
                Assert.Less(v, 45);
            }
        }

        [Test]
        public void T01_07_Range的span为0或负数时不崩()
        {
            Assert.DoesNotThrow(() => Rng.Range(1, 1, Rng.CH_DEAL, 1, 10, 10));
            Assert.DoesNotThrow(() => Rng.Range(1, 1, Rng.CH_DEAL, 1, 10, 3));
            Assert.AreEqual(10, Rng.Range(1, 1, Rng.CH_DEAL, 1, 10, 10), "span=0 应返回下限");
            Assert.AreEqual(10, Rng.Range(1, 1, Rng.CH_DEAL, 1, 10, 3), "span<0 应返回下限（防 %0 崩）");
        }

        // ── T01_08 Chance 的边界 ──
        [Test]
        public void T01_08_Chance的0和100是短路()
        {
            for (int k = 0; k < 1000; k++)
            {
                Assert.IsFalse(Rng.Chance((ulong)k, k, Rng.CH_DEAL, k, 0), "0% 必须永远 false");
                Assert.IsTrue(Rng.Chance((ulong)k, k, Rng.CH_DEAL, k, 100), "100% 必须永远 true");
            }
        }

        // ── T01_09 Chance 的分布不能偏 ──
        // 加权随机的"权重"只有在分布无偏时才等于真实概率。
        [Test]
        public void T01_09_Chance的分布大致无偏()
        {
            int hit = 0, n = 200000;
            for (int k = 0; k < n; k++)
                if (Rng.Chance(2026UL, k / 100, Rng.CH_DEAL, k % 100, 30)) hit++;
            double p = hit * 100.0 / n;
            Assert.That(p, Is.InRange(29.3, 30.7), $"30% 的命中率实测 {p:F2}% —— 分布有偏，权重就不是概率了");
        }

        // ── T01_10 Mix64 单射（低碰撞）──
        // 混合器一旦退化成双射的近似，随机流的独立段之间会出现可预测的结构。
        [Test]
        public void T01_10_Mix64无碰撞()
        {
            var seen = new System.Collections.Generic.HashSet<ulong>();
            for (ulong x = 0; x < 100000; x++)
                Assert.IsTrue(seen.Add(Rng.Mix64(x)), $"Mix64 在输入 {x} 处发生碰撞 —— 它不再是双射");
        }
    }
}
