// ═══════════════════════════════════════════════════════════════════
// T04 · 存档（Bug 3）+ 律法属主与疲劳（Bug 5）
//
// Bug 3 的表现是「**新玩家第一次退出就崩**」：File.Replace 要求目标已存在，
//   而首次保存时它不存在 → FileNotFoundException。修复率极低、影响面 100%。
//
// Bug 5 是「两个字段都标了[哈希]，而疲劳逻辑只读其中一个」——
//   它不崩溃、不报错，只会让**重放静默分叉**。所以测试必须盯住"只有一个主"。
// ═══════════════════════════════════════════════════════════════════
using System;
using System.IO;
using NUnit.Framework;

namespace SimLit.Tests
{
    [TestFixture]
    [Category("Fast")]
    public class T04_AtomicWriteTests
    {
        string _dir, _path;

        [SetUp]
        public void MakeDir()
        {
            _dir = Path.Combine(Path.GetTempPath(), "simlit_save_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "village.sav");
        }

        [TearDown]
        public void KillDir()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        // ── T04_01 ★ Bug 3 回归：首次保存不得抛异常 ──
        [Test]
        public void T04_01_首次保存不抛异常_Bug3回归()
        {
            Assert.DoesNotThrow(() => AtomicWrite.Write(_path, "SAVE-1"),
                "首次保存抛异常了 —— 这就是 Bug 3，新玩家第一次退出即崩");
            Assert.AreEqual("SAVE-1", AtomicWrite.Read(_path));
        }

        // ── T04_02 连续保存都成功且读到最新 ──
        [Test]
        public void T04_02_连续三次保存都读回最新()
        {
            for (int i = 1; i <= 3; i++)
            {
                AtomicWrite.Write(_path, "SAVE-" + i);
                Assert.AreEqual("SAVE-" + i, AtomicWrite.Read(_path), $"第 {i} 次保存读回的不是最新档");
            }
        }

        // ── T04_03 写完之后不留 tmp 残骸 ──
        // 留下 .tmp 说明替换没走完；下次启动可能读到半截文件。
        [Test]
        public void T04_03_保存后不留tmp残骸()
        {
            AtomicWrite.Write(_path, "A");
            AtomicWrite.Write(_path, "B");
            Assert.IsFalse(File.Exists(_path + ".tmp"), "保存完成后 .tmp 还在 —— 替换流程没走完");
        }

        // ── T04_04 主档损坏 → 读备份 ──
        [Test]
        public void T04_04_主档损坏时回落到备份()
        {
            AtomicWrite.Write(_path, "OLD");     // 第 1 次：无 .bak
            AtomicWrite.Write(_path, "NEW");     // 第 2 次：产生 .bak = OLD
            File.Delete(_path);                  // 模拟主档被毁
            Assert.AreEqual("OLD", AtomicWrite.Read(_path), "主档没了却没有回落到备份");
        }

        // ── T04_05 主备都坏 → 返回 null，不抛 ──
        [Test]
        public void T04_05_主备都不可用时返回null不抛()
        {
            string got = null;
            Assert.DoesNotThrow(() => got = AtomicWrite.Read(Path.Combine(_dir, "不存在.sav")));
            Assert.IsNull(got, "读不到档应该返回 null，交给调用方走分级兜底，而不是抛异常卡在加载页");
        }

        // ── T04_06 目录不存在时也不能静默毁档 ──
        [Test]
        public void T04_06_目录不存在时抛的是IO异常而不是覆盖别人的档()
        {
            string bad = Path.Combine(_dir, "没有这个目录", "x.sav");
            Assert.Throws<DirectoryNotFoundException>(() => AtomicWrite.Write(bad, "X"));
        }
    }

