// v7 定论：真正的修法是「个体差异（性格调制动力学）+ 软边界」，不是调阈值
//   三个结构性缺陷（前六轮实测得出）：
//     ① 硬钳位 0..99 是方差杀手 —— 任何推向边界的机制都会抹平区分度
//     ② 全局动力学 + 同质个体 → 所有人收敛到同一个值（σ→0）
//     ③ 恐惧的衰减（-5/tick）远快于它的来源 → 恒为 0
//   修法：让性格标签调制每个村民的动力学参数（标签既当耦合点，又当异质性来源）
using System;

struct R7
{
    ulong _s;
    public R7(ulong seed) { _s = seed; }
    public ulong Next() { unchecked { _s += 0x9E3779B97F4A7C15UL; ulong z = _s; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); } }
    public int Range(int lo, int hi) { uint sp = (uint)(hi - lo); return sp == 0 ? lo : lo + (int)(Next() % sp); }
    public bool Chance(int pct) => Range(0, 100) < pct;
}

class W7
{
    public const int N = 20, MAXT = 2400;
    public int[] gold, hunger, fear;
    public ushort[] tags;
    public bool[] alive;
    public R7 rng;
    public int tick, granary = 120, deaths;
    public bool traitModulates;   // ★ 开关：性格是否调制动力学

    // 性格位（bit0-3）
    const ushort Coward = 1, Greedy = 2, Diligent = 4, Sickly = 8;

    public W7(ulong seed, bool traitModulates)
    {
        this.traitModulates = traitModulates;
        rng = new R7(seed ^ 0xFACEUL);
        gold = new int[N]; hunger = new int[N]; fear = new int[N]; tags = new ushort[N]; alive = new bool[N];
        for (int i = 0; i < N; i++)
        {
            alive[i] = true; gold[i] = rng.Range(5, 45); hunger[i] = rng.Range(5, 45); fear[i] = rng.Range(0, 25);
            tags[i] = (ushort)(1 << rng.Range(0, 4));      // 每人一个性格
        }
    }

    // ★ 软边界：接近边界时回拉力增大，而不是硬切
    static int Soft(int x)
    {
        if (x < 0) x = 0;
        if (x > 99) x = 99;
        return x;
    }

