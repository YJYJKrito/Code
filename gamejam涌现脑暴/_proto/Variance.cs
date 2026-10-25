// v6 决定性实验：涌现的真正必要条件不是"有源有汇"，而是"正反馈略强于负反馈"
//   假设：纯负反馈（均值回归）会把所有人拉平 → σ→0 → 条件全是死条件/恒真
//         正反馈略强于负反馈 → 分化维持 → 分布在临界点附近自组织出长尾
//         正反馈过强 → 一个人吃掉全村 → σ 大但退化
//   扫的就是"正反馈 / 负反馈"这个比值
using System;

struct R6
{
    ulong _s;
    public R6(ulong seed) { _s = seed; }
    public ulong Next() { unchecked { _s += 0x9E3779B97F4A7C15UL; ulong z = _s; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); } }
    public int Range(int lo, int hi) { uint sp = (uint)(hi - lo); return sp == 0 ? lo : lo + (int)(Next() % sp); }
    public bool Chance(int pct) => Range(0, 100) < pct;
}

class W6
{
    public const int N = 20, MAXT = 2400;
    public int[] gold = new int[N], hunger = new int[N], fear = new int[N];
    public bool[] alive = new bool[N];
    public R6 rng;
    public int tick, granary = 120;
    public int posMul, negDiv;     // 正反馈乘数 / 负反馈除数

    public W6(ulong seed, int posMul, int negDiv)
    {
        this.posMul = posMul; this.negDiv = negDiv;
        rng = new R6(seed ^ 0xBEEFUL);
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

            // ── 金币：正反馈（马太：有钱更能赚） vs 负反馈（累进税：有钱扣更多）──
            if (posMul > 0)
            {
                // 正反馈：生产收益与自己当前财富成正比（越有钱越能赚）
                gold[i] = C(gold[i] + gold[i] / posMul + (rng.Chance(30) ? 1 : 0));
            }
            if (negDiv > 0)
            {
                // 负反馈：累进税 / 损耗，同样与财富成正比
                gold[i] = C(gold[i] - gold[i] / negDiv);
            }

            // ── 恐惧：自举 + 传染 ──
            // 自举：哪怕 0 也有一点点自发情绪（否则永远起不来 —— 这是 v5 失败的根因）
            int nb = 0; for (int k = 0; k < 4; k++) { int j = (i + 1 + k) % N; if (alive[j] && fear[j] > 40) nb++; }
            int fsrc = (hunger[i] > 45 ? 2 : 0) + nb * 3 + (gold[i] < 8 ? 2 : 0) + (rng.Chance(8) ? 1 : 0);
            fear[i] = C(fear[i] + fsrc - Math.Max(1, fear[i] / 12));

            if (hunger[i] >= 99) alive[i] = false;
        }
        tick++;
    }

    public int Alive() { int a = 0; for (int i = 0; i < N; i++) if (alive[i]) a++; return a; }
    public (double mg, double mh, double mf, double sg, double sh, double sf) Stat()
    {
        int a = 0; double g = 0, h = 0, f = 0;
        for (int i = 0; i < N; i++) if (alive[i]) { a++; g += gold[i]; h += hunger[i]; f += fear[i]; }
        if (a == 0) return (0, 0, 0, 0, 0, 0);
        g /= a; h /= a; f /= a;
        double vg = 0, vh = 0, vf = 0;
        for (int i = 0; i < N; i++) if (alive[i]) { vg += (gold[i] - g) * (gold[i] - g); vh += (hunger[i] - h) * (hunger[i] - h); vf += (fear[i] - f) * (fear[i] - f); }
        return (g, h, f, Math.Sqrt(vg / a), Math.Sqrt(vh / a), Math.Sqrt(vf / a));
    }
}

class Variance
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("═══ v6 决定性实验：分化从哪来 ═══\n");
        Console.WriteLine("正反馈乘数越小 = 财富增殖越快；负反馈除数越小 = 累进税越重");
        Console.WriteLine("比值 posMul/negDiv > 1 → 分化；< 1 → 趋于均一\n");

        Console.WriteLine("posMul | negDiv | 比值 | 均金币 | σ(金币) | 存活 | 可用条件数");
        Console.WriteLine("-------|--------|------|--------|---------|------|-----------");
        foreach (var (pm, nd) in new[] { (0, 30), (30, 30), (25, 35), (30, 40), (40, 60), (35, 50), (50, 60), (60, 60), (20, 60) })
        {
            var w = new W6(1, pm, nd);
            for (int t = 0; t < W6.MAXT && w.Alive() > 0; t++) w.Tick();
            var s = w.Stat();
            int usable = 0;
            void P(Func<int, int, int, bool> f) { int hit = 0, tot = 0; for (int i = 0; i < 20; i++) if (w.alive[i]) { tot++; if (f(w.gold[i], w.hunger[i], w.fear[i])) hit++; } double p = tot == 0 ? 0 : hit * 100.0 / tot; if (p >= 5 && p <= 95) usable++; }
            P((g, h, f) => h > 20); P((g, h, f) => h > 40); P((g, h, f) => h > 60);
            P((g, h, f) => g < 10); P((g, h, f) => g < 30); P((g, h, f) => g > 60); P((g, h, f) => g > 80);
            P((g, h, f) => f > 15); P((g, h, f) => f > 35); P((g, h, f) => f > 60);
            string ratio = nd == 0 ? "∞" : ((double)pm / nd).ToString("F2");
            Console.WriteLine($"  {pm,3}  |  {nd,3}   | {ratio,4} | {s.mg,5:F0}  |  {s.sg,5:F1}  | {w.Alive(),3}/20 |    {usable,2}/10");
        }

        Console.WriteLine("\n═══ 最优配置下的三寄存器时间演化 ═══");
        var best = new W6(1, 30, 40);   // 比值 0.75
        Console.WriteLine("  tick | 均金币(σ) | 均饥饿(σ) | 均恐惧(σ)");
        for (int t = 0; t < 2400 && best.Alive() > 0; t++)
        {
            best.Tick();
            if (t % 400 == 0 || t == 2399) { var s = best.Stat(); Console.WriteLine($"  {best.tick,4} | {s.mg,4:F0}({s.sg,4:F1}) | {s.mh,4:F0}({s.sh,4:F1}) | {s.mf,4:F0}({s.sf,4:F1})"); }
        }

        Console.WriteLine("\n═══ 同一配置下 50 个 seed 的终局分布（看是否存在多种结局）═══");
        var outcomes = new System.Collections.Generic.Dictionary<string, int>();
        for (ulong sd = 0; sd < 50; sd++)
        {
            var w = new W6(sd, 30, 40);
            for (int t = 0; t < 2400 && w.Alive() > 0; t++) w.Tick();
            var s = w.Stat();
            string key = w.Alive() >= 15 ? (s.sg > 12 ? "丰富但分化" : "均一稳定") : (w.Alive() >= 8 ? "损耗" : "崩塌");
            outcomes[key] = outcomes.GetValueOrDefault(key) + 1;
        }
        foreach (var kv in outcomes) Console.WriteLine($"  {kv.Key,-12} {kv.Value,3}/50");
    }
}
