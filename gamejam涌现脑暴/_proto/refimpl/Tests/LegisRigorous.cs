// ═══════════════════════════════════════════════════════════════════
// Tests/LegisRigorous.cs —— 严格实验：立法有没有有效影响村庄进程
//
// 三级判据 + 方差分解 + 置换检验（不依赖正态假设）+ 恐惧诊断。
//
//   级别 A（开火）  ：每条法的触发率、覆盖密度 → 死条件 / 恒真条件
//   级别 B（过程）  ：同 seed 换法，村民状态首次分岔的 tick、分岔率
//   级别 C（结局）  ：终局「类别」与律法的互信息 NMI + 置换零分布
//   方差分解        ：lawset / seed / 残差 的 η² 与 F（交叉设计）
//
// 说明：模拟本身仍是纯整数、零 GC；本文件只是离线分析壳，允许用 double。
// ═══════════════════════════════════════════════════════════════════
using System;
using System.Text;

namespace SimLit
{
    public static class LegisRigorous
    {
        const int SLOTS = World.SLOTS;
        const int P = World.PRESETS_COUNT;

        static int SEEDS = 40;
        const int PERM = 200;                 // 置换次数

        // ── 只吃「村民状态」的签名，排除律法自身字段与 tick ──
        //   这样「首次分岔」测的是村庄的故事有没有变，而不是律法表变了没。
        static ulong VSig(World w)
        {
            unchecked
            {
                ulong h = 0x9E3779B97F4A7C15UL;
                for (int i = 0; i < World.N; i++)
                {
                    h = Rng.Mix64(h ^ (ulong)(uint)w.v[i].gold);
                    h = Rng.Mix64(h ^ (ulong)(uint)w.v[i].hunger);
                    h = Rng.Mix64(h ^ (ulong)(uint)w.v[i].fear);
                    h = Rng.Mix64(h ^ w.v[i].tags);
                    h = Rng.Mix64(h ^ (w.v[i].alive ? 1UL : 0UL));
                    h = Rng.Mix64(h ^ (ulong)(uint)w.v[i].nRobbed);
                }
                h = Rng.Mix64(h ^ (ulong)(uint)w.st.granary);
                h = Rng.Mix64(h ^ (ulong)(uint)w.st.aliveCount);
                return h;
            }
        }

        struct Outcome
        {
            public int alive, deaths, classId;
            public double gold, hunger, fear, sg, sh, sf;
            public int aliveTick;      // 最后一个村民死亡 / MAXT
        }

        static Outcome Run(ulong seed, int[] lawset, bool measureShock = false)
        {
            var w = new World(seed, lawset);
            int last = 0;
            for (int t = 0; t < World.MAXT; t++)
            {
                w.Tick();
                if (w.AliveCount() == 0) { last = t; break; }
                last = t;
            }
            var st = w.Stat();
            return new Outcome
            {
                alive = w.AliveCount(),
                deaths = w.deaths,
                gold = st.mg, hunger = st.mh, fear = st.mf,
                sg = st.sg, sh = st.sh, sf = st.sf,
                aliveTick = last,
                classId = 0
            };
        }

        static System.Collections.Generic.List<int[]> Combos()
        {
            var list = new System.Collections.Generic.List<int[]>();
            for (int a = 0; a < P; a++) for (int b = a + 1; b < P; b++)
                for (int c = b + 1; c < P; c++) for (int d = c + 1; d < P; d++)
                    for (int e = d + 1; e < P; e++) list.Add(new[] { a, b, c, d, e });
            return list;
        }

        static string Name(int pi) => new[] { "福利", "税收", "饥则抢", "济贫", "耕作", "恐惧蔓延", "安抚", "私刑" }[pi];

        // 终局类别：存活桶 × 死亡桶 × 主导行为标签
        static int Classify(World w)
        {
            int a = w.AliveCount();
            int ab = a == 0 ? 0 : a <= 5 ? 1 : a <= 10 ? 2 : a <= 15 ? 3 : 4;
            int db = w.deaths == 0 ? 0 : w.deaths <= 3 ? 1 : w.deaths <= 7 ? 2 : 3;
            int beh = 0;
            for (int i = 0; i < World.N; i++)
                beh |= (w.v[i].tags & TagBits.BEHAV_MASK) >> 8;
            int dom = System.Numerics.BitOperations.PopCount((uint)(beh & 0x1F));
            return ab * 40 + db * 8 + dom;   // ≤ 200 个可能类别
        }

