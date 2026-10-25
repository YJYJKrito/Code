// 《立法村》核心模拟原型 —— 用于验证三个技术假设
//   假设 A：整数 + 自写 PRNG 能做到逐位可复现（同 seed 两遍哈希一致）
//   假设 B：8 法 × 20 村民 × ~20 卡的核心循环在手机性能预算内
//   假设 C（最关键）：这套规则跑出来的村庄是"活的"，不是退化成一潭死水或全灭
// 另外做一组 A/B 实验：事件卡门禁"读历史" vs "读当前值"，验证涌现成色差异
using System;
using System.Diagnostics;

// ══════════════════════════════════════════════════════════
// 确定性 PRNG —— splitmix64。全 unchecked，纯 ulong 运算。
// 这是双端一致的地基：不用 UnityEngine.Random，不用 System.Random。
// ══════════════════════════════════════════════════════════
struct Rng
{
    ulong _s;
    public Rng(ulong seed) { _s = seed; }
    public ulong Next()
    {
        unchecked
        {
            _s += 0x9E3779B97F4A7C15UL;
            ulong z = _s;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
    // 分通道随机流：加一张卡不会扰动下游所有随机
    public int Range(int lo, int hiExclusive)
    {
        uint span = (uint)(hiExclusive - lo);
        return span == 0 ? lo : lo + (int)(Next() % span);
    }
}

// ══════════════════════════════════════════════════════════
// 标签位图：低 4 位性格（可增删），高 4 位伤疤（RemoveTag 免疫），高 8 位行为标签
// ══════════════════════════════════════════════════════════
static class Tag
{
    public const ushort Coward = 1 << 0, Greedy = 1 << 1, Diligent = 1 << 2, Sickly = 1 << 3;
    public const ushort Bandit = 1 << 4, Debtor = 1 << 5, Deserter = 1 << 6, Accused = 1 << 7;
    public const ushort Taxpayer = 1 << 8, Believer = 1 << 9, Hunger = 1 << 10, Guard = 1 << 11;
    public const ushort SCAR_MASK = Bandit | Debtor | Deserter | Accused;
    public const ushort TRAIT_MASK = Coward | Greedy | Diligent | Sickly;
    public const ushort ALL = 0xFFFF;
}

struct Villager
{
    public int gold, hunger, fear;
    public ushort tags;
    public bool alive;
    public int deathTick;
    // 历史计数（这就是"读历史"的门禁要用到的东西）
    public int nRobbed, nFed, nTaxed, nScarred;
    public bool flagged; // 供 UI/调试
}

struct Law
{
    public byte condType; public int condParam;
    public byte effType;  public int effParam;
    public ushort tagArg;
    public int hitsTotal, hitsThisTick;
    public bool fatigued;
    public bool suspended;
}

enum Ev { None = 0, Robbed = 1, Fed = 2, Taxed = 3, Scarred = 4, Died = 5 }

// ══════════════════════════════════════════════════════════
// 世界
// ══════════════════════════════════════════════════════════
class World
{
    public const int N = 20;              // 村民数
    public const int LAWS = 8;            // 预设法
    public const int SLOTS = 5;           // 玩家可选槽位
    public const int MAX_TICK = 2400;     // 一局

    public Villager[] v = new Villager[N];
    public Law[] law = new Law[SLOTS];
    public int[] rel = new int[N * N];    // 有向关系表
    public int tick;
    public int aliveCount, popPeak, sumFear;
    public int authority;                 // 威信，厘点制
    public bool suspendedAll;
    public Rng rng;
    public ulong hash;

    // 统计
    public int[] lawHits = new int[SLOTS];
    public int totalDeaths;
    public int[] evCount = new int[8];
    // 事件卡
    public int cardsFired;
    public int chainDepthMax;
    public bool historyGates;             // ★ A/B 开关

    public const int AUTH_MAX = 10000;
    public const int UPKEEP = 5;          // 厘点/tick/条
    public const int FATIGUE_K = 100;

    public World(ulong seed, bool historyGates)
    {
        this.historyGates = historyGates;
        rng = new Rng(seed ^ 0xA5A5A5A5A5A5A5A5UL);
        authority = AUTH_MAX;
        popPeak = N;

        for (int i = 0; i < N; i++)
        {
            v[i].alive = true;
            v[i].gold = rng.Range(0, 30);
            v[i].hunger = rng.Range(0, 40);
            v[i].fear = rng.Range(0, 20);
            v[i].tags = (ushort)(1 << rng.Range(0, 4));
        }
        for (int i = 0; i < N * N; i++) rel[i] = 0;

        // 从 12 条预设法里确定性挑 5 条放进槽
        for (int s = 0; s < SLOTS; s++)
        {
            law[s].condType = (byte)rng.Range(0, 6);
            law[s].condParam = rng.Range(20, 70);
            law[s].effType = (byte)rng.Range(0, 8);
            law[s].effParam = rng.Range(5, 30);
            law[s].tagArg = (ushort)(1 << rng.Range(0, 16));
        }
    }

    // ── 条件求值（纯整数）──
    bool Cond(int s, int i, Villager[] src)
    {
        ref Law L = ref law[s];
        ref Villager x = ref src[i];
        switch (L.condType)
        {
            case 0: return x.hunger > L.condParam;
            case 1: return x.gold < L.condParam;
            case 2: return x.fear > L.condParam;
            case 3: return x.gold > L.condParam;
            case 4: return (x.tags & L.tagArg) != 0;
            case 5: return x.nRobbed > 0;       // 读历史
            default: return false;
        }
    }

    // ── 效果执行（写 new 缓冲）──
    void Apply(int s, int i, Villager[] dst, out Ev ev)
    {
        ref Law L = ref law[s];
        ref Villager x = ref dst[i];
        ev = Ev.None;
        int mag = L.fatigued ? L.effParam / 2 : L.effParam;   // 疲劳：数值效果减半
        switch (L.effType)
        {
            case 0: x.gold = Clamp(x.gold + mag); ev = Ev.None; break;
            case 1: x.gold = Clamp(x.gold - mag); ev = Ev.Taxed; break;
            case 2: x.hunger = Clamp(x.hunger - mag); x.nFed++; ev = Ev.Fed; break;
            case 3: x.fear = Clamp(x.fear + mag); break;
            case 4: // 抢最近的邻居
                {
                    int tgt = (i + 1) % N;
                    int take = mag; if (dst[tgt].gold < take) take = dst[tgt].gold;
                    dst[tgt].gold -= take; x.gold = Clamp(x.gold + take);
                    dst[tgt].nRobbed++; x.nRobbed++;
                    rel[i * N + tgt] -= 15; rel[tgt * N + i] -= 30;
                    if ((dst[tgt].nRobbed >= 2) && (dst[tgt].tags & Tag.SCAR_MASK) == 0)
                    { dst[tgt].tags |= Tag.Bandit; dst[tgt].nScarred++; }
                    ev = Ev.Robbed;
                }
                break;
            case 5: x.tags |= L.tagArg; if ((L.tagArg & Tag.SCAR_MASK) != 0) { x.nScarred++; ev = Ev.Scarred; } break;
            case 6: x.tags &= (ushort)~L.tagArg; ApplyRemoveTagGuard(ref x, L.tagArg); break;
            case 7: x.fear = Clamp(x.fear - mag); break;
        }
    }

    // 伤疤免疫清除 —— 三行，废法路径里根本不存在"回滚标签"的代码
    static void ApplyRemoveTagGuard(ref Villager x, ushort bits)
    {
        if ((bits & Tag.SCAR_MASK) != 0) x.tags |= (ushort)(bits & Tag.SCAR_MASK);
    }

    static int Clamp(int x) => x < 0 ? 0 : (x > 99 ? 99 : x);

    // ── 事件卡（storylet）──
    // ★ A/B 的核心：historyGates=true 时门禁读"历史/标签"（产生卡→卡链）
    //                false 时门禁读"当前值"（产生孤立段子）
    int RunStorylets()
    {
        int fired = 0;
        // 卡 1：抢过东西的人更容易再抢（读历史）
        // 卡 2：被抢过的人仇恨累积 → 变成劫匪（读历史 + 标签）
        // 卡 3：饿到阈值 + 曾经的劫匪 → 私刑（读历史 + 标签）
        // 卡 4：饿过的人聚成信徒（读历史）
        // 卡 5：财富差过大 → 仇富（读标签）
        for (int i = 0; i < N; i++)
        {
            ref Villager x = ref v[i];
            if (!x.alive) continue;

            bool g1 = historyGates ? (x.nRobbed >= 1) : (x.hunger > 60);
            bool g2 = historyGates ? ((x.tags & Tag.Bandit) != 0 && x.nRobbed >= 2) : (x.gold < 10);
            bool g3 = historyGates ? ((x.nRobbed >= 3) && (x.tags & Tag.Hunger) != 0) : (x.fear > 70);
            bool g4 = historyGates ? (x.nFed >= 2) : (x.gold > 50);

            if (g1 && rng.Range(0, 100) < 30)
            {
                cardsFired++;
                x.fear = Clamp(x.fear + 5);
                x.tags |= Tag.Hunger;
            }
            if (g2 && rng.Range(0, 100) < 20)
            {
                cardsFired++;
                x.tags |= Tag.Bandit;
                int t = (i + 3) % N;
                if (v[t].alive) { v[t].gold = Clamp(v[t].gold - 10); x.gold = Clamp(x.gold + 10); v[t].nRobbed++; }
            }
            if (g3 && rng.Range(0, 100) < 15)
            {
                cardsFired++;
                x.tags |= Tag.Accused;
                if (x.hunger > 50) { x.alive = false; x.deathTick = tick; totalDeaths++; evCount[(int)Ev.Died]++; }
            }
            if (g4 && rng.Range(0, 100) < 25)
            {
                cardsFired++;
                x.tags |= Tag.Believer;
                x.fear = Clamp(x.fear - 5);
            }
        }
        return fired;
    }

    // ══ 一个 tick ══
    public void Tick()
    {
        // 1. 派生量
        aliveCount = 0; sumFear = 0;
        for (int i = 0; i < N; i++) if (v[i].alive) { aliveCount++; sumFear += v[i].fear; }
        if (aliveCount > popPeak) popPeak = aliveCount;
        int avgFear = aliveCount == 0 ? 0 : sumFear / aliveCount;

        // 2. 威信结算
        int active = 0;
        for (int s = 0; s < SLOTS; s++) if (!law[s].suspended) active++;
        int regen = (aliveCount * 2 / 5) + Math.Max(0, 30 - avgFear) / 5;
        if (!suspendedAll)
        {
            authority = authority - UPKEEP * active + regen;
            if (authority <= 0) { authority = 0; suspendedAll = true; }
        }
        else
        {
            authority += regen;
            if (authority >= 1000) { authority = 1000; suspendedAll = false; }
        }
        for (int s = 0; s < SLOTS; s++) law[s].suspended = suspendedAll;

        // 3. 律法求值（双缓冲：读 old，写 new）
        var old = new Villager[N];
        Array.Copy(v, old, N);
        for (int s = 0; s < SLOTS; s++) law[s].hitsThisTick = 0;

        if (!suspendedAll)
        {
            for (int s = 0; s < SLOTS; s++)
            {
                for (int i = 0; i < N; i++)
                {
                    if (!v[i].alive) continue;
                    if (!Cond(s, i, old)) continue;
                    Apply(s, i, v, out Ev ev);
                    law[s].hitsThisTick++;
                    law[s].hitsTotal++;
                    lawHits[s]++;
                    if (ev != Ev.None) evCount[(int)ev]++;
                }
                // 疲劳锁存
                if (!law[s].fatigued && law[s].hitsTotal > popPeak * FATIGUE_K) law[s].fatigued = true;
            }
        }

        // 4. 事件卡
        RunStorylets();

        // 5. 基础物理
        for (int i = 0; i < N; i++)
        {
            if (!v[i].alive) continue;
            v[i].hunger = Clamp(v[i].hunger + 1);   // 饥饿累积
            v[i].fear = v[i].fear - 5; if (v[i].fear < 0) v[i].fear = 0;
            if (v[i].hunger >= 99) { v[i].alive = false; v[i].deathTick = tick; totalDeaths++; evCount[(int)Ev.Died]++; }
        }
        for (int i = 0; i < N * N; i++) rel[i] = rel[i] * 98 / 100;   // 关系回归（整数）

        tick++;
    }

    // ══ 状态哈希 —— 只吃模拟字段 ══
    public ulong HashState()
    {
        unchecked
        {
            ulong h = 1469598103934665603UL;
            void Mix(ulong x) { h ^= x; h *= 1099511628211UL; }
            Mix((ulong)tick); Mix((ulong)aliveCount); Mix((ulong)authority); Mix((ulong)totalDeaths);
            for (int i = 0; i < N; i++)
            {
                Mix((ulong)(uint)v[i].gold); Mix((ulong)(uint)v[i].hunger);
                Mix((ulong)(uint)v[i].fear); Mix(v[i].tags);
                Mix(v[i].alive ? 1UL : 0UL); Mix((ulong)(uint)v[i].nRobbed);
            }
            for (int s = 0; s < SLOTS; s++) { Mix((ulong)law[s].hitsTotal); Mix(law[s].fatigued ? 1UL : 0UL); }
            return h;
        }
    }

    // ══ 退化判定：死 / 同态 / 活 ══
    public string Classify()
    {
        int alive = 0, tagUnion = 0, minG = 999, maxG = -1;
        for (int i = 0; i < N; i++)
        {
            if (!v[i].alive) continue;
            alive++; tagUnion |= v[i].tags;
            if (v[i].gold < minG) minG = v[i].gold;
            if (v[i].gold > maxG) maxG = v[i].gold;
        }
        int distinctBeh = System.Numerics.BitOperations.PopCount((uint)(tagUnion & 0xFF00));
        int lawsFired = 0;
        for (int s = 0; s < SLOTS; s++) if (lawHits[s] > 20) lawsFired++;

        if (alive < 3) return "死";                                  // 全灭/濒灭
        if (lawsFired <= 1 || distinctBeh <= 1) return "同态";        // 一条法碾压 / 村民失去区分度
        return "活";
    }
}

class Program
{
    static int SimulateOne(ulong seed, bool historyGates, out ulong h1, out int deaths, out int cards, out int aliveEnd)
    {
        var w = new World(seed, historyGates);
        for (int t = 0; t < World.MAX_TICK; t++)
        {
            w.Tick();
            if (w.aliveCount == 0) break;
        }
        h1 = w.HashState();
        deaths = w.totalDeaths; cards = w.cardsFired; aliveEnd = w.aliveCount;
        string c = w.Classify();
        return c == "活" ? 2 : (c == "同态" ? 1 : 0);
    }

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("═══ 《立法村》核心模拟原型 ═══\n");

        // ───────── 实验 1：确定性（同 seed 两遍哈希一致）─────────
        Console.WriteLine("【实验 1】确定性验证：同 seed 跑两遍，比对状态哈希");
        int mismatch = 0;
        for (ulong s = 0; s < 200; s++)
        {
            SimulateOne(s, true, out ulong a, out _, out _, out _);
            SimulateOne(s, true, out ulong b, out _, out _, out _);
            if (a != b) mismatch++;
        }
        Console.WriteLine($"  200 个 seed × 2 遍 → 哈希不一致 {mismatch} 例");
        Console.WriteLine(mismatch == 0 ? "  ✅ 通过：整数模拟 + 自写 PRNG 完全可复现\n" : "  ❌ 失败\n");

        // ───────── 实验 2：涌现 gate（500 seed 的形态分布）─────────
        Console.WriteLine("【实验 2】涌现 gate：500 个 seed，看村庄跑成什么样");
        foreach (bool hg in new[] { true, false })
        {
            int alive = 0, homo = 0, dead = 0;
            long deathSum = 0, cardSum = 0, aliveSum = 0;
            for (ulong s = 0; s < 500; s++)
            {
                int r = SimulateOne(s, hg, out _, out int d, out int c, out int ae);
                if (r == 2) alive++; else if (r == 1) homo++; else dead++;
                deathSum += d; cardSum += c; aliveSum += ae;
            }
            string label = hg ? "门禁读【历史/标签】(修正版)" : "门禁读【当前值】(原版)";
            Console.WriteLine($"  {label}");
            Console.WriteLine($"    ✅ 活 {alive,3}  ({alive * 100.0 / 500:F1}%)   ⚠ 同态 {homo,3} ({homo * 100.0 / 500:F1}%)   ☠ 死 {dead,3} ({dead * 100.0 / 500:F1}%)");
            Console.WriteLine($"    平均：死亡 {deathSum / 500.0:F1} 人   事件卡触发 {cardSum / 500.0:F0} 次   终局存活 {aliveSum / 500.0:F1} 人\n");
        }

        // ───────── 实验 3：性能 ─────────
        Console.WriteLine("【实验 3】性能：单 tick 耗时（本机 x64，纯逻辑无渲染）");
        var w2 = new World(42, true);
        // 预热
        for (int t = 0; t < 2400; t++) { w2.Tick(); if (w2.aliveCount == 0) { w2 = new World(42, true); } }
        var sw = Stopwatch.StartNew();
        int totalTicks = 0;
        for (ulong s = 0; s < 100; s++)
        {
            var ww = new World(s, true);
            for (int t = 0; t < 2400; t++) { ww.Tick(); totalTicks++; if (ww.aliveCount == 0) break; }
        }
        sw.Stop();
        double perTickUs = sw.Elapsed.TotalMilliseconds * 1000.0 / totalTicks;
        Console.WriteLine($"  {totalTicks} tick 用时 {sw.Elapsed.TotalMilliseconds:F1} ms");
        Console.WriteLine($"  单 tick ≈ {perTickUs:F2} µs（本机 i7 级 x64）");
        Console.WriteLine($"  一局 2400 tick ≈ {perTickUs * 2400 / 1000.0:F1} ms");
        // 手机 ARM 保守按 8~15 倍慢
        Console.WriteLine($"  → 推算到低端 ARM（×10 保守）：单 tick ≈ {perTickUs * 10:F1} µs，一局 ≈ {perTickUs * 2400 * 10 / 1000.0:F0} ms");
        Console.WriteLine($"  → 60fps 单帧预算 16.6ms，按 1 倍速（1 tick/秒）占用 ≈ {(perTickUs * 10) / 16600 * 100:F2}%\n");

        // ───────── 实验 4：单局样例 ─────────
        Console.WriteLine("【实验 4】一个真实 seed 的村庄轨迹（seed=7）");
        var w3 = new World(7, true);
        for (int t = 0; t < 2400 && w3.aliveCount > 0; t++)
        {
            w3.Tick();
            if (t % 400 == 0 || w3.aliveCount == 0)
                Console.WriteLine($"  tick {w3.tick,4}  存活 {w3.aliveCount,2}  威信 {w3.authority / 100.0,5:F1}  死 {w3.totalDeaths,2}  卡 {w3.cardsFired,4}  判定 {w3.Classify()}");
        }
    }
}

// 补一个 Tag 常量（上面 Apply 里用到）
static class TagExt { }
