// v2 原型：加上粮食生产闭环 + 参数扫描 + 故事卡数量对性能的影响
// 目的：找出"村庄能活下来"的参数区域（D5 gate 的实际扫参工作）
using System;
using System.Diagnostics;

struct R2
{
    ulong _s;
    public R2(ulong seed) { _s = seed; }
    public ulong Next() { unchecked { _s += 0x9E3779B97F4A7C15UL; ulong z = _s; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); } }
    public int Range(int lo, int hi) { uint sp = (uint)(hi - lo); return sp == 0 ? lo : lo + (int)(Next() % sp); }
}

class V2
{
    public int gold, hunger, fear;
    public ushort tags;
    public bool alive;
    public int nRobbed, nFed, nProd;
}

class W2
{
    public const int N = 20, SLOTS = 5, MAXT = 2400;
    public V2[] v = new V2[N];
    public int[] ct = new int[SLOTS], cp = new int[SLOTS], et = new int[SLOTS], ep = new int[SLOTS];
    public int granary, tick, aliveCount, popPeak, totalDeaths, cardsFired;
    public int authority;
    public R2 rng;
    public bool suspended;
    public int[] lawHits = new int[SLOTS];
    public int storylets;

    // ★ 可扫的参数
    public int HUNGER_RATE;
    public int FOOD_PER_TICK;
    public int EAT_COST;
    public int EAT_GAIN;

    public W2(ulong seed, int hungerRate, int foodPer, int eatCost, int eatGain, int storylets)
    {
        HUNGER_RATE = hungerRate; FOOD_PER_TICK = foodPer; EAT_COST = eatCost; EAT_GAIN = eatGain;
        this.storylets = storylets;
        rng = new R2(seed ^ 0x123456789ABCDEFUL);
        authority = 10000; popPeak = N; granary = 120;
        for (int i = 0; i < N; i++)
        {
            v[i] = new V2 { alive = true, gold = rng.Range(0, 30), hunger = rng.Range(0, 30), fear = rng.Range(0, 20), tags = (ushort)(1 << rng.Range(0, 4)) };
        }
        for (int s = 0; s < SLOTS; s++)
        {
            ct[s] = rng.Range(0, 6); cp[s] = rng.Range(20, 70);
            et[s] = rng.Range(0, 9); ep[s] = rng.Range(5, 30);
        }
    }

    static int C(int x) => x < 0 ? 0 : (x > 99 ? 99 : x);

    bool Cond(int s, int i, V2[] o)
    {
        var x = o[i];
        switch (ct[s])
        {
            case 0: return x.hunger > cp[s];
            case 1: return x.gold < cp[s];
            case 2: return x.fear > cp[s];
            case 3: return x.gold > cp[s];
            case 4: return (x.tags & 0xF0) != 0;      // 有伤疤
            case 5: return x.nRobbed > 0;             // 读历史
        }
        return false;
    }

    void Apply(int s, int i)
    {
        var x = v[i];
        int mag = ep[s];
        switch (et[s])
        {
            case 0: x.gold = C(x.gold + mag); break;
            case 1: x.gold = C(x.gold - mag); break;
            case 2: // 从粮仓取食
                if (granary >= 10) { granary -= 10; x.hunger = C(x.hunger - mag); x.nFed++; }
                break;
            case 3: x.fear = C(x.fear + mag); break;
            case 4: // 抢邻居
                {
                    int t = (i + 1) % N;
                    if (v[t].alive) { int take = mag; if (v[t].gold < take) take = v[t].gold; v[t].gold -= take; x.gold = C(x.gold + take); v[t].nRobbed++; x.nRobbed++; if (v[t].nRobbed >= 2) v[t].tags |= 1 << 4; }
                    break;
                }
            case 5: x.tags |= (ushort)(1 << rng.Range(4, 8)); break;   // 打伤疤
            case 6: x.fear = C(x.fear - mag); break;
            case 7: // 耕作：往粮仓加粮
                granary = Math.Min(granary + mag, 400); x.nProd++; break;
            case 8: // 均贫：把多的给穷的
                { int t = (i + 1) % N; if (v[t].alive && x.gold > v[t].gold + 10) { int mv = (x.gold - v[t].gold) / 2; x.gold -= mv; v[t].gold = C(v[t].gold + mv); } break; }
        }
    }

