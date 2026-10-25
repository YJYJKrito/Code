// ═══════════════════════════════════════════════════════════════════
// SimTestBase.cs —— 全部 Sim 测试的基类
//
// 【为什么需要它】World 的标定参数是 static（为了让 Calib 能扫参），
//   所以任何一条测试改了 PROD_BASE / FEAR_DECAY_DIV / FARM_PCT …… 都会污染后面的测试。
//   更糟的是它**不报错**，只会让某条不相干的测试偶尔失败，然后你花半天找错地方。
//   SetUp 快照 + TearDown 还原是唯一的解。
//
// 【Diag 也必须复位】Diag.On 是 static，A3 那类测试会打开它；
//   开着 Diag 跑别的测试不会改变结果（计数器只读不写），但会让耗时翻几倍。
// ═══════════════════════════════════════════════════════════════════
using System;
using NUnit.Framework;

namespace SimLit.Tests
{
    public abstract class SimTestBase
    {
        int _pb, _ck, _fh, _fd, _fj, _fat, _farm;

        [SetUp]
        public void SnapshotStatics()
        {
            _pb = World.PROD_BASE; _ck = World.CROWD_K;
            _fh = World.FEAR_HUNGER_DIV; _fd = World.FEAR_DECAY_DIV;
            _fj = World.PROD_JITTER_PCT; _fat = World.FATIGUE_K; _farm = World.FARM_PCT;
            Diag.On = false;      // 诊断默认关闭（理由见下）
            Diag.Reset();
        }

        [TearDown]
        public void RestoreStatics()
        {
            World.PROD_BASE = _pb; World.CROWD_K = _ck;
            World.FEAR_HUNGER_DIV = _fh; World.FEAR_DECAY_DIV = _fd;
            World.PROD_JITTER_PCT = _fj; World.FATIGUE_K = _fat; World.FARM_PCT = _farm;
            Diag.On = false;
            Diag.Reset();
        }

        // 【为什么 Diag.On 不"快照—还原"，而是两边都硬置 false】
        //   原来写的是 _diagOn = Diag.On; … Diag.On = _diagOn; 看似对称，其实有毒：
        //   一旦某条测试在把 On 关掉之前就断言失败（T05_06 / T06_01 就是这么写的），
        //   On 就留在 true；下一条测试的 SetUp 把 true 快照下来，TearDown 又还原成 true。
        //   **一次失败 = 整个进程后续所有测试都开着诊断跑**，慢好几倍，而且看不出来。
        //   诊断开关是基类的不变量，测试不该有能力把它泄漏给下一条 —— 所以两边都置 false。

        // ── 常用跑法 ──

        /// <summary>跑完一局，返回状态哈希。</summary>
        protected static ulong RunHash(ulong seed, int[] lawset = null)
        {
            var w = new World(seed, lawset);
            for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
            return w.HashState();
        }

        /// <summary>跑完一局，返回终局存活人数。</summary>
        protected static int RunAlive(ulong seed, int[] lawset = null)
        {
            var w = new World(seed, lawset);
            for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
            return w.AliveCount();
        }

        /// <summary>跑一局并返回终局 World（调用方自己看字段）。</summary>
        protected static World RunWorld(ulong seed, int[] lawset = null)
        {
            var w = new World(seed, lawset);
            for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
            return w;
        }

        /// <summary>56 种「8 选 5」的律法组合。</summary>
        protected static int[][] AllLawsets()
        {
            var list = new System.Collections.Generic.List<int[]>();
            int P = World.PRESETS_COUNT;
            for (int a = 0; a < P; a++)
                for (int b = a + 1; b < P; b++)
                    for (int c = b + 1; c < P; c++)
                        for (int d = c + 1; d < P; d++)
                            for (int e = d + 1; e < P; e++)
                                list.Add(new[] { a, b, c, d, e });
            return list.ToArray();
        }

        protected static string Join(int[] a) => string.Join(",", a);
    }
}
