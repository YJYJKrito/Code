// v4 修法验证：给寄存器注入"永不停息的外生扰动 + 自然消耗"，让它不落进不动点
//   对照 A：基线物理（v3 的问题版本）—— 恐惧归零、金币顶天花板、饥饿夹在不动点
//   对照 B：加扰动 + 负反馈 —— 看寄存器是否恢复"活"的分布
using System;

struct R4
{
    ulong _s;
    public R4(ulong seed) { _s = seed; }
    public ulong Next() { unchecked { _s += 0x9E3779B97F4A7C15UL; ulong z = _s; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); } }
    public int Range(int lo, int hi) { uint sp = (uint)(hi - lo); return sp == 0 ? lo : lo + (int)(Next() % sp); }
    public bool Chance(int pct) => Range(0, 100) < pct;
}

class Reg4
{
    public int gold, hunger, fear;
    public ushort tags;
    public bool alive;
    public int nRobbed, nFed, nTaxed;
}

class W4
{
    public const int N = 20, MAXT = 2400;
    public Reg4[] v = new Reg4[N];
    public R4 rng;
    public int tick, granary = 120, totalDeaths;
    public bool fixedMode;         // ★ 开关：false = v3 的病灶版本；true = 注入扰动版
    public int shocks;

    public W4(ulong seed, bool fixedMode)
    {
        this.fixedMode = fixedMode;
        rng = new R4(seed ^ 0x5EED1234UL);
        for (int i = 0; i < N; i++)
            v[i] = new Reg4 { alive = true, gold = rng.Range(0, 30), hunger = rng.Range(0, 30), fear = rng.Range(0, 20), tags = (ushort)(1 << rng.Range(0, 4)) };
    }
    static int C(int x) => x < 0 ? 0 : (x > 99 ? 99 : x);

    public void Tick()
    {
        // ── 生产 ──
        for (int i = 0; i < N; i++) if (v[i].alive) granary = Math.Min(granary + 2, 500);

        // ── 外生扰动（这就是"永不停息的变化源"）──
        if (fixedMode)
        {
            if (tick > 0 && tick % 200 == 0)   // 灾荒：全体饥饿暴涨 + 恐慌
            {
                shocks++;
                for (int i = 0; i < N; i++) if (v[i].alive) { v[i].hunger = C(v[i].hunger + 25); v[i].fear = C(v[i].fear + 20); }
            }
            if (tick > 0 && tick % 300 == 0)   // 丰收
            {
                shocks++;
                granary = Math.Min(granary + 150, 500);
            }
            if (tick > 0 && tick % 137 == 0)   // 随机小事件（质数周期避免与上面共振）
            {
                shocks++;
                int i = rng.Range(0, N);
                if (v[i].alive) { v[i].gold = C(v[i].gold + rng.Range(-15, 20)); v[i].fear = C(v[i].fear + rng.Range(0, 15)); }
            }
        }

        // ── 物理 ──
        for (int i = 0; i < N; i++)
        {
            if (!v[i].alive) continue;
            v[i].hunger = C(v[i].hunger + 3);
            if (v[i].hunger > 50 && granary >= 10) { granary -= 10; v[i].hunger = C(v[i].hunger - 40); }

            if (fixedMode)
            {
                // 恐惧有源：饿会怕，怕会累积；同时缓慢消退（形成动态平衡而非归零）
                v[i].fear = C(v[i].fear + (v[i].hunger > 45 ? 2 : 0) + (v[i].nRobbed > 0 ? 1 : 0));
                v[i].fear = Math.Max(0, v[i].fear - 1);
                // 金币有自然消耗（税、损耗），不再单调顶天花板
                v[i].gold = C(v[i].gold - (rng.Chance(35) ? 1 : 0) + (rng.Chance(45) ? 1 : 0));
            }
            else
            {
                v[i].fear = Math.Max(0, v[i].fear - 3);                       // 病灶：只减不增
                v[i].gold = C(v[i].gold + (rng.Chance(30) ? 1 : 0));           // 病灶：只增不减
            }

            if (v[i].hunger >= 99) { v[i].alive = false; totalDeaths++; }
        }
        tick++;
    }