        // ── 主入口 ──
        public static string Run(string[] args)
        {
            if (args != null && args.Length > 1) int.TryParse(args[1], out SEEDS);
            if (SEEDS < 5) SEEDS = 40;

            var sb = new StringBuilder();
            var combos = Combos();
            sb.AppendLine("════════ 严格实验：立法有效性 ════════");
            sb.AppendLine($"设计：{combos.Count} 种律法组合 × {SEEDS} 个 seed（PRNG 无状态 → 同 seed 随机流一致，唯一变量是律法）\n");

            // 全网格数据
            int K = combos.Count;
            var grid = new Outcome[K * SEEDS];
            var wRun = new World[K * SEEDS];
            for (int l = 0; l < K; l++)
                for (int s = 0; s < SEEDS; s++)
                {
                    var w = new World((ulong)s, combos[l]);
                    for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
                    var st = w.Stat();
                    grid[l * SEEDS + s] = new Outcome {
                        alive = w.AliveCount(), deaths = w.deaths,
                        gold = st.mg, hunger = st.mh, fear = st.mf,
                        sg = st.sg, sh = st.sh, sf = st.sf,
                        aliveTick = w.st.tick, classId = Classify(w)
                    };
                }

            // ═══ 级别 A：开火 ═══
            sb.AppendLine("【级别 A：律法开火率】每条法在多少比例的 tick 里至少命中一人，及覆盖密度");
            sb.AppendLine("  法          | tick触发率 | 命中密度(次/人/tick) | 判定");
            var fireRate = new double[P]; var dens = new double[P]; var judgeA = new string[P];
            for (int pi = 0; pi < P; pi++)
            {
                long fireTicks = 0, totalTicks = 0, hits = 0, personTicks = 0;
                for (int l = 0; l < K; l++)
                {
                    if (Array.IndexOf(combos[l], pi) < 0) continue;
                    int slot = Array.IndexOf(combos[l], pi);
                    for (int s = 0; s < SEEDS; s++)
                    {
                        var w = new World((ulong)s, combos[l]);
                        for (int t = 0; t < World.MAXT; t++)
                        {
                            w.Tick(); totalTicks++;
                            personTicks += w.AliveCount();
                            if (w.view[slot].hitsThisTick > 0) { fireTicks++; hits += w.view[slot].hitsThisTick; }
                            if (w.AliveCount() == 0) break;
                        }
                    }
                }
                fireRate[pi] = totalTicks == 0 ? 0 : fireTicks * 1.0 / totalTicks;
                dens[pi] = personTicks == 0 ? 0 : hits * 1.0 / personTicks;
                judgeA[pi] = fireRate[pi] < 0.05 ? "死条件" : fireRate[pi] > 0.95 ? "恒真/泛化" : "可用";
                sb.AppendLine($"  #{pi} {Name(pi),-5} |   {fireRate[pi] * 100,6:F1}%  |       {dens[pi],7:F3}       | {judgeA[pi]}");
            }
            int usableA = 0; for (int pi = 0; pi < P; pi++) if (judgeA[pi] == "可用") usableA++;
            sb.AppendLine($"  → 可用条件 {usableA}/{P}\n");

            // ═══ 级别 B：过程分岔 ═══
            sb.AppendLine("【级别 B：过程分岔】同 seed，基准法 {0,1,2,3,4} vs 把某一条换成另一条，村民状态首次分岔的 tick");
            var baseSet = new int[] { 0, 1, 2, 3, 4 };
            int divCount = 0, divTotal = 0; var firstDiv = new System.Collections.Generic.List<int>();
            var inert = new System.Collections.Generic.List<string>();
            for (int swapSlot = 0; swapSlot < SLOTS; swapSlot++)
                for (int pi = 0; pi < P; pi++)
                {
                    if (Array.IndexOf(baseSet, pi) >= 0) continue;
                    var alt = (int[])baseSet.Clone(); alt[swapSlot] = pi;
                    int thisDiv = 0;
                    for (int s = 0; s < SEEDS; s++)
                    {
                        var A = new World((ulong)s, baseSet);
                        var B = new World((ulong)s, alt);
                        int first = -1;
                        for (int t = 0; t < World.MAXT; t++)
                        {
                            A.Tick(); B.Tick();
                            if (VSig(A) != VSig(B)) { first = t + 1; break; }
                            if (A.AliveCount() == 0 && B.AliveCount() == 0) break;
                        }
                        divTotal++;
                        if (first >= 0) { divCount++; firstDiv.Add(first); thisDiv++; }
                    }
                    if (thisDiv < SEEDS) inert.Add($"槽{swapSlot}换成#{pi}{Name(pi)}:{SEEDS - thisDiv}/{SEEDS}未分岔");
                }
            firstDiv.Sort();
            int med = firstDiv.Count == 0 ? -1 : firstDiv[firstDiv.Count / 2];
            int p10 = firstDiv.Count == 0 ? -1 : firstDiv[firstDiv.Count / 10];
            sb.AppendLine($"  单条替换：分岔率 {divCount * 100.0 / divTotal:F1}%（{divCount}/{divTotal}），首次分岔 tick 中位 {med}，10 分位 {p10}（一局 {World.MAXT}）");
            if (inert.Count > 0) sb.AppendLine("  未分岔的替换（换了法，村庄故事一模一样）： " + string.Join("；", inert));
            sb.AppendLine(divCount == divTotal ? "  ✅ 每一条法都能改变村庄轨迹\n" : "  ❌ 存在「换了法但村庄故事完全一样」的情况\n");

            // ═══ 级别 C：结局类别与律法的关联 ═══
            sb.AppendLine("【级别 C：结局性质】把终局离散成「存活桶×死亡桶×主导标签」类别，测律法与结局类别的关联");
            var classIds = new int[K * SEEDS];
            var lawsets = new int[K * SEEDS];
            for (int i = 0; i < K * SEEDS; i++) { classIds[i] = grid[i].classId; lawsets[i] = i / SEEDS; }
            double mi = MI(lawsets, classIds, K, 256);
            double hL = Entropy(lawsets, K), hC = Entropy(classIds, 256);
            double nmi = mi / Math.Sqrt(Math.Max(1e-9, hL * hC));
            var (nullMean, nullP95, pval) = PermNMI(lawsets, classIds, K, 256, mi);
            var distinctClasses = new System.Collections.Generic.HashSet<int>(classIds);
            sb.AppendLine($"  结局类别数 {distinctClasses.Count} / {K * SEEDS} 局；MI {mi:F3} bit，NMI {nmi:F3}");
            sb.AppendLine($"  置换零分布（打乱结局类别）：MI 均值 {nullMean:F3}，95 分位 {nullP95:F3}，p = {pval:F4}；观测/零分位 = {mi / Math.Max(1e-9, nullP95):F1}×");
            sb.AppendLine(pval < 0.01 && mi > nullP95 * 1.5 ? "  ✅ 律法显著且强地决定结局的性质（不是巧合）\n"
                       : pval < 0.01 ? "  ⚠️ 关联显著但效应量偏小\n" : "  ❌ 律法与结局类别无显著关联\n");

            // ═══ 方差分解 ═══
            sb.AppendLine("【方差分解】交叉设计 lawset(56) × seed(S)：η²=方差占比，F=组间/残差，p=置换检验");
            sb.AppendLine("  变量        |  lawset η² | seed η² | 残差 η² |   F(lawset) |  p(置换)");
            string[] vn = { "终局存活", "死亡数", "终局金币", "终局饥饿", "终局恐惧", "存活时长" };
            for (int d = 0; d < 6; d++)
            {
                var arr = new double[K * SEEDS];
                for (int i = 0; i < K * SEEDS; i++)
                    arr[i] = d == 0 ? grid[i].alive : d == 1 ? grid[i].deaths : d == 2 ? grid[i].gold
                           : d == 3 ? grid[i].hunger : d == 4 ? grid[i].fear : grid[i].aliveTick;
                var (etaLaw, etaSeed, etaErr, F, p) = TwoWay(arr, K, SEEDS);
                sb.AppendLine($"  {vn[d],-8} |   {etaLaw,5:F3}   | {etaSeed,5:F3}  | {etaErr,5:F3}  |  {F,8:F1}   |  {p:F4}");
            }
            sb.AppendLine();

            // ═══ 无法状态对照 ═══
            sb.AppendLine("【对照】无法（5 个空槽）时的跨 seed 散布，对比 56 种法的终局存活散布");
            double noLawSd = Sd(Collect((ulong s) => Run(s, new[] { -1, -1, -1, -1, -1 })), o => o.alive);
            double lawSd = Sd(Collect2(combos, SEEDS), o => o.alive);
            sb.AppendLine($"  无法：终局存活 σ = {noLawSd:F2}；56 种法：终局存活 σ = {lawSd:F2}\n");

            // ═══ 恐惧诊断 ═══
            sb.AppendLine("【恐惧诊断】恐惧是「村庄共同模式」还是「个体分化」？");
            FearDiagnose(sb);

            return sb.ToString();
        }

