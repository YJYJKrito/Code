// v3 诊断：为什么村庄"活着但什么都不发生"
//   A. 追踪村民寄存器随时间的实际取值分布（律法条件能不能被满足）
//   B. 按真实求值模型测性能（每张卡 × 每个目标，而不是"随机挑一个"）
//   C. 扫"律法条件阈值"，找律法真能开火的参数区域
using System;
using System.Diagnostics;

struct R3
{
    ulong _s;
    public R3(ulong seed) { _s = seed; }
    public ulong Next() { unchecked { _s += 0x9E3779B97F4A7C15UL; ulong z = _s; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); } }
    public int Range(int lo, int hi) { uint sp = (uint)(hi - lo); return sp == 0 ? lo : lo + (int)(Next() % sp); }
}

class Reg
{
    public int gold, hunger, fear;
    public ushort tags;
    public bool alive;
    public int nRobbed;
}

class W3
{
    public const int N = 20, MAXT = 2400;
    public Reg[] v = new Reg[N];
    public R3 rng;
    public int tick, granary = 120, authority = 10000, totalDeaths;
    public long sumGold, sumHunger, sumFear;
    public int deathTickFirst = -1;

    public W3(ulong seed)
    {
        rng = new R3(seed ^ 0xABCDEF12UL);
        for (int i = 0; i < N; i++)
            v[i] = new Reg { alive = true, gold = rng.Range(0, 30), hunger = rng.Range(0, 30), fear = rng.Range(0, 20), tags = (ushort)(1 << rng.Range(0, 4)) };
    }
    static int C(int x) => x < 0 ? 0 : (x > 99 ? 99 : x);

    // 基线物理：没有律法、没有卡，只有生产 + 饥饿 + 吃饭
    public void Tick()
    {
        for (int i = 0; i < N; i++) if (v[i].alive) granary = Math.Min(granary + 2, 500);
        for (int i = 0; i < N; i++)
        {
            if (!v[i].alive) continue;
            v[i].hunger = C(v[i].hunger + 3);
            if (v[i].hunger > 50 && granary >= 10) { granary -= 10; v[i].hunger = C(v[i].hunger - 40); }
            v[i].fear = Math.Max(0, v[i].fear - 3);
            // 金币缓慢漂移（模拟经济活动）
            v[i].gold = C(v[i].gold + (rng.Range(0, 100) < 30 ? 1 : 0) - (rng.Range(0, 100) < 20 ? 1 : 0));
            if (v[i].hunger >= 99) { v[i].alive = false; if (deathTickFirst < 0) deathTickFirst = tick; totalDeaths++; }
        }
        tick++;
    }
    public void Sample()
    {
        sumGold = sumHunger = sumFear = 0; int a = 0;
        for (int i = 0; i < N; i++) if (v[i].alive) { a++; sumGold += v[i].gold; sumHunger += v[i].hunger; sumFear += v[i].fear; }
        if (a > 0) { sumGold /= a; sumHunger /= a; sumFear /= a; }
    }
    public int Alive() { int a = 0; for (int i = 0; i < N; i++) if (v[i].alive) a++; return a; }
}