    public void Tick()
    {
        aliveCount = 0;
        for (int i = 0; i < N; i++) if (v[i].alive) aliveCount++;
        if (aliveCount > popPeak) popPeak = aliveCount;

        // 威信
        int regen = aliveCount * 2 / 5;
        if (!suspended) { authority -= 5 * SLOTS; authority += regen; if (authority <= 0) { authority = 0; suspended = true; } }
        else { authority += regen; if (authority >= 1000) suspended = false; }

        // 生产
        for (int i = 0; i < N; i++) if (v[i].alive) granary = Math.Min(granary + FOOD_PER_TICK, 500);

        // 律法
        var old = new V2[N];
        for (int i = 0; i < N; i++) old[i] = v[i];
        if (!suspended)
        {
            for (int s = 0; s < SLOTS; s++)
            {
                for (int i = 0; i < N; i++)
                {
                    if (!v[i].alive) continue;
                    if (!Cond(s, i, old)) continue;
                    Apply(s, i); lawHits[s]++;
                }
            }
        }

        // 事件卡（数量可扫，用于性能测试）
        for (int k = 0; k < storylets; k++)
        {
            int i = rng.Range(0, N);
            if (!v[i].alive) continue;
            if (v[i].nRobbed >= 1 && rng.Range(0, 100) < 8) { cardsFired++; v[i].fear = C(v[i].fear + 4); }
            if (v[i].nFed >= 2 && rng.Range(0, 100) < 6) { cardsFired++; v[i].tags |= 1 << 9; }
        }

        // 物理：饥饿 + 吃
        for (int i = 0; i < N; i++)
        {
            if (!v[i].alive) continue;
            v[i].hunger = C(v[i].hunger + HUNGER_RATE);
            if (v[i].hunger > 50 && granary >= EAT_COST) { granary -= EAT_COST; v[i].hunger = C(v[i].hunger - EAT_GAIN); }
            v[i].fear = Math.Max(0, v[i].fear - 3);
            if (v[i].hunger >= 99) { v[i].alive = false; totalDeaths++; }
        }
        tick++;
    }

    public bool IsAlive()
    {
        int a = 0, tu = 0;
        for (int i = 0; i < N; i++) if (v[i].alive) { a++; tu |= v[i].tags; }
        int beh = System.Numerics.BitOperations.PopCount((uint)(tu & 0xFF00));
        int fired = 0; for (int s = 0; s < SLOTS; s++) if (lawHits[s] > 20) fired++;
        return a >= 8 && fired >= 2 && beh >= 2;   // 存活过半 + 多条法在起作用 + 村民有区分度
    }
    public int Alive() { int a = 0; for (int i = 0; i < N; i++) if (v[i].alive) a++; return a; }
}

class Sweep
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("═══ v2 参数扫描：找出村庄能活下来的区域 ═══\n");
        Console.WriteLine("（配置：20 村民 / 5 法槽 / 初始粮仓 120 / 判活 = 存活≥8 且 ≥2 条法在起作用 且 ≥2 种行为标签）\n");

        Console.WriteLine("饥饿速率 | 产食/tick | 吃饭成本/收益 | 判活率 | 平均终局存活");
        Console.WriteLine("---------|-----------|---------------|--------|-------------");
        foreach (int hr in new[] { 2, 3, 4, 5 })
            foreach (int fp in new[] { 1, 2 })
                foreach (var (cost, gain) in new[] { (5, 25), (10, 40), (15, 60) })
                {
                    int alive = 0; long surv = 0;
                    const int SEEDS = 200;
                    for (ulong s = 0; s < SEEDS; s++)
                    {
                        var w = new W2(s, hr, fp, cost, gain, 4);
                        for (int t = 0; t < W2.MAXT && w.Alive() > 0; t++) w.Tick();
                        if (w.IsAlive()) alive++;
                        surv += w.Alive();
                    }
                    Console.WriteLine($"   {hr,2}    |    {fp}      |   {cost,2} / {gain,2}     | {alive * 100.0 / SEEDS,5:F1}% |   {surv / (double)SEEDS,4:F1} 人");
                }

        Console.WriteLine("\n═══ 性能：故事卡数量对单 tick 耗时的影响 ═══");
        Console.WriteLine("卡数 | 单 tick(µs, x64) | 推算低端 ARM(×10) | 一局2400tick(ARM)");
        Console.WriteLine("-----|------------------|-------------------|------------------");
        foreach (int sc in new[] { 0, 4, 20, 40, 80 })
        {
            var w = new W2(42, 3, 2, 10, 40, sc);
            for (int t = 0; t < 800; t++) w.Tick();     // 预热
            var sw2 = Stopwatch.StartNew();
            int ticks = 0;
            for (int rep = 0; rep < 30; rep++)
            {
                var ww = new W2((ulong)rep, 3, 2, 10, 40, sc);
                for (int t = 0; t < W2.MAXT && ww.Alive() > 0; t++) { ww.Tick(); ticks++; }
            }
            sw2.Stop();
            double us = sw2.Elapsed.TotalMilliseconds * 1000.0 / ticks;
            Console.WriteLine($" {sc,3} |      {us,6:F2}      |      {us * 10,6:F1}       |    {us * 2400 * 10 / 1000.0,5:F0} ms");
        }
    }
}