        static System.Collections.Generic.List<Outcome> Collect(Func<ulong, Outcome> f)
        {
            var r = new System.Collections.Generic.List<Outcome>();
            for (int s = 0; s < SEEDS; s++) r.Add(f((ulong)s));
            return r;
        }
        static System.Collections.Generic.List<Outcome> Collect2(System.Collections.Generic.List<int[]> combos, int seeds)
        {
            var r = new System.Collections.Generic.List<Outcome>();
            foreach (var L in combos) for (int s = 0; s < seeds; s++) r.Add(Run((ulong)s, L));
            return r;
        }

        static double Sd(System.Collections.Generic.List<Outcome> xs, Func<Outcome, double> sel)
        {
            double m = 0; for (int i = 0; i < xs.Count; i++) m += sel(xs[i]); m /= xs.Count;
            double v = 0; for (int i = 0; i < xs.Count; i++) { double x = sel(xs[i]) - m; v += x * x; }
            return Math.Sqrt(v / xs.Count);
        }

        // ── 恐惧诊断：共同模式指数 = 村民恐惧时间序列的平均两两相关 ──
        static void FearDiagnose(StringBuilder sb)
        {
            const int SN = 12;
            double sumCrossSD = 0, sumMean = 0, sumCorr = 0; int nC = 0, nS = 0;
            for (int s = 0; s < SN; s++)
            {
                var w = new World((ulong)s, new[] { 0, 1, 2, 3, 4 });
                int T = 600;
                var series = new int[World.N][];
                for (int i = 0; i < World.N; i++) series[i] = new int[T];
                for (int t = 0; t < T; t++)
                {
                    w.Tick();
                    for (int i = 0; i < World.N; i++) series[i][t] = w.v[i].fear;
                }
                // 末段跨村民 σ
                double m = 0; int a = 0;
                for (int i = 0; i < World.N; i++) if (w.v[i].alive) { m += w.v[i].fear; a++; }
                if (a == 0) continue;
                m /= a;
                double v = 0;
                for (int i = 0; i < World.N; i++) if (w.v[i].alive) { double x = w.v[i].fear - m; v += x * x; }
                sumCrossSD += Math.Sqrt(v / a); sumMean += m; nS++;
                // 两两相关（只在存活的人之间，用全程）
                for (int i = 0; i < World.N; i++)
                    for (int j = i + 1; j < World.N; j++)
                    { sumCorr += Pearson(series[i], series[j], T); nC++; }
            }
            if (nS > 0) sb.AppendLine($"  同一时刻：均恐惧 {sumMean / nS:F1}，跨村民 σ = {sumCrossSD / nS:F2}");
            if (nC > 0) sb.AppendLine($"  共同模式指数（村民恐惧序列平均两两相关）= {sumCorr / nC:F3}（→1 表示所有人一起涨落，→0 表示各自分化）");
            // 恐惧对结局的影响：分组对比
            sb.AppendLine();
        }

