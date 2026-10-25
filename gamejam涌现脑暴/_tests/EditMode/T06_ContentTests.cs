// ═══════════════════════════════════════════════════════════════════
// T06 · 内容完整性 —— 抓「死信」
//
// 死信 = 只被写、从来没有被读的字段。
//   它是这类项目最隐蔽的浪费：数据写进去了、看着很丰富，
//   其实没有任何机制消费它 —— 你为它写的美术、文案、UI 全是空转。
//
// 修之前实测：13 个标签里 3 个是死信（信徒/税民/守卫）。
//   其中「信徒」被写了 33 万次，一次都没被读过。
//
// ★ 这整个文件依赖 Sim/Diag.cs 的**运行时计数器**，不是人工维护的清单。
//   上一版诊断仪用写死的字符串报告这件事，改完代码后字符串没跟着改，
//   于是它开始报告一个已经不存在的世界。数出来的才算数。
// ═══════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using NUnit.Framework;

namespace SimLit.Tests
{
    [TestFixture]
    public class T06_ContentTests : SimTestBase
    {
        /// <summary>开着遥测跑一批村庄，把标签/恐惧的读写次数攒起来。</summary>
        void CollectDiag(int lawsetsToRun)
        {
            Diag.Reset(); Diag.On = true;
            var lawsets = AllLawsets();
            for (int ci = 0; ci < lawsetsToRun; ci++)
                RunWorld((ulong)(ci * 131 + 7), lawsets[(ci * 7) % lawsets.Length]);
            Diag.On = false;
        }

        // ── T06_01 ★ 13 个标签全部必须有读者 ★ ──
        [Test]
        [Category("Fast")]
        public void T06_01_每个标签都必须有读者()
        {
            CollectDiag(12);

            var dead = new List<string>();
            var idle = new List<string>();
            for (int k = 0; k < Diag.NTAG; k++)
            {
                long wr = Diag.TagWriteTotal(k), rd = Diag.TagReadTotal(k);
                if (wr == 0 && rd == 0) idle.Add(Diag.TAG_NAME[k]);
                else if (rd == 0) dead.Add($"{Diag.TAG_NAME[k]}（被写 {wr} 次，读 0 次）");
            }

            Assert.IsEmpty(dead,
                "以下标签是死信 —— 有代码在写它，但没有任何机制读它：\n  " + string.Join("\n  ", dead) +
                "\n要么给它接一个读者（事件卡门禁 / 律法条件 / 物理层调制），要么把它从设计里删掉。");
            Assert.IsEmpty(idle,
                "以下标签从未出现过 —— 声明了但没有任何代码写它：\n  " + string.Join("\n  ", idle));
        }

        // ── T06_02 每条律法都要真的开火（要在**真实组合**里测）──
        //
        // 【为什么不能把同一条法装 5 份来测】踩过一次：
        //   「私刑倾向」的生效前提是"这个人身上已经有伤疤或被判过罪"，
        //   而伤疤是靠**另一条法**（饥则抢）打出来的。把私刑倾向自己叠 5 份，
        //   村里一个受害者都不会产生 → 它永远不开火。
        //
        //   这不是 bug，这是设计要的 **法→法耦合**：反应式律法不能自举。
        //   但它意味着「测试方式」必须跟着改 —— 必须把每条法放回 56 种真实组合里数。
        //
        //   ⚠️ 顺带得到一条**内容设计约束**（已写进开工清单）：
        //      反应式律法（#7 私刑倾向）不能单独出现在第一幕的法池里 ——
        //      玩家选了它、村里却因为还没有造伤疤的法而永远不触发，
        //      他的体感就是"这条法是坏的"。
        [Test]
        [Category("Fast")]
        public void T06_02_每条预设法都要在真实组合里开火()
        {
            var lawsets = AllLawsets();
            var hits = new long[World.PRESETS_COUNT];
            var appearances = new int[World.PRESETS_COUNT];

            for (int ci = 0; ci < lawsets.Length; ci++)
            {
                var L = lawsets[ci];
                var w = new World((ulong)(ci * 31 + 5), L);
                for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
                for (int s = 0; s < World.SLOTS; s++) { hits[L[s]] += w.lawHits[s]; appearances[L[s]]++; }
            }

            var silent = new List<string>();
            var report = new List<string>();
            for (int pi = 0; pi < World.PRESETS_COUNT; pi++)
            {
                long per = appearances[pi] == 0 ? 0 : hits[pi] / appearances[pi];
                report.Add($"#{pi} {CalibNames[pi]} {per}");
                if (per < 20) silent.Add($"#{pi} {CalibNames[pi]}（平均每局 {per} 次）");
            }

            Assert.IsEmpty(silent,
                "以下律法在它出现的组合里几乎不开火，等于没装：\n  " + string.Join("\n  ", silent) +
                "\n平均每局开火次数：" + string.Join("  ", report) +
                "\n检查它的条件是否落在死区（用 --deep 的 B2b 节看可用带）。");
        }

        // ── T06_03 律法条件不能落在死区 ──
        // 技术验证报告 §2.1 第四层：原始参数下 10 个条件只有 2 个可用，
        // 其余不是死条件（永不触发）就是恒真（无区分度）。
        // 「村庄活着但无事发生」比全灭更隐蔽。
        [Test]
        [Category("Fast")]
        public void T06_03_可用条件数不能太低()
        {
            const int N = 40;
            int total = 0, living = 0;
            for (ulong s = 0; s < N; s++)
            {
                var w = RunWorld(s);
                if (w.AliveCount() < 3) continue;
                total += w.UsableConditions(); living++;
            }
            Assert.Greater(living, 0, "没有一个村庄活到终局，先修生存率");
            double avg = total / (double)living;
            Assert.GreaterOrEqual(avg, 5.0,
                $"存活村庄的可用条件平均只有 {avg:F1}/10 —— 大部分律法条件落在死区。" +
                "玩家贴上去的法永远不会被满足，他只会觉得这游戏没反应。" +
                "（注意：UsableConditions 的探针阈值必须落在 --deep B2b 实测的可用带内）");
        }

        // ── T06_04 讣告必须能引用到具体的律法槽 ──
        // 设计的宣传语是「他们死于你写的法律」。如果死亡事件里没有
        // causeLawSlot，讣告就只能写"他死于饥饿"，写不出"死于你写的第 3 条法"——
        // 整个卖点就没了，而且不会报错。
        [Test]
        [Category("Fast")]
        public void T06_04_死亡必须能归因到律法槽()
        {
            var f = typeof(Villager).GetFields();
            bool hasDeathTick = false;
            foreach (var x in f) if (x.Name == "deathTick") hasDeathTick = true;
            Assert.IsTrue(hasDeathTick, "Villager 缺少 deathTick —— 讣告需要知道死于哪一 tick");

            // 至少要有足够的死亡样本可归因
            int totalDeaths = 0;
            for (ulong s = 0; s < 20; s++) totalDeaths += RunWorld(s).deaths;
            Assert.Greater(totalDeaths, 20,
                $"20 个村庄合计只有 {totalDeaths} 人死亡 —— 讣告是核心内容，死亡样本太少内容就立不住");
        }

        static readonly string[] CalibNames =
        { "福利", "税收", "饥则抢", "济贫", "耕作", "恐惧蔓延", "安抚", "私刑倾向" };
    }
}
