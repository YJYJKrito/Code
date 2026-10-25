// ═══════════════════════════════════════════════════════════════════
// T03 · 确定性（整个项目的命根子）
//
// 为什么它最重要：比赛的"一句话证明"是 ——
//   「同一个 seed，只改一条法的一个参数，跑出两个完全不同的村子。」
//   这句话成立的前提是**同 seed 必须逐位可复现**。它一旦破，反事实对照、
//   分享码、双端同一内容、录像验收全部一起没。
//
// 注意：这些测试在**同一台机器同一进程**里跑，证明的是"同进程内确定"。
//   "跨设备/跨架构确定"（真机 ARM64 vs PC x64）必须靠真机哈希比对，
//   见 测试集-程序.md 的 §4。这里挡不住 float 泄漏，那是 T08 的活。
// ═══════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using NUnit.Framework;

namespace SimLit.Tests
{
    [TestFixture]
    [Category("Fast")]
    public class T03_DeterminismTests : SimTestBase
    {
        const int SEEDS = 60;

        // ── T03_01 同 seed 两遍完全一致 ──
        [Test]
        public void T03_01_同seed两遍哈希一致()
        {
            int mism = 0; string firstDiff = null;
            for (ulong s = 0; s < SEEDS; s++)
            {
                ulong a = RunHash(s), b = RunHash(s);
                if (a != b) { mism++; if (firstDiff == null) firstDiff = $"seed={s}: {a:X16} vs {b:X16}"; }
            }
            Assert.AreEqual(0, mism, $"{mism}/{SEEDS} 个 seed 两遍不一致。首例 {firstDiff} —— 有 static 状态没复位");
        }

        // ── T03_02 ★ 交错跑也不能互相影响 ──
        // 这条比 T03_01 严：它专门抓"跨实例污染"。
        // 如果有人把某个本该是实例字段的东西写成了 static，T03_01 可能仍然全绿
        // （因为两遍跑的污染方式一样），但这条会红。
        [Test]
        public void T03_02_交错运行不互相污染()
        {
            const ulong A = 11, B = 22;
            ulong a1 = RunHash(A);
            for (ulong k = 0; k < 5; k++) { RunHash(B + k); RunHash(100 + k); }
            ulong a2 = RunHash(A);
            Assert.AreEqual(a1, a2,
                $"跑过别的村庄之后 seed={A} 的结果变了（{a1:X16} → {a2:X16}）—— 有跨实例的 static 状态");
        }

        // ── T03_03 不同 seed 必须分岔 ──
        // 反过来的失败模式：所有 seed 跑出同一个村子 = seed 根本没接进去。
        [Test]
        public void T03_03_不同seed必须分岔()
        {
            var seen = new HashSet<ulong>();
            for (ulong s = 0; s < SEEDS; s++) seen.Add(RunHash(s));
            Assert.Greater(seen.Count, SEEDS * 3 / 4,
                $"{SEEDS} 个 seed 只跑出 {seen.Count} 种哈希 —— seed 的影响力太小，或者根本没接进去");
        }

        // ── T03_04 律法槽位顺序 = 优先级，必须真的影响结局 ──
        //
        // 【这是一条设计裁决，不是实现细节】实测：40/40 个 seed 上，同一组 5 条法
        //   换个装填顺序就会跑出不同的村子。因为 tick 内按槽位 0→4 依次适用律法。
        //
        //   我们**保留**这个行为，并把它定义成「律法的适用优先级」——
        //   现实里法条的适用顺序本来就不中性（先"济贫"还是先"饥则抢"，决定谁先拿到粮）。
        //
        //   ⚠️ 由此产生一条**必须落到 UI 上的约束**：槽位顺序绝不能被藏起来。
        //      玩家看到的必须是「第 1 条 / 第 2 条 …」，而不是一堆无序的卡片。
        //      否则同一个组合因拖放顺序不同给出不同村子，玩家只会觉得"这游戏随机"。
        //      （设计文档已砍掉"排序动词"，所以顺序只能在**装填时**确定并显式展示。）
        //
        //   这条测试钉住的是：顺序**确实**有影响力。若哪天有人把律法按 id 排序了，
        //   这条会红 —— 那是一个需要你显式决策的改动，不该悄悄发生。
        [Test]
        public void T03_04_槽位顺序是优先级_必须影响结局()
        {
            int[][] cases = {
                new[]{ 0, 1, 2, 3, 4 }, new[]{ 4, 3, 2, 1, 0 },
                new[]{ 2, 0, 4, 1, 3 }, new[]{ 3, 4, 0, 2, 1 },
            };
            int influenced = 0;
            const int N = 20;
            for (ulong s = 0; s < N; s++)
            {
                ulong b = RunHash(s, cases[0]);
                for (int k = 1; k < cases.Length; k++)
                    if (RunHash(s, cases[k]) != b) { influenced++; break; }
            }
            Assert.GreaterOrEqual(influenced, N / 2,
                $"20 个 seed 里只有 {influenced} 个被槽位顺序影响 —— 顺序已经不再有意义。" +
                "若这是有意的（比如改成按 id 规范排序），请同步更新本测试与 UI 上的槽位展示约定。");
        }

        // ── T03_05 HashState 的字段覆盖（绊线）──
        // 这是「新增字段必须显式决定是否进哈希」这条纪律的可执行版本。
        // 加了字段却忘了更新这个白名单 → 这条测试红 → 你必须停下来想一次。
        // 想清楚是「进哈希」还是「不进（纯表现层）」，然后改白名单。
        [Test]
        public void T03_05_Villager字段白名单_加字段必须显式决定是否进哈希()
        {
            string[] expected =
            {
                "gold", "hunger", "fear",      // 三个主寄存器
                "food",                        // 个人存粮（进哈希）
                "tags",                        // 16 位标签位图
                "alive", "deathTick",
                "nRobbed", "nFed", "nTaxed", "nProduced",   // 历史计数（卡的读历史门禁用）
            };
            var actual = new List<string>();
            foreach (var f in typeof(Villager).GetFields(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                actual.Add(f.Name);

            // fearSrc 是本 tick 的临时值，明确不进哈希，允许存在
            actual.Remove("fearSrc");
            actual.Sort();

            var exp = new List<string>(expected); exp.Sort();
            Assert.AreEqual(string.Join(",", exp), string.Join(",", actual),
                "Villager 的字段集合变了。请显式决定新字段是否进 HashState，" +
                "然后同时更新 测试集 与本白名单 —— 别让哈希和玩法悄悄说两件不同的事。");
        }
    }
}
