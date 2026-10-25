// ═══════════════════════════════════════════════════════════════════
// PlayMode/SmokeTests.cs —— 真机冒烟（**Unity 专用，脱机不可跑**）
//
// 拷到：Assets/Tests/PlayMode/
// asmdef 需要：UnityEngine.TestRunner / UnityEditor.TestRunner / Sim / Diag
//             + 你的 Game 程序集
//
// 【它和 EditMode 的分工】EditMode 测的是"逻辑对不对"（毫秒级、秒级反馈）。
//   这一层测的是"在真实的 Unity 生命周期里会不会炸" ——
//   首帧、协程、切后台、纹理上传、资源释放。这些**在编辑器里全绿、在手机上崩**。
//
// ⚠️ 标了 TODO 的地方要你填自己的类名。填之前那些测试会编译不过 —— 这是故意的，
//    它逼你至少看一遍每个钩子该挂在哪。
// ═══════════════════════════════════════════════════════════════════
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SimLit;

namespace SimLit.Tests.PlayMode
{
    public class SmokeTests
    {
        // ── P01 首帧不炸 ──
        // TapPlay 最贵的 P1 是"冷启动 ANR"。这条在真机上跑，
        // 首帧的重活（大图集、场景加载、反射初始化）会全部暴露在这里。
        [UnityTest]
        public IEnumerator P01_冷启动能进到第一个可交互帧()
        {
            // TODO: 换成你的入口场景名
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            yield return null;   // 让 Awake/Start 跑完
            yield return null;   // 再给一帧

            Assert.IsFalse(Application.isLoadingLevel, "场景还没加载完 —— 首帧做了太多事");
            // 若这里超时或无输出，就是 ANR
        }

        // ── P02 tick 循环不能跑在协程里（本项目最贵的一条纪律）──
        // Android 上协程里的未捕获异常会**静默杀掉该协程**：
        // 一次异常 = 游戏永久卡住但**不崩溃**，用户报"卡死"你拿不到任何堆栈。
        // 这条测试验证 tick 是由累加器驱动的同步调用，不依赖协程。
        [UnityTest]
        public IEnumerator P02_tick循环不依赖协程()
        {
            // TODO: 换成你的 tick 驱动器类型
            // var driver = Object.FindObjectOfType<TickDriver>();
            // Assert.IsNotNull(driver, "没找到 tick 驱动器");
            //
            // int before = driver.TickCount;
            // yield return new WaitForSeconds(2f);
            // Assert.Greater(driver.TickCount, before, "两秒过去 tick 没有推进 —— 驱动可能挂了");

            yield return null;
            Assert.Pass("（占位：接上你的 TickDriver 后启用上面的断言）");
        }

        // ── P03 单 tick 耗时（真机数字，比 EditMode 的可信）──
        [UnityTest]
        public IEnumerator P03_真机单tick耗时在预算内()
        {
            var w = new World(1, new[] { 0, 2, 4, 5, 6 });
            for (int t = 0; t < 300; t++) w.Tick();      // 预热

            const int N = 1000;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int t = 0; t < N; t++) w.Tick();
            sw.Stop();

            double ms = sw.Elapsed.TotalMilliseconds / N;
            Debug.Log($"[P03] 真机单 tick = {ms * 1000:F1} µs");
            Assert.Less(ms, 3.0,
                $"真机单 tick {ms * 1000:F1}µs，超过 3ms —— 1x 下每 tick 有 1000ms 预算，但 3x 只剩 333ms，" +
                "而且这是纯逻辑，渲染还没算进去。先查是不是有卡被写成了关系域（380 目标）");

            yield return null;
        }

        // ── P04 稳态零 GC（真机 GC 的代价远大于 PC）──
        [UnityTest]
        public IEnumerator P04_稳态每tick零GC分配()
        {
            var w = new World(1, new[] { 0, 2, 4, 5, 6 });
            for (int t = 0; t < 300; t++) w.Tick();

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int t = 0; t < 500; t++) w.Tick();
            long perTick = (System.GC.GetAllocatedBytesForCurrentThread() - before) / 500;

            Debug.Log($"[P04] 每 tick 分配 {perTick} 字节");
            Assert.LessOrEqual(perTick, 64, $"每 tick 分配 {perTick} 字节 —— 手机上会是每秒一次的帧尖峰");

            yield return null;
        }

        // ── P05 切后台 → 回前台：只弹汇总，绝不逐帧补 ──
        // 设计里已经定了这条（OnApplicationPause → 跳结果）。
        // 逐帧补会在低端机上变成一次几百毫秒的卡顿，TapPlay 直接判 P1。
        [UnityTest]
        public IEnumerator P05_切后台回来只跳结果不逐帧补()
        {
            // TODO: 接上你的暂停处理
            // 手动验证步骤：
            //   1. 游戏进行中，home 键切后台
            //   2. 等 10 分钟（或改系统时间）
            //   3. 回前台
            // 期望：弹一个"你离开期间发生了 N 件事"的汇总面板
            // 期望：**不**看到 tick 在快速追赶
            yield return null;
            Assert.Pass("（占位：见上方的手动验证步骤）");
        }

        // ── P06 存档往返：存 → 读 → 哈希一致 ──
        [UnityTest]
        public IEnumerator P06_存档往返后状态哈希一致()
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "smoke_test.sav");

            var w = new World(42, new[] { 0, 2, 4, 5, 6 });
            for (int t = 0; t < 500; t++) w.Tick();
            ulong hash = w.HashState();

            // TODO: 换成你的存档编解码
            // SaveCodec.Save(w, path);
            // var loaded = SaveCodec.Load(path);
            // Assert.AreEqual(hash, loaded.HashState(), "存档往返后状态变了 —— 有字段没被存");
            //
            // 在接上之前，至少验证 AtomicWrite 在真机上能用：
            AtomicWrite.Write(path, hash.ToString());
            Assert.AreEqual(hash.ToString(), AtomicWrite.Read(path), "AtomicWrite 在真机的 persistentDataPath 上读回不一致");

            yield return null;
        }

        // ── P07 长图导出不泄漏纹理 ──
        // 忘 Destroy(rt) 的话每次存图泄漏 16MB，存几张就 OOM。
        [UnityTest]
        public IEnumerator P07_长图导出不泄漏纹理()
        {
            // TODO: 接上你的长图导出
            // try {
            //     for (int i = 0; i < 5; i++) LongImageExporter.Export();
            // } finally { Resources.UnloadUnusedAssets(); }
            //
            // yield return Resources.UnloadUnusedAssets();
            // long mb = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024*1024);
            // Assert.Less(mb, 300, $"导出 5 张长图后内存 {mb}MB —— RenderTexture 没释放");

            yield return null;
            Assert.Pass("（占位：接上 LongImageExporter 后启用）");
        }

        // ── P08 挂机 30 分钟：内存不涨 ──
        [UnityTest]
        [Explicit("真机长跑，手动触发")]
        public IEnumerator P08_挂机30分钟内存不涨()
        {
            long start = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1800f) yield return new WaitForSeconds(1f);
            long end = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            Debug.Log($"[P08] 30 分钟内存 {start / 1048576}MB → {end / 1048576}MB");
            Assert.Less(end - start, 50L * 1048576, "挂机 30 分钟内存涨了超过 50MB");
        }
    }
}