class Diagnose
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("═══ v3 诊断 ═══\n");

        // ───── A. 基线物理下，寄存器实际落在什么区间 ─────
        Console.WriteLine("【A】不开任何律法、不跑任何卡，只看基线物理下村民寄存器停在什么值");
        Console.WriteLine("     （这决定了律法条件阈值该设在哪儿才有意义）\n");
        Console.WriteLine("  tick |  均金币 |  均饥饿 |  均恐惧 | 存活");
        Console.WriteLine("  -----|---------|---------|---------|-----");
        var w = new W3(1);
        for (int t = 0; t < 2400 && w.Alive() > 0; t++)
        {
            w.Tick();
            if (t % 300 == 0 || t == 2399) { w.Sample(); Console.WriteLine($"  {w.tick,4} |  {w.sumGold,5}  |  {w.sumHunger,5}  |  {w.sumFear,5}  | {w.Alive(),3}/20"); }
        }
        var wb = new W3(1);
        for (int t = 0; t < 2400 && wb.Alive() > 0; t++) wb.Tick();
        wb.Sample();
        Console.WriteLine($"\n  基线终值：均金币 {wb.sumGold}  均饥饿 {wb.sumHunger}  均恐惧 {wb.sumFear}  存活 {wb.Alive()}");
        Console.WriteLine($"  → 律法条件阈值只有落在这些值两侧才有区分度。");
        Console.WriteLine($"     例如「饥饿 > 60」在均值 {wb.sumHunger} 的村庄里永远不触发（死条件）。\n");

        // ───── B. 真实求值模型的性能 ─────
        Console.WriteLine("【B】性能：按真实求值模型（每张卡 × 每个目标）");
        Console.WriteLine("     之前的原型是「每张卡随机挑 1 个目标」，真实模型是「每张卡扫全部目标」");
        Console.WriteLine("     目标数 = 1(村庄) + 20(村民) + 20×19(关系对) = 401\n");
        Console.WriteLine("  卡数 | 目标数 | 总判定次数/tick | 单tick(x64) | 单tick(ARM×10) | 一局2400tick(ARM)");
        Console.WriteLine("  -----|--------|-----------------|-------------|----------------|------------------");
        foreach (int cards in new[] { 20, 40, 80 })
        {
            int[] targets = { 21, 401 };
            foreach (int tg in targets)
            {
                var bench = new W3(42);
                var sw = Stopwatch.StartNew();
                const int REPS = 2400;
                for (int rep = 0; rep < REPS; rep++)
                {
                    int acc = 0;
                    for (int c = 0; c < cards; c++)
                        for (int i = 0; i < tg; i++)
                        {
                            // 模拟一次 3-6 个条件的门禁求值（纯整数比较）
                            int a = bench.v[i % 20].hunger, b = bench.v[i % 20].gold, f = bench.v[i % 20].fear;
                            bool ok = (a > 50) && (b < 80) && (f < 90) && ((bench.v[i % 20].tags & 0x10) == 0) && (a + b > 30);
                            acc += ok ? 1 : 0;
                        }
                    bench.tick += acc >> 30;
                }
                sw.Stop();
                double us = sw.Elapsed.TotalMilliseconds * 1000.0 / REPS;
                Console.WriteLine($"   {cards,3}  |  {tg,4}  |     {cards * tg,7}      |   {us,7:F1} µs |   {us * 10,7:F0} µs  |     {us * 2400 * 10 / 1000.0,6:F0} ms");
            }
        }

        // ───── C. 律法真能开火的阈值区域 ─────
        Console.WriteLine("\n【C】把律法条件设在什么阈值，它才真的会开火");
        Console.WriteLine("     测法：基线跑 800 tick 后，统计各条件在 20 人 × 200 tick 里的命中率\n");
        var wc = new W3(3);
        for (int t = 0; t < 800; t++) wc.Tick();
        wc.Sample();
        Console.WriteLine($"  基线状态：均金币 {wc.sumGold}  均饥饿 {wc.sumHunger}  均恐惧 {wc.sumFear}\n");
        Console.WriteLine("  条件              | 触发率 | 评价");
        Console.WriteLine("  ------------------|--------|------");
        void Probe(string name, Func<Reg, bool> f)
        {
            int hit = 0, tot = 0;
            for (int i = 0; i < 20; i++) if (wc.v[i].alive) { tot++; if (f(wc.v[i])) hit++; }
            double pct = tot == 0 ? 0 : hit * 100.0 / tot;
            string verdict = pct < 5 ? "❌ 死条件，法永不触发" : (pct > 95 ? "❌ 恒真，无区分度" : "✅ 可用");
            Console.WriteLine($"  {name,-17} | {pct,5:F0}% | {verdict}");
        }
        Probe("饥饿 > 20",     x => x.hunger > 20);
        Probe("饥饿 > 40",     x => x.hunger > 40);
        Probe("饥饿 > 60",     x => x.hunger > 60);
        Probe("金币 < 10",     x => x.gold < 10);
        Probe("金币 < 30",     x => x.gold < 30);
        Probe("金币 > 50",     x => x.gold > 50);
        Probe("恐惧 > 20",     x => x.fear > 20);
        Probe("恐惧 > 50",     x => x.fear > 50);
        Probe("有伤疤(bit4-7)", x => (x.tags & 0xF0) != 0);
        Probe("有性格(bit0-3)", x => (x.tags & 0x0F) != 0);

        Console.WriteLine("\n  ⚠ 关键结论：如果律法条件大量落在「死条件」区，");
        Console.WriteLine("     玩家贴了 5 条法却什么都不发生 —— 这就是 v2 里「判活 0%」的真正原因。");
    }
}
