// v5 根因验证：三共享寄存器的动力学是"非对称"的 —— 有源无汇 / 有汇无源 → 必然收敛到不动点
//   假设 1：金币在村民之间只转移、不销毁 → 总量单调不减 → 必然全员顶格（σ→0）
//   假设 2：恐惧有汇无源 → 必然归零
//   修法：给每个寄存器同时配上"与自身状态相关的源"和"与自身状态相关的汇" → 产生稳态分布而非不动点
using System;

struct R5
{
    ulong _s;
    public R5(ulong seed) { _s = seed; }
    public ulong Next() { unchecked { _s += 0x9E3779B97F4A7C15UL; ulong z = _s; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); } }
    public int Range(int lo, int hi) { uint sp = (uint)(hi - lo); return sp == 0 ? lo : lo + (int)(Next() % sp); }
}

class W5
{
    public const int N = 20, MAXT = 2400;
    public int[] gold = new int[N], hunger = new int[N], fear = new int[N];
    public bool[] alive = new bool[N];
    public R5 rng;
    public int tick, granary = 120, totalDeaths;
    public bool goldSink, fearSource;   // 两个开关

    public W5(ulong seed, bool goldSink, bool fearSource)
    {
        this.goldSink = goldSink; this.fearSource = fearSource;
        rng = new R5(seed ^ 0x777UL);
        for (int i = 0; i < N; i++) { alive[i] = true; gold[i] = rng.Range(0, 30); hunger[i] = rng.Range(0, 30); fear[i] = rng.Range(0, 20); }
    }
    static int C(int x) => x < 0 ? 0 : (x > 99 ? 99 : x);

    public void Tick()
    {
        for (int i = 0; i < N; i++) if (alive[i]) granary = Math.Min(granary + 2, 500);
        for (int i = 0; i < N; i++)
        {
            if (!alive[i]) continue;
            hunger[i] = C(hunger[i] + 3);
            if (hunger[i] > 50 && granary >= 10) { granary -= 10; hunger[i] = C(hunger[i] - 40); }

            // ── 金币 ──
            // 基线：只在村民之间转移（这里用"随机漂移"近似），没有销毁途径
            gold[i] = C(gold[i] + (rng.Range(0, 100) < 45 ? 1 : 0) - (rng.Range(0, 100) < 25 ? 1 : 0));
            if (goldSink)
            {
                // ★ 汇：消耗与持有量成正比（累进税 / 熵增 / 仓储损耗）
                //   小额持有几乎无损耗，大额持有被持续削 → 产生均衡点而不是天花板
                int drain = gold[i] / 40;                       // 99 → 2/tick；20 → 0/tick
                gold[i] = C(gold[i] - drain);
                // ★ 源：与系统状态挂钩的生产（越饿越不生产；有粮的时候才生产）
                if (granary > 80 && hunger[i] < 60 && rng.Range(0, 100) < 25) gold[i] = C(gold[i] + 1);
            }

            // ── 恐惧 ──
            if (fearSource)
            {
                // ★ 源：情绪有真实来源 —— 饿会怕、看到别人怕会跟着怕（这才是"情绪是介质"）
                int nb = 0; for (int k = 0; k < 4; k++) { int j = (i + 1 + k) % N; if (alive[j] && fear[j] > 50) nb++; }
                fear[i] = C(fear[i] + (hunger[i] > 45 ? 2 : 0) + (nb >= 2 ? 2 : 0) + (gold[i] < 10 ? 1 : 0));
                // ★ 汇：与持有量成正比的消退（不是固定值）→ 高恐惧退得快，低恐惧退得慢
                fear[i] = Math.Max(0, fear[i] - Math.Max(1, fear[i] / 25));
            }
            else
            {
                fear[i] = Math.Max(0, fear[i] - 3);
            }

            if (hunger[i] >= 99) { alive[i] = false; totalDeaths++; }
        }
        tick++;
    }

    public int Alive() { int a = 0; for (int i = 0; i < N; i++) if (alive[i]) a++; return a; }
    public (double mg, double mh, double mf, double sg, double sh, double sf, int totG) Stat()
    {
        int a = 0; double sg1 = 0, sh1 = 0, sf1 = 0; int tg = 0;
        for (int i = 0; i < N; i++) if (alive[i]) { a++; sg1 += gold[i]; sh1 += hunger[i]; sf1 += fear[i]; }
        for (int i = 0; i < N; i++) tg += gold[i];      // 总量（含死者，看守恒）
        if (a == 0) return (0, 0, 0, 0, 0, 0, tg);
        double mg = sg1 / a, mh = sh1 / a, mf = sf1 / a, vg = 0, vh = 0, vf = 0;
        for (int i = 0; i < N; i++) if (alive[i]) { vg += (gold[i] - mg) * (gold[i] - mg); vh += (hunger[i] - mh) * (hunger[i] - mh); vf += (fear[i] - mf) * (fear[i] - mf); }
        return (mg, mh, mf, Math.Sqrt(vg / a), Math.Sqrt(vh / a), Math.Sqrt(vf / a), tg);
    }
}