    public int Alive() { int a = 0; for (int i = 0; i < N; i++) if (v[i].alive) a++; return a; }
    public (int mg, int mh, int mf, int sg, int sh, int sf) Stats()
    {
        int a = 0, sg = 0, sh = 0, sf = 0;
        for (int i = 0; i < N; i++) if (v[i].alive) { a++; sg += v[i].gold; sh += v[i].hunger; sf += v[i].fear; }
        if (a == 0) return (0, 0, 0, 0, 0, 0);
        int mg = sg / a, mh = sh / a, mf = sf / a;
        long vg = 0, vh = 0, vf = 0;
        for (int i = 0; i < N; i++) if (v[i].alive) { vg += (long)(v[i].gold - mg) * (v[i].gold - mg); vh += (long)(v[i].hunger - mh) * (v[i].hunger - mh); vf += (long)(v[i].fear - mf) * (v[i].fear - mf); }
        return (mg, mh, mf, (int)Math.Sqrt(vg / a), (int)Math.Sqrt(vh / a), (int)Math.Sqrt(vf / a));
    }
}

class Fix
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("═══ v4 修法验证：让寄存器不落进不动点 ═══\n");

        foreach (bool fixedMode in new[] { false, true })
        {
            Console.WriteLine(fixedMode ? "【B】注入外生扰动 + 自然消耗（修正版）" : "【A】基线物理（v3 的病灶版本）");
            var w = new W4(1, fixedMode);
            Console.WriteLine("  tick | 均金币(σ) | 均饥饿(σ) | 均恐惧(σ) | 存活");
            for (int t = 0; t < 2400 && w.Alive() > 0; t++)
            {
                w.Tick();
                if (t % 480 == 0 || t == 2399)
                {
                    var (mg, mh, mf, sg, sh, sf) = w.Stats();
                    Console.WriteLine($"  {w.tick,4} | {mg,3}({sg,2})   | {mh,3}({sh,2})   | {mf,3}({sf,2})   | {w.Alive(),2}/20");
                }
            }
            var (g, h, f, sg2, sh2, sf2) = w.Stats();
            Console.WriteLine($"  终值：金币 {g}(σ{sg2})  饥饿 {h}(σ{sh2})  恐惧 {f}(σ{sf2})  存活 {w.Alive()}\n");
        }

        // ───── 修正版下的"条件可用率" ─────
        Console.WriteLine("【C】修正版下，律法条件重新变得可用了吗");
        Console.WriteLine("     （上次：只有「饥饿>20/40」可用，其余 8 个条件全是死条件或恒真）\n");
        var wc = new W4(1, true);
        for (int t = 0; t < 1000; t++) wc.Tick();
        Console.WriteLine("  条件              | 上轮 | 本轮 | 评价");
        Console.WriteLine("  ------------------|------|------|------");
        int usable = 0, total = 0;
        void Probe(string name, string last, Func<Reg4, bool> fn)
        {
            int hit = 0, tot = 0;
            for (int i = 0; i < 20; i++) if (wc.v[i].alive) { tot++; if (fn(wc.v[i])) hit++; }
            double pct = tot == 0 ? 0 : hit * 100.0 / tot;
            string verdict = pct < 5 ? "❌ 死条件" : (pct > 95 ? "❌ 恒真" : "✅ 可用");
            if (verdict == "✅ 可用") usable++; total++;
            Console.WriteLine($"  {name,-17} | {last,4} | {pct,4:F0}% | {verdict}");
        }
        Probe("饥饿 > 20", "40%", x => x.hunger > 20);
        Probe("饥饿 > 40", "20%", x => x.hunger > 40);
        Probe("饥饿 > 60", "0%", x => x.hunger > 60);
        Probe("金币 < 10", "0%", x => x.gold < 10);
        Probe("金币 < 30", "0%", x => x.gold < 30);
        Probe("金币 > 50", "100%", x => x.gold > 50);
        Probe("恐惧 > 20", "0%", x => x.fear > 20);
        Probe("恐惧 > 50", "0%", x => x.fear > 50);
        Probe("有伤疤(bit4-7)", "0%", x => (x.tags & 0xF0) != 0);
        Probe("有性格(bit0-3)", "100%", x => (x.tags & 0x0F) != 0);
        Console.WriteLine($"\n  可用条件：上轮 2/10 → 本轮 {usable}/{total}");
    }
}