        static double Pearson(int[] a, int[] b, int n)
        {
            double ma = 0, mb = 0;
            for (int i = 0; i < n; i++) { ma += a[i]; mb += b[i]; }
            ma /= n; mb /= n;
            double sab = 0, sa = 0, sbb = 0;
            for (int i = 0; i < n; i++) { double x = a[i] - ma, y = b[i] - mb; sab += x * y; sa += x * x; sbb += y * y; }
            double d = Math.Sqrt(sa * sbb);
            return d < 1e-9 ? 0 : sab / d;
        }

        // ── 统计工具 ──
        static double Entropy(int[] xs, int C)
        {
            var cnt = new int[C]; int n = xs.Length;
            for (int i = 0; i < n; i++) if (xs[i] >= 0 && xs[i] < C) cnt[xs[i]]++;
            double h = 0;
            for (int c = 0; c < C; c++) if (cnt[c] > 0) { double p = cnt[c] * 1.0 / n; h -= p * Math.Log2(p); }
            return h;
        }
        static double MI(int[] xs, int[] ys, int Cx, int Cy)
        {
            int n = xs.Length;
            var j = new int[Cx * Cy]; var cx = new int[Cx]; var cy = new int[Cy];
            for (int i = 0; i < n; i++)
            {
                if (xs[i] < 0 || xs[i] >= Cx || ys[i] < 0 || ys[i] >= Cy) continue;
                j[xs[i] * Cy + ys[i]]++; cx[xs[i]]++; cy[ys[i]]++;
            }
            double mi = 0;
            for (int a = 0; a < Cx; a++) for (int b = 0; b < Cy; b++)
            {
                int nij = j[a * Cy + b];
                if (nij == 0 || cx[a] == 0 || cy[b] == 0) continue;
                double pij = nij * 1.0 / n, pi = cx[a] * 1.0 / n, pj = cy[b] * 1.0 / n;
                mi += pij * Math.Log2(pij / (pi * pj));
            }
            return mi;
        }
        static (double, double, double) PermNMI(int[] lawsets, int[] classes, int Cx, int Cy, double obs)
        {
            var rng = new System.Random(12345);
            var sh = new int[classes.Length];
            double sum = 0, sum2 = 0; int p = 0;
            var nulls = new double[PERM];
            for (int b = 0; b < PERM; b++)
            {
                Array.Copy(classes, sh, classes.Length);
                for (int i = sh.Length - 1; i > 0; i--) { int k = rng.Next(i + 1); (sh[i], sh[k]) = (sh[k], sh[i]); }
                double m = MI(lawsets, sh, Cx, Cy);
                nulls[b] = m; sum += m; sum2 += m * m;
                if (m >= obs) p++;
            }
            Array.Sort(nulls);
            double mean = sum / PERM;
            double p95 = nulls[(int)(PERM * 0.95)];
            return (mean, p95, (p + 1.0) / (PERM + 1));
        }