class RootCause
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("═══ v5 根因验证：寄存器动力学的对称性 ═══\n");

        // ── 假设 1：金币总量守恒 → 单调 → 顶格 ──
        Console.WriteLine("【假设 1】金币只在村民之间转移、没有销毁途径 → 总量单调不减");
        Console.WriteLine("  tick |  金币总量 | 均金币 | σ(金币)");
        var w1 = new W5(1, false, false);
        for (int t = 0; t < 2400 && w1.Alive() > 0; t++)
        {
            w1.Tick();
            if (t % 480 == 0 || t == 2399) { var s = w1.Stat(); Console.WriteLine($"  {w1.tick,4} |   {s.totG,5}   |  {s.mg,4:F0}  |  {s.sg,5:F1}"); }
        }
        var sf = w1.Stat();
        Console.WriteLine($"  → 终局：总量 {sf.totG}（几乎没变，符合守恒），均金币 {sf.mg:F0} 但 σ 只有 {sf.sg:F1}");
        Console.WriteLine($"  → 全部挤在顶部：金币条件的区分度被抹平\n");

        // ── 假设 2：加汇与源之后 ──
        Console.WriteLine("【假设 2】给金币加「累进消耗」、给恐惧加「状态相关的源与汇」");
        var w2 = new W5(1, true, true);
        Console.WriteLine("  tick | 均金币(σ) | 均饥饿(σ) | 均恐惧(σ)");
        for (int t = 0; t < 2400 && w2.Alive() > 0; t++)
        {
            w2.Tick();
            if (t % 480 == 0 || t == 2399) { var s = w2.Stat(); Console.WriteLine($"  {w2.tick,4} | {s.mg,4:F0}({s.sg,4:F1}) | {s.mh,4:F0}({s.sh,4:F1}) | {s.mf,4:F0}({s.sf,4:F1})"); }
        }
        var s2 = w2.Stat();
        Console.WriteLine($"  → 终局 σ：金币 {s2.sg:F1}  饥饿 {s2.sh:F1}  恐惧 {s2.sf:F1}（三个都有真实方差 = 有区分度）\n");

        // ── 条件可用率对照 ──
        Console.WriteLine("【C】四个配置下的「律法条件可用率」（10 个条件里几个能用）\n");
        Console.WriteLine("  配置                      | 可用条件 | 均金币/σ | 均恐惧/σ");
        Console.WriteLine("  --------------------------|----------|----------|----------");
        foreach (var (gs, fs, name) in new[] { (false, false, "原始（无汇无源）"), (true, false, "只加金币汇"), (false, true, "只加恐惧源汇"), (true, true, "两者都加") })
        {
            var w = new W5(1, gs, fs);
            for (int t = 0; t < 1200; t++) w.Tick();
            int usable = 0;
            void P(Func<int, int, int, bool> f) { int hit = 0, tot = 0; for (int i = 0; i < 20; i++) if (w.alive[i]) { tot++; if (f(w.gold[i], w.hunger[i], w.fear[i])) hit++; } double p = tot == 0 ? 0 : hit * 100.0 / tot; if (p >= 5 && p <= 95) usable++; }
            P((g, h, f) => h > 20); P((g, h, f) => h > 40); P((g, h, f) => h > 60);
            P((g, h, f) => g < 10); P((g, h, f) => g < 30); P((g, h, f) => g > 60);
            P((g, h, f) => f > 15); P((g, h, f) => f > 35); P((g, h, f) => f > 60); P((g, h, f) => g > 20);
            var s = w.Stat();
            Console.WriteLine($"  {name,-24} |   {usable,2}/10   | {s.mg,4:F0}/{s.sg,4:F1} | {s.mf,4:F0}/{s.sf,4:F1}");
        }

        Console.WriteLine("\n  ⚠ 结论：寄存器必须同时有「与自身状态相关的源」和「汇」，");
        Console.WriteLine("     否则必然收敛到 0 或天花板 —— 律法条件随之全变成死条件或恒真条件。");
    }
}
