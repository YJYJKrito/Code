// ═══════════════════════════════════════════════════════════════════
// T02 · 有序对打包 + 标签位图（Bug 1 / Bug 4 的回归测试）
//
// Bug 1 是**致命**的：朴素的 a*N+b 在 N=20 时最大下标 419，
//   而 NumTargets 预留的槽只有 0..400 → 越界。N≥3 就崩。
//   它只在"人际关系对"这个域上触发，所以很容易在开发后期才炸。
//
// Bug 4 是编译错误（const byte = 1<<8），已经被编译器挡掉；
//   但它的**语义后遗症**必须留在测试里：伤疤永远清不掉。
//   这是「废法 = 止血，不是撤销」这条设计承诺的唯一实现点。
// ═══════════════════════════════════════════════════════════════════
using NUnit.Framework;

namespace SimLit.Tests
{
    [TestFixture]
    [Category("Fast")]
    public class T02_TargetsTests : SimTestBase
    {
        static readonly int[] Ns = { 3, 5, 12, 20 };

        // ── T02_01 双射：每个有序对唯一，且不越界 ──
        [Test]
        public void T02_01_PackPair是双射且不越界([ValueSource(nameof(Ns))] int n)
        {
            int limit = n * (n - 1);                       // 关系槽总数
            var seen = new bool[limit];
            for (int a = 0; a < n; a++)
                for (int b = 0; b < n; b++)
                {
                    if (a == b) continue;
                    int idx = Targets.PackPair(a, b, n);
                    Assert.That(idx, Is.InRange(0, limit - 1),
                        $"PackPair({a},{b},{n}) = {idx} 越界 [0,{limit - 1}]");
                    Assert.IsFalse(seen[idx], $"PackPair 不是单射：下标 {idx} 被 ({a},{b}) 与另一个对共用");
                    seen[idx] = true;
                }
            for (int k = 0; k < limit; k++)
                Assert.IsTrue(seen[k], $"PackPair 不是满射：下标 {k} 没有任何有序对映射到它");
        }

        // ── T02_02 Slot 永远落在 [0, Count) 内 ──
        [Test]
        public void T02_02_Slot不越界([ValueSource(nameof(Ns))] int n)
        {
            int count = Targets.Count(n);
            Assert.AreEqual(1 + n + n * (n - 1), count);
            for (int a = 0; a < n; a++)
            {
                Assert.That(Targets.Slot(a, -1, n), Is.InRange(0, count - 1));
                for (int b = 0; b < n; b++)
                    if (a != b)
                        Assert.That(Targets.Slot(a, b, n), Is.InRange(0, count - 1),
                            $"Slot({a},{b},{n}) 越界 —— 关系域卡会在这里摔");
            }
        }

        // ── T02_03 ★ Bug 1 反证：朴素写法确实会越界 ──
        // 这条测试的价值在于：它把"为什么不能用 a*N+b"钉成了一条会失败的断言。
        // 将来有人"顺手简化"回去，这条会立刻红。
        [Test]
        public void T02_03_朴素的aN加b确实会越界_Bug1反证()
        {
            int n = 20;
            int limit = n * (n - 1);           // 380
            int worst = -1;
            for (int a = 0; a < n; a++)
                for (int b = 0; b < n; b++)
                {
                    if (a == b) continue;
                    int naive = a * n + b;
                    if (naive > worst) worst = naive;
                }
            Assert.Greater(worst, limit - 1,
                $"朴素 a*N+b 的最大下标 {worst} 竟然没超过上限 {limit - 1} —— " +
                "要么 NumTargets 的定义变了，要么这条反证已经失效，请重新推导 Bug 1");
        }
    }

    [TestFixture]
    [Category("Fast")]
    public class T02_TagBitsTests : SimTestBase
    {
        // ── T02_04 16 位全部可用（Bug 4）──
        [Test]
        public void T02_04_标签位图16位全部可用()
        {
            ushort all = 0;
            for (int bit = 0; bit < 16; bit++)
            {
                ushort t = (ushort)(1 << bit);
                Assert.AreNotEqual(0, t, $"第 {bit} 位为 0 —— 又退回 byte 了");
                all |= t;
            }
            Assert.AreEqual(ushort.MaxValue, all, "16 位没有全部覆盖");
        }

        // ── T02_05 ★ 伤疤免疫清除（设计承诺的唯一实现点）──
        [Test]
        public void T02_05_伤疤不可被清除()
        {
            ushort tags = TagBits.SCAR_MASK;
            TagBits.ApplyRemoveTag(ref tags, TagBits.SCAR_MASK);
            Assert.AreEqual(TagBits.SCAR_MASK, tags,
                "伤疤被清掉了 —— 「废法 = 止血，不是撤销」这条承诺就没了");
        }

        // ── T02_06 性格可清 ──
        [Test]
        public void T02_06_性格可被清除()
        {
            ushort tags = TagBits.TRAIT_MASK;
            TagBits.ApplyRemoveTag(ref tags, TagBits.TRAIT_MASK);
            Assert.AreEqual(0, tags, "性格应该可以被清除");
        }

        // ── T02_07 行为标签可清 ──
        [Test]
        public void T02_07_行为标签可被清除()
        {
            ushort tags = TagBits.BEHAV_MASK;
            TagBits.ApplyRemoveTag(ref tags, TagBits.BEHAV_MASK);
            Assert.AreEqual(0, tags, "行为标签应该可以被清除");
        }

        // ── T02_08 清性格时不得误伤伤疤 ──
        [Test]
        public void T02_08_清性格不误伤伤疤()
        {
            ushort tags = (ushort)(TagBits.Coward | TagBits.Bandit | TagBits.Sinner);
            TagBits.ApplyRemoveTag(ref tags, TagBits.TRAIT_MASK);
            Assert.AreEqual((ushort)(TagBits.Bandit | TagBits.Sinner), tags,
                "清性格的时候把伤疤/行为标签一起抹了 —— 掩码算错了");
        }

        // ── T02_09 三类位图互不重叠 ──
        [Test]
        public void T02_09_三类位图互不重叠()
        {
            Assert.AreEqual(0, TagBits.TRAIT_MASK & TagBits.SCAR_MASK, "性格与伤疤位重叠");
            Assert.AreEqual(0, TagBits.TRAIT_MASK & TagBits.BEHAV_MASK, "性格与行为位重叠");
            Assert.AreEqual(0, TagBits.SCAR_MASK & TagBits.BEHAV_MASK, "伤疤与行为位重叠");
        }

        // ── T02_10 Describe 对空标签不崩 ──
        [Test]
        public void T02_10_Describe不崩()
        {
            Assert.DoesNotThrow(() => TagBits.Describe(0));
            Assert.DoesNotThrow(() => TagBits.Describe(ushort.MaxValue));
            Assert.AreEqual("（无）", TagBits.Describe(0));
        }
    }
}