    [TestFixture]
    [Category("Fast")]
    public class T04_LawOwnershipTests : SimTestBase
    {
        // ── T04_07 ★ Bug 5 回归：hitsTotal 只有一个主 ──
        [Test]
        public void T04_07_hitsTotal只有一个主_Bug5回归()
        {
            bool onLaw = false;
            foreach (var f in typeof(Law).GetFields())
                if (f.Name == "hitsTotal") onLaw = true;
            Assert.IsTrue(onLaw, "Law 上必须有一个 hitsTotal（它是唯一的属主）");

            foreach (var f in typeof(LawRuntimeView).GetFields())
                Assert.AreNotEqual("hitsTotal", f.Name,
                    "LawRuntimeView 上又出现了 hitsTotal —— 双主回归，哈希与玩法会静默分叉");
        }

        // ── T04_08 疲劳判定与 Law.hitsTotal 同源 ──
        [Test]
        public void T04_08_疲劳判定与hitsTotal同源()
        {
            var w = new World(3, new[] { 0, 1, 2, 3, 4 });
            for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }

            int sum = 0;
            for (int s = 0; s < World.SLOTS; s++) sum += w.laws[s].hitsTotal;

            bool matched = true;
            for (int s = 0; s < World.SLOTS; s++)
            {
                bool shouldBeFatigued = w.laws[s].hitsTotal > w.st.popPeak * World.FATIGUE_K;
                bool isFatigued = (w.laws[s].flags & LawFlag.Fatigued) != 0;
                if (shouldBeFatigued != isFatigued) matched = false;
            }
            Assert.Greater(sum, 0, "一整局下来律法一次都没命中 —— 条件层就崩了");
            Assert.IsTrue(matched, "疲劳标志与 Law.hitsTotal 的判定不一致 —— 又是双主的味道");
        }

        // ── T04_09 疲劳后效果减半 ──
        [Test]
        public void T04_09_疲劳后数值效果减半()
        {
            var w = new World(1, new[] { 0, 0, 0, 0, 0 });
            // 手动把 slot0 设成「给所有人发 10 金币」
            w.laws[0].state = LawState.Enacted;
            w.laws[0].condType = 1; w.laws[0].condParam = 99;   // 金币 < 99：恒真
            w.laws[0].effType = 0;  w.laws[0].effParam = 10;    // +10 金币

            int g0 = w.v[0].gold;
            w.Tick();
            int gained = w.v[0].gold - g0;   // 允许被软回拉削掉一点
            Assert.Greater(gained, 0, "法没生效");

            w.laws[0].flags |= LawFlag.Fatigued;
            int g1 = w.v[0].gold;
            w.Tick();
            int gainedFatigued = w.v[0].gold - g1;
            Assert.Less(gainedFatigued, gained,
                "疲劳之后效果没有变弱 —— 负反馈失效，系统会收敛到不动点");
        }

        // ── T04_10 废法 = 止血，不是撤销（伤疤不动）──
        [Test]
        public void T04_10_废法不清除伤疤()
        {
            var w = new World(9, new[] { 2, 4, 5, 2, 7 });   // 含「私刑倾向」
            for (int t = 0; t < 400; t++) { w.Tick(); if (w.AliveCount() == 0) break; }

            // 找出所有带伤疤的人，记下现场
            var before = new ushort[World.N];
            for (int i = 0; i < World.N; i++) before[i] = w.v[i].tags;

            // 全部废掉
            for (int s = 0; s < World.SLOTS; s++) w.laws[s].state = LawState.Empty;

            // 跑 50 tick，看伤疤是否被"治愈"
            for (int t = 0; t < 50; t++) w.Tick();

            for (int i = 0; i < World.N; i++)
            {
                ushort scarsBefore = (ushort)(before[i] & TagBits.SCAR_MASK);
                ushort scarsAfter = (ushort)(w.v[i].tags & TagBits.SCAR_MASK);
                Assert.AreEqual(scarsBefore, scarsAfter,
                    $"村民 {i} 的伤疤在废法后被清掉了 —— 「废法 = 止血，不是撤销」这条承诺破了");
            }
        }
    }
}