        // 交叉设计两因素方差分解 + lawset 的置换检验
        static (double etaLaw, double etaSeed, double etaErr, double F, double p) TwoWay(double[] x, int K, int S)
        {
            int n = K * S;
            double gm = 0; for (int i = 0; i < n; i++) gm += x[i]; gm /= n;
            var lm = new double[K]; var sm = new double[S];
            for (int l = 0; l < K; l++) { double t = 0; for (int s = 0; s < S; s++) t += x[l * S + s]; lm[l] = t / S; }
            for (int s = 0; s < S; s++) { double t = 0; for (int l = 0; l < K; l++) t += x[l * S + s]; sm[s] = t / K; }
            double ssLaw = 0, ssSeed = 0, ssTot = 0;
            for (int l = 0; l < K; l++) { double d = lm[l] - gm; ssLaw += S * d * d; }
            for (int s = 0; s < S; s++) { double d = sm[s] - gm; ssSeed += K * d * d; }
            for (int i = 0; i < n; i++) { double d = x[i] - gm; ssTot += d * d; }
            double ssErr = Math.Max(0, ssTot - ssLaw - ssSeed);
            int dfLaw = K - 1, dfErr = (K - 1) * (S - 1);
            double msLaw = dfLaw <= 0 ? 0 : ssLaw / dfLaw;
            double msErr = dfErr <= 0 ? 1 : ssErr / dfErr;
            double F = msErr <= 0 ? 0 : msLaw / msErr;
            double etaLaw = ssTot <= 0 ? 0 : ssLaw / ssTot;
            double etaSeed = ssTot <= 0 ? 0 : ssSeed / ssTot;
            double etaErr = ssTot <= 0 ? 0 : ssErr / ssTot;

            // 置换：打乱 lawset 标签（即随机重排每个元素的 law 归属）
            var rng = new System.Random(999);
            var idx = new int[n]; for (int i = 0; i < n; i++) idx[i] = i;
            int ge = 0;
            for (int b = 0; b < PERM; b++)
            {
                for (int i = n - 1; i > 0; i--) { int k = rng.Next(i + 1); (idx[i], idx[k]) = (idx[k], idx[i]); }
                var raw = new double[K];
                for (int l = 0; l < K; l++) { double t = 0; for (int s = 0; s < S; s++) t += x[idx[l * S + s]]; raw[l] = t / S; }
                double sl = 0; for (int l = 0; l < K; l++) { double d = raw[l] - gm; sl += S * d * d; }
                if (sl >= ssLaw) ge++;
            }
            return (etaLaw, etaSeed, etaErr, F, (ge + 1.0) / (PERM + 1));
        }
    }
}
