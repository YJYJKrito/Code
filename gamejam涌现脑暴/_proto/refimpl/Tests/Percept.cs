// ═══════════════════════════════════════════════════════════════════
// Tests/Percept.cs —— 可感知性实验：玩家多久能看到一次「法在动」？
//   回答四件事：
//   A 感知延迟：第一条法开火的 tick、第一个人死的 tick
//   B 结局分布：固定 seed，56 种律法组合的终局存活直方图
//   C 死法普查：8 条预设各自能不能开火
//   D 恐惧可达性：恐惧有没有可能越过各条恐惧类法的阈值
// ═══════════════════════════════════════════════════════════════════
using System;
using System.Text;
using System.Collections.Generic;

namespace SimLit
{
    public static class Percept
    {
        const int SEEDS = 40;

        static int FirstHitTick(ulong seed, int[] lawset)
        {
            var w = new World(seed, lawset);
            for (int t = 0; t < World.MAXT; t++)
            {
                w.Tick();
                int h = 0;
                for (int s = 0; s < World.SLOTS; s++) h += w.view[s].hitsThisTick;
                if (h > 0) return w.st.tick;
                if (w.AliveCount() == 0) return -1;
            }
            return -1;
        }

        static int FirstDeathTick(ulong seed, int[] lawset)
        {
            var w = new World(seed, lawset);
            for (int t = 0; t < World.MAXT; t++)
            {
                w.Tick();
                if (w.deaths > 0) return w.st.tick;
                if (w.AliveCount() == 0) return w.st.tick;
            }
            return -1;
        }

        static List<int[]> Combos()
        {
            var list = new List<int[]>();
            for (int a = 0; a < 8; a++)
            for (int b = a + 1; b < 8; b++)
            for (int c = b + 1; c < 8; c++)
            for (int d = c + 1; d < 8; d++)
            for (int e = d + 1; e < 8; e++)
                list.Add(new[] { a, b, c, d, e });
            return list;
        }

        static string Stat(int[] xs)
        {
            Array.Sort(xs);
            int n = xs.Length;
            int med = n % 2 == 1 ? xs[n / 2] : (xs[n / 2 - 1] + xs[n / 2]) / 2;
            int zero = 0, never = 0;
            foreach (var x in xs) { if (x == 0) zero++; if (x < 0) never++; }
            return $"min {xs[0]}  中位 {med}  max {xs[n - 1]}  0 或负 {zero}  从不开火 {-never - (-zero) * 0}";
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════ 可感知性实验 ══════");
            var combos = Combos();

            // ── A 感知延迟 ──
            var fh = new int[SEEDS]; var fd = new int[SEEDS];
            for (int s = 0; s < SEEDS; s++)
            {
                var L = combos[s % combos.Count];
                fh[s] = FirstHitTick((ulong)s, L);
                fd[s] = FirstDeathTick((ulong)s, L);
            }
            sb.AppendLine("\n【A】第一条法开火的 tick（40 seed × 各自组合）");
            sb.AppendLine("   " + Stat(fh));
            sb.AppendLine("   第一次死亡的 tick");
            sb.AppendLine("   " + Stat(fd));
            int hitIn60 = 0, deathIn60 = 0, hitIn180 = 0;
            for (int s = 0; s < SEEDS; s++) { if (fh[s] > 0 && fh[s] <= 60) hitIn60++; if (fd[s] > 0 && fd[s] <= 60) deathIn60++; if (fh[s] > 0 && fh[s] <= 180) hitIn180++; }
            sb.AppendLine($"   60 秒(=60 tick@1x / 180 tick@3x)内必有开火：{hitIn60}/{SEEDS}（≤60t）");
            sb.AppendLine($"   180 tick 内必有开火：{hitIn180}/{SEEDS}");
            sb.AppendLine($"   60 tick 内已有死亡：{deathIn60}/{SEEDS}");

            // ── B 结局分布 ──
            sb.AppendLine("\n【B】固定 seed=0，56 种组合的终局存活直方图");
            var hist = new int[21];
            for (int i = 0; i < combos.Count; i++)
            {
                var w = new World(0, combos[i]);
                for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
                hist[w.AliveCount()]++;
            }
            int allAlive = hist[20], zeroAlive = hist[0];
            for (int a = 20; a >= 0; a--) if (hist[a] > 0) sb.AppendLine($"   存活 {a,2}/20 : {new string('#', hist[a])} {hist[a]}");
            sb.AppendLine($"   → 全员幸存（无人死）的组合 {allAlive}/56，全灭 {zeroAlive}/56");

            // ── C 死法普查：每条预设单独放满 5 槽时能不能开火 ──
            sb.AppendLine("\n【C】8 条预设各自的「单独运行」开火量（5 槽全放同一条法，seed 0..9）");
            string[] names = { "福利", "税收", "饥则抢", "济贫", "耕作", "恐惧蔓延", "安抚", "私刑倾向" };
            for (int pi = 0; pi < 8; pi++)
            {
                long hits = 0; int fired = 0; int aliveSum = 0;
                for (ulong s = 0; s < 10; s++)
                {
                    var w = new World(s, new[] { pi, pi, pi, pi, pi });
                    for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
                    int h = 0; for (int k = 0; k < World.SLOTS; k++) h += w.lawHits[k];
                    hits += h; if (h > 0) fired++; aliveSum += w.AliveCount();
                }
                sb.AppendLine($"   #{pi} {names[pi],-6} 平均命中 {hits / 10.0,9:F0}  有开火的 seed {fired}/10  终局存活 {aliveSum / 10.0:F1}/20");
            }

            // ── D 恐惧可达性 ──
            sb.AppendLine("\n【D】恐惧能否越过各条恐惧类法的阈值（base 组合 {0,1,2,3,4}，seed 0..19）");
            int maxFearSeen = 0; long ticksOver25 = 0, ticksOver35 = 0, ticksOver50 = 0, vticks = 0;
            for (ulong s = 0; s < 20; s++)
            {
                var w = new World(s, new[] { 0, 1, 2, 3, 4 });
                for (int t = 0; t < World.MAXT; t++)
                {
                    w.Tick();
                    for (int i = 0; i < World.N; i++)
                    {
                        if (!w.v[i].alive) continue;
                        vticks++;
                        int f = w.v[i].fear;
                        if (f > maxFearSeen) maxFearSeen = f;
                        if (f > 25) ticksOver25++;
                        if (f > 35) ticksOver35++;
                        if (f > 50) ticksOver50++;
                    }
                    if (w.AliveCount() == 0) break;
                }
            }
            sb.AppendLine($"   全程最高恐惧 {maxFearSeen}");
            sb.AppendLine($"   村民-tick 中 fear>25 占比 {ticksOver25 * 100.0 / vticks:F2}%   >35 占比 {ticksOver35 * 100.0 / vticks:F2}%   >50 占比 {ticksOver50 * 100.0 / vticks:F3}%");
            sb.AppendLine("   → 「安抚」法条件 fear>50：能开火的概率由上一行决定");

            return sb.ToString();
        }
    }
}
