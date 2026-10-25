// ═══════════════════════════════════════════════════════════════════
// Tests/SelfTest.cs —— 可自动跑的验收门槛
//   每一条都对应技术验证报告里的一个 GATE。
//   这套自检在 Unity 里应作为 Editor 菜单项 / 构建前必跑项。
// ═══════════════════════════════════════════════════════════════════
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SimLit
{
    public static class SelfTest
    {
        static int _pass, _fail;
        static readonly StringBuilder Log = new StringBuilder();

        static void Gate(string name, bool ok, string detail)
        {
            if (ok) _pass++; else _fail++;
            Log.AppendLine($"  {(ok ? "✅" : "❌")} {name}");
            if (detail != null) Log.AppendLine($"       {detail}");
        }

        public static string RunAll()
        {
            _pass = 0; _fail = 0;
            Log.Clear();
            Log.AppendLine("══════ 参考实现自检 ══════\n");

            G1_Determinism();
            G2_PackPair();
            G3_AtomicWrite();
            G4_TagBits();
            G5_SingleOwnerHitsTotal();
            G6_Divergence();
            G7_Diversity();
            G8_RegisterHealth();
            G9_ZeroAlloc();
            G10_Performance();
            G11_OutcomeGradient();

            Log.AppendLine($"\n═══ 合计：{_pass} 通过 / {_fail} 失败 ═══");
            return Log.ToString();
        }

        // ── G1 同 seed 两遍一致（对应 Bug 2 的修复效果）──
        static void G1_Determinism()
        {
            int mism = 0;
            for (ulong s = 0; s < 200; s++)
            {
                ulong a = Run(s), b = Run(s);
                if (a != b) mism++;
            }
            Gate("G1 同 seed 两遍哈希一致（200 seed）", mism == 0,
                mism == 0 ? "0 例不一致" : $"{mism} 例不一致 —— 检查是否有静态状态未重置");
        }

        static ulong Run(ulong seed, int paramOverride = -1)
        {
            var w = new World(seed);
            if (paramOverride >= 0)
            {
                // 把 slot0 固定成一条「必然开火」的法则，再只改它的阈值 ——
                // 这才是定版要的证明：「同 seed，只改一条法的一个参数」
                w.laws[0].condType = 0;      // 饥饿 > N
                w.laws[0].effType = 4;       // 抢最近的邻居
                w.laws[0].effParam = 12;
                w.laws[0].condParam = paramOverride;
            }
            for (int t = 0; t < World.MAXT; t++)
            {
                w.Tick();
                if (w.AliveCount() == 0) break;
            }
            return w.HashState();
        }

        // ── G2 关系对打包双射（修 Bug 1）──
        static void G2_PackPair()
        {
            bool ok = true;
            string detail = "";
            foreach (int n in new[] { 5, 12, 20 })
            {
                int total = n * (n - 1);
                var seen = new bool[total];
                int maxIdx = -1, dup = 0, miss = 0;
                for (int a = 0; a < n; a++)
                    for (int b = 0; b < n; b++)
                    {
                        if (a == b) continue;
                        int p = Targets.PackPair(a, b, n);
                        if (p < 0 || p >= total) { ok = false; detail += $"N={n} 越界 p={p} @({a},{b}) "; }
                        else { if (seen[p]) dup++; seen[p] = true; if (p > maxIdx) maxIdx = p; }
                    }
                for (int k = 0; k < total; k++) if (!seen[k]) miss++;
                // 槽位索引也必须全部落在 NumTargets 内
                int nt = Targets.Count(n);
                int maxSlot = Targets.Slot(n - 1, n - 2, n);
                if (maxSlot >= nt) { ok = false; detail += $"N={n} Slot 越界 {maxSlot} >= {nt} "; }
                if (dup > 0 || miss > 0) { ok = false; detail += $"N={n} dup={dup} miss={miss} "; }
                if (ok && detail.Length == 0) detail += $"N={n} 合法[0,{total - 1}] 满射 无冲突 | Slot 最大 {maxSlot} < {nt}； ";
            }
            Gate("G2 有序对打包是双射、槽位不越界（N=5/12/20）", ok, detail);
        }

        // ── G3 原子写首次保存不抛异常（修 Bug 3）──
        static void G3_AtomicWrite()
        {
            string dir = Path.Combine(Path.GetTempPath(), "simlit_atomic_test");
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                string p = Path.Combine(dir, "sim.json");

                bool ok = true;
                string detail = "";
                try
                {
                    AtomicWrite.Write(p, "{\"first\":1}");            // 首次保存（原实现在这里抛异常）
                    AtomicWrite.Write(p, "{\"second\":2}");           // 第二次（走 Replace 分支）
                    AtomicWrite.Write(p, "{\"third\":3}");            // 第三次
                }
                catch (Exception e) { ok = false; detail = $"抛异常：{e.GetType().Name}: {e.Message}"; }

                if (ok)
                {
                    if (AtomicWrite.Read(p) != "{\"third\":3}") { ok = false; detail = "读回内容不对"; }
                    else detail = "首次/二次/三次保存均成功，读回正确";
                    // 坏档兜底
                    File.WriteAllText(p, "!!corrupted!!");
                    string bak = AtomicWrite.Read(p);
                    if (bak == null) { ok = false; detail = "坏档时未能回落到备份"; }
                }
                Gate("G3 原子写首次保存不抛异常 + 坏档回落", ok, detail);
            }
            catch (Exception e)
            {
                Gate("G3 原子写首次保存不抛异常 + 坏档回落", false, e.Message);
            }
            finally
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            }
        }

        // ── G4 TagBits 16 位 + 伤疤免疫（修 Bug 4）──
        static void G4_TagBits()
        {
            bool ok = true; string detail = "";
            // 高 8 位常量必须能编过（原设计 const byte 1<<8 是编译错误）
            ushort high = TagBits.Taxpayer;
            if (high != 256) { ok = false; detail += "高 8 位常量值不对 "; }

            // 伤疤免疫 RemoveTag
            ushort t = TagBits.Bandit;
            TagBits.ApplyRemoveTag(ref t, TagBits.Bandit);
            if ((t & TagBits.Bandit) != 0) detail += "伤疤免疫 ✓ "; else { ok = false; detail += "伤疤被清掉了 ✗ "; }

            // 性格可被清除
            ushort t2 = TagBits.Greedy;
            TagBits.ApplyRemoveTag(ref t2, TagBits.Greedy);
            if ((t2 & TagBits.Greedy) == 0) detail += "性格可清 ✓"; else { ok = false; detail += "性格清不掉 ✗"; }

            Gate("G4 TagBits 16 位可用 + 伤疤不可清 + 性格可清", ok, detail);
        }

        // ── G5 hitsTotal 单主（修 Bug 5）──
        static void G5_SingleOwnerHitsTotal()
        {
            // 编译期即验证：LawRuntimeView 里没有 hitsTotal 字段。
            // 这里做行为验证：Law.hitsTotal 累加，且与疲劳判定用的是同一个值。
            var w = new World(7);
            for (int t = 0; t < 300; t++) w.Tick();
            int sumSlot = 0;
            for (int s = 0; s < World.SLOTS; s++) sumSlot += w.laws[s].hitsTotal;
            bool fatiguedMatches = true;
            for (int s = 0; s < World.SLOTS; s++)
            {
                bool shouldBeFatigued = w.laws[s].hitsTotal > w.st.popPeak * World.FATIGUE_K;
                bool isFatigued = (w.laws[s].flags & LawFlag.Fatigued) != 0;
                if (shouldBeFatigued != isFatigued) fatiguedMatches = false;
            }
            Gate("G5 hitsTotal 单主 + 疲劳判定同源", sumSlot >= 0 && fatiguedMatches,
                $"累计命中 {sumSlot}，疲劳判定与 Law.hitsTotal 一致：{fatiguedMatches}");
        }

        // ── G6 反事实分岔（给评委的核心证明）──
        static void G6_Divergence()
        {
            int diverged = 0, tested = 0;
            for (ulong s = 0; s < 40; s++)
            {
                ulong h1 = Run(s, 40);
                ulong h2 = Run(s, 60);
                tested++;
                if (h1 != h2) diverged++;
            }
            Gate("G6 只改一条法的阈值 → 村庄轨迹分岔", diverged == tested,
                $"{diverged}/{tested} 个 seed 分岔。这一条不通过，『同 seed 改一个参数跑出两个村子』的证明就拿不出来。");
        }

        // ── G7 500 seed 的形态多样性（D5 gate 的可执行版）──
        static void G7_Diversity()
        {
            var buckets = new System.Collections.Generic.HashSet<int>();
            int aliveSum = 0;
            for (ulong s = 0; s < 500; s++)
            {
                var w = new World(s);
                for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
                var st = w.Stat();
                aliveSum += w.AliveCount();
                int bucket = (w.AliveCount() / 4) * 10000 + ((int)st.sg / 5) * 100 + (int)st.mf / 5;
                buckets.Add(bucket);
            }
            Gate("G7 500 seed 的去重形态数 ≥ 3", buckets.Count >= 3,
                $"形态数 {buckets.Count}，平均终局存活 {aliveSum / 500.0:F1}/20");
        }

        // ── G8 寄存器健康度（五条配平是否生效）──
        // 【为什么不能只看一个村】原来用 new World(1) 单跑一局 —— 但那是**随机律法组合**，
        //   实测有一半组合会把村子打没。拿一个已经死掉的村子去测「寄存器有没有方差」，
        //   测的是空气（Stat 在 0 人时返回全 0）。现在改成：跨 60 个随机村庄，
        //   只看**活下来的**那些，取中位数 —— 这才是「典型村庄的寄存器健康度」。
        static void G8_RegisterHealth()
        {
            int n = 60, living = 0;
            double sg = 0, sh = 0, sf = 0; int usableSum = 0;
            for (ulong s = 0; s < (ulong)n; s++)
            {
                var w = new World(s);
                for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
                if (w.AliveCount() < 3) continue;
                living++;
                var st = w.Stat();
                sg += st.sg; sh += st.sh; sf += st.sf; usableSum += w.UsableConditions();
            }
            if (living == 0) { Gate("G8 三寄存器都有方差 + 可用条件 ≥ 5/10", false, "60 个村庄全部灭村 —— 配平崩了"); return; }
            sg /= living; sh /= living; sf /= living; usableSum /= living;
            bool ok = sg > 8 && sh > 5 && sf > 5 && usableSum >= 5;
            Gate("G8 存活村庄的三寄存器方差 + 可用条件 ≥ 5/10", ok,
                $"（{living}/{n} 个村庄活到终局）均值 σ: 金币 {sg:F1} / 饥饿 {sh:F1} / 恐惧 {sf:F1}；可用条件 {usableSum}/10");
        }

        // ── G11 结局梯度（防止退回「非 0 即 20」的两极）──
        // 这条门是本次结构改动（承载力模型 + 个人存粮）的守护者。
        // 没有它，任何一次「顺手调个数值」都可能把结局重新压回两极，而且不会报错。
        static void G11_OutcomeGradient()
        {
            int n = 200;
            var hist = new int[21];
            int polar = 0;
            for (ulong s = 0; s < (ulong)n; s++)
            {
                var w = new World(s);
                for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
                int a = w.AliveCount();
                hist[a]++;
                if (a == 0 || a == World.N) polar++;
            }
            int buckets = 0; for (int k = 0; k <= 20; k++) if (hist[k] > 0) buckets++;
            double polarShare = polar / (double)n;
            bool ok = polarShare <= 0.55 && buckets >= 8;
            Gate("G11 200 个随机村庄的结局是一条梯度而非两极", ok,
                $"两极占比 {polarShare * 100:F0}%（门槛 ≤55%）；去重档位 {buckets}（门槛 ≥8）");
        }

        // ── G9 Tick 内零分配 ──
        static void G9_ZeroAlloc()
        {
            var w = new World(1);
            for (int t = 0; t < 200; t++) w.Tick();          // 预热
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int t = 0; t < 1000; t++) w.Tick();
            long after = GC.GetAllocatedBytesForCurrentThread();
            long perTick = (after - before) / 1000;
            Gate("G9 Tick 稳态零 GC 分配", perTick <= 64,
                $"每 tick 分配 {perTick} 字节（门槛 ≤64B）");
        }

        // ── G10 性能 ──
        static void G10_Performance()
        {
            var w = new World(1);
            for (int t = 0; t < 500; t++) w.Tick();          // 预热
            var sw = Stopwatch.StartNew();
            int ticks = 0;
            for (int rep = 0; rep < 40; rep++)
            {
                var ww = new World((ulong)rep);
                for (int t = 0; t < World.MAXT; t++) { ww.Tick(); ticks++; if (ww.AliveCount() == 0) break; }
            }
            sw.Stop();
            double usPerTick = sw.Elapsed.TotalMilliseconds * 1000.0 / ticks;
            double armUs = usPerTick * 15;                    // 保守按 ARM 慢 15 倍
            Gate("G10 单 tick 耗时（推算低端 ARM）< 500µs", armUs < 500,
                $"x64 {usPerTick:F2} µs/tick → 推算 ARM {armUs:F0} µs/tick；整局 2400 tick ≈ {armUs * 2400 / 1000000.0:F2} 秒");
        }
    }
}