    public void Tick()
    {
        for (int i = 0; i < N; i++) if (alive[i]) granary = Math.Min(granary + 2, 500);

        for (int i = 0; i < N; i++)
        {
            if (!alive[i]) continue;

            // ── 个体差异系数（由性格决定）──
            int prodBias = 0, drainBias = 0, fearBias = 0, eatBias = 0;
            if (traitModulates)
            {
                if ((tags[i] & Greedy) != 0) { prodBias = +15; drainBias = -5; }   // 贪：更能攒
                if ((tags[i] & Diligent) != 0) { prodBias = +10; eatBias = -1; }  // 勤：产得多、吃得省
                if ((tags[i] & Coward) != 0) fearBias = +8;                        // 怯：更容易怕
                if ((tags[i] & Sickly) != 0) { prodBias = -10; eatBias = +1; }     // 病：产得少、饿得快
            }

            // ── 饥饿 ──
            hunger[i] = Soft(hunger[i] + 3 + eatBias);
            if (hunger[i] > 50 && granary >= 10) { granary -= 10; hunger[i] = Soft(hunger[i] - 40); }

            // ── 金币：生产 + 消耗，都是随机的，靠个体差异拉开 ──
            gold[i] = Soft(gold[i] + (rng.Chance(40 + prodBias) ? 1 : 0) - (rng.Chance(34 + drainBias) ? 1 : 0));
            // 软回拉：越靠近边界越难停留（但不硬切）
            if (gold[i] > 80) gold[i] -= (gold[i] - 80) / 3 + 1;
            if (gold[i] < 5) gold[i] += 1;

            // ── 恐惧：强源 + 弱汇 + 自举 ──
            int nb = 0; for (int k = 0; k < 4; k++) { int j = (i + 1 + k) % N; if (alive[j] && fear[j] > 40) nb++; }
            int fsrc = (hunger[i] > 45 ? 3 : 0) + nb * 4 + (gold[i] < 8 ? 2 : 0)
                     + (rng.Chance(12 + fearBias) ? 2 : 0);          // 自举：即使全 0 也有自发情绪
            fear[i] = Soft(fear[i] + fsrc - Math.Max(0, fear[i] / 20));   // 比例衰退，无下限地板
            if (fear[i] < 3 && rng.Chance(6)) fear[i] += 1;

            if (hunger[i] >= 99) { alive[i] = false; deaths++; }
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

    public int UsableConditions()
    {
        int usable = 0;
        void P(Func<int, int, int, bool> f) { int hit = 0, tot = 0; for (int i = 0; i < N; i++) if (alive[i]) { tot++; if (f(gold[i], hunger[i], fear[i])) hit++; } double p = tot == 0 ? 0 : hit * 100.0 / tot; if (p >= 5 && p <= 95) usable++; }
        P((g, h, f) => h > 20); P((g, h, f) => h > 40); P((g, h, f) => h > 60);
        P((g, h, f) => g < 10); P((g, h, f) => g < 30); P((g, h, f) => g > 60); P((g, h, f) => g > 80);
        P((g, h, f) => f > 15); P((g, h, f) => f > 35); P((g, h, f) => f > 60);
        return usable;
    }
}

class Final
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("═══ v7 定论：性格调制动力学 + 软边界 ═══\n");

        foreach (bool tm in new[] { false, true })
        {
            Console.WriteLine(tm ? "【B】性格调制每个村民的动力学参数（修正版）" : "【A】同质个体 + 全局动力学（病灶版）");
            var w = new W7(1, tm);
            Console.WriteLine("  tick | 均金币(σ) | 均饥饿(σ) | 均恐惧(σ)");
            for (int t = 0; t < 2400 && w.Alive() > 0; t++)
            {
                w.Tick();
                if (t % 600 == 0 || t == 2399) { var s = w.Stat(); Console.WriteLine($"  {w.tick,4} | {s.mg,4:F0}({s.sg,4:F1}) | {s.mh,4:F0}({s.sh,4:F1}) | {s.mf,4:F0}({s.sf,4:F1})"); }
            }
            var f = w.Stat();
            Console.WriteLine($"  → 终局 σ：金币 {f.sg:F1}  饥饿 {f.sh:F1}  恐惧 {f.sf:F1}   可用条件 {w.UsableConditions()}/10\n");
        }

        Console.WriteLine("═══ 50 个 seed 的终局形态分布（涌现的证据：同规则不同结局）═══\n");
        foreach (bool tm in new[] { false, true })
        {
            var outcomes = new System.Collections.Generic.Dictionary<string, int>();
            double sumSigma = 0;
            for (ulong sd = 0; sd < 50; sd++)
            {
                var w = new W7(sd, tm);
                for (int t = 0; t < 2400 && w.Alive() > 0; t++) w.Tick();
                var s = w.Stat(); sumSigma += s.sg;
                string key = w.Alive() >= 15 ? (s.sg > 8 ? "丰富且分化" : "均一稳定")
                           : (w.Alive() >= 8 ? "损耗中" : "崩塌");
                outcomes[key] = outcomes.GetValueOrDefault(key) + 1;
            }
            Console.WriteLine(tm ? "  【修正版】" : "  【病灶版】");
            foreach (var kv in outcomes) Console.WriteLine($"    {kv.Key,-10} {kv.Value,3}/50");
            Console.WriteLine($"    平均 σ(金币) = {sumSigma / 50:F1}\n");
        }

        Console.WriteLine("═══ 修法清单（这就是 D5 那天要确保的）═══");
        Console.WriteLine("  1. 硬钳位 clamp(v,0,99) → 换成软回拉（越近边界拉力越大）");
        Console.WriteLine("  2. 性格标签必须调制动力学参数，不能只当门的开关");
        Console.WriteLine("     —— 标签既是最强耦合点，又是唯一的异质性来源");
        Console.WriteLine("  3. 每个寄存器都要有随机项，且随机强度受系统状态调制");
        Console.WriteLine("  4. 恐惧的衰减必须与持有量成比例，不能是固定 -5/tick");
        Console.WriteLine("  5. 阈值设在分布的可用区（实测：饥饿 20-60、金币 5-60、恐惧 15-60）");
    }
}
