// ═══════════════════════════════════════════════════════════════════
// T07 · 性能与 GC（手机端的生死线）
//
// 为什么在 PC 上测手机性能是有意义的：这两个指标**不是"快不快"**，是**"有没有"**。
//   · 稳态 GC Alloc/tick 必须是 0 字节 —— 在 PC 上不是 0，在手机上就不是"偶尔卡"，
//     而是"每秒一次的 GC 尖峰"，TapPlay 上直接变成 P1 的卡顿投诉。
//   · 单 tick 耗时在 PC 上是 ARM 的 1/15~1/30 —— 只要 PC 上留有 10 倍余量，
//     真机就是安全的。这里断言的是**余量**，不是绝对值。
//
// ⚠️ 这里测不出真机的一切：IL2CPP 剥离、纹理上传、DrawCall。
//   那些必须靠 T09（真机冒烟）和 D15 的 Profiler 连真机。
// ═══════════════════════════════════════════════════════════════════
using System;
using System.Diagnostics;
using NUnit.Framework;

namespace SimLit.Tests
{
    [TestFixture]
    public class T07_PerfTests : SimTestBase
    {
        // ── T07_01 ★ 稳态零 GC 分配 ★ ──
        // 门槛 ≤64 B/tick 是参考实现里的宽松值；真目标是 0。
        // 一旦有人往 Tick 里塞了 List / LINQ / 字符串拼接 / 装箱，这条立刻红。
        [Test]
        [Category("Fast")]
        public void T07_01_单tick不产生GC分配()
        {
            var w = new World(1, new[] { 0, 2, 4, 5, 6 });
            for (int t = 0; t < 300; t++) w.Tick();          // 预热：让 JIT 与池子都稳下来

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int t = 0; t < 200; t++) w.Tick();
            long perTick = (GC.GetAllocatedBytesForCurrentThread() - before) / 200;

            Assert.LessOrEqual(perTick, 64,
                $"稳态每 tick 分配 {perTick} 字节。Tick 里出现了 new / 装箱 / 字符串 / LINQ。" +
                "在 PC 上这只是「多几次 GC」，在手机上就是每秒一次的帧尖峰。");
        }

        // ── T07_02 单 tick 耗时留够余量 ──
        // 1x 速度下每 tick 有 1000ms 墙钟预算。断言 PC 上 ≤ 500µs，
        // 按 ARM 折价 30 倍算 → 15ms，仍留有 60 倍余量。
        [Test]
        [Category("Fast")]
        public void T07_02_单tick耗时留有真机余量()
        {
            var w = new World(1, new[] { 0, 2, 4, 5, 6 });
            for (int t = 0; t < 500; t++) w.Tick();          // 预热

            const int N = 2000;
            var sw = Stopwatch.StartNew();
            for (int t = 0; t < N; t++) w.Tick();
            sw.Stop();

            double us = sw.Elapsed.TotalMilliseconds * 1000.0 / N;
            Assert.LessOrEqual(us, 500.0,
                $"单 tick {us:F1}µs（PC）—— 超过 500µs 就没有 30 倍的真机折价余量了。" +
                "首先怀疑：律法/事件卡被写成了关系域（380 目标），或者用了容器与 lambda。");
        }

        // ── T07_03 构造 World 不能分配超大数组 ──
        // 回归测试。曾经有两个 int[CARDS * NUM_TARGETS]（各 32KB）被分配但**从未被读取**，
        // 每个 World 白扔 64KB。批量实验里几千个 World 就是几百 MB 垃圾，
        // 表现为跑几千局后在 Tick 里随机抛 NullReferenceException 并伴随进程段错误。
        [Test]
        [Category("Fast")]
        public void T07_03_构造World的分配量有上限()
        {
            // 预热一次，避免把 JIT 的开销算进来
            var warm = new World(0); GC.KeepAlive(warm);

            long before = GC.GetAllocatedBytesForCurrentThread();
            var list = new World[20];
            for (int i = 0; i < 20; i++) list[i] = new World((ulong)i);
            long perWorld = (GC.GetAllocatedBytesForCurrentThread() - before) / 20;
            GC.KeepAlive(list);

            Assert.LessOrEqual(perWorld, 8192,
                $"每个 World 构造分配 {perWorld} 字节。参考实现约 2KB。" +
                "超了说明有大数组被预分配 —— 先确认它真的有人读（曾经有一对 int[8020] 纯属垃圾）。");
        }

        // ── T07_04 长局不能有内存泄漏 ──
        // 跑完一整局后强制 GC，存活集合不应该持续增长。
        [Test]
        [Category("Slow")]
        public void T07_04_反复跑局不累积内存()
        {
            for (int i = 0; i < 20; i++) { var w = new World((ulong)i); for (int t = 0; t < 300; t++) w.Tick(); }
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long after = GC.GetTotalMemory(true);

            for (int i = 0; i < 200; i++) { var w = new World((ulong)i); for (int t = 0; t < 300; t++) w.Tick(); }
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long later = GC.GetTotalMemory(true);

            long growth = later - after;
            Assert.LessOrEqual(growth, 2 * 1024 * 1024,
                $"跑 200 局后存活堆增长 {growth / 1024}KB —— 有东西被 static 集合攒住了（事件日志？因果链？）");
        }
    }
}
