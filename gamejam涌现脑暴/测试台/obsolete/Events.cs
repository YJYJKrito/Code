using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 第三轮：把"条件门槛"换成"权重"。
///
/// 前两轮证明了：三个法条组的参数区间几乎不相交（开垦系平均饥饿恒为 0、
/// 威信三组恒为 300、税库两组恒为 0），所以**任何绝对值门槛都必然在某些玩法下永不触发**。
///
/// 改成：每个事件永远可抽，只是权重随状态变。
/// 目标：12 个事件在三组玩法里都出现，且配比明显不同。
/// </summary>
public static class Events
{
    const int YEARS = 30;
    const int CheckEveryTicks = 1800;

    static VillageSaveData NewGame(int seed, int[] ids) {
        var d = Bootstrap.CreateNewGame(0, seed * 7919 + 13);
        for (int k = 0; k < d.lawIds.Length; k++) { d.lawIds[k] = 0; d.lawNames[k] = null; }
        for (int k = 0; k < ids.Length && k < d.lawIds.Length; k++) {
            d.lawIds[k] = ids[k];
            d.lawNames[k] = LawCatalog.FindById(ids[k])?.name;
        }
        return d;
    }

    struct Snap {
        public int pop, granary, prestige, treasury;
        public int avgHunger, avgFear, poor;
    }
    static Snap Take(VillageSaveData d) {
        var s = new Snap();
        s.pop = VillageLifecycleService.CountAlive(d);
        s.granary = d.granaryFood; s.prestige = d.prestige; s.treasury = d.treasuryGold;
        long h = 0, f = 0; int a = 0;
        for (int i = 0; i < d.villagerAlive.Length; i++) if (d.villagerAlive[i]) {
            h += d.villagerHunger[i]; f += d.villagerFear[i]; a++;
            
            if (d.villagerGold[i] < 10) s.poor++;
        }
        if (a == 0) a = 1;
        s.avgHunger = (int)(h / a); s.avgFear = (int)(f / a);
        return s;
    }

    static readonly (string, int[])[] GROUPS = {
        ("默认",   new[]{1,5,3}),
        ("开垦系", new[]{71,5,12}),
        ("不装法", new int[0]),
        ("苛政",   new[]{54,39,3}),
    };

    // ★ 权重表：每一项永远 >= 1（保证不会被"永不触发"）
    //    返回 (名字, 权重函数)
    static readonly (string, Func<Snap,int>)[] EVENTS = {
        // 负面：状态越差，权重越大
        ("蝗灾",     s => 1 + s.avgHunger / 4),
        ("疫病",     s => 1 + s.pop / 15),
        ("边患",     s => 3),                                   // 定期，与状态无关
        ("民变",     s => 1 + (300 - s.prestige) / 12 + s.avgFear / 3),
        ("贪墨",     s => 1 + s.treasury / 100),
        ("寒潮",     s => 3),
        ("匪患",     s => 1 + s.poor / 5),
        // 正面：状态越好，权重越大 —— 但也给个保底 1，穷村子偶尔也会走运
        ("丰年",     s => 1 + Math.Max(0, 40 - s.avgHunger) / 4),
        ("商队",     s => 1 + s.treasury / 60),
        ("乡绅",     s => 1 + s.prestige / 100),
        ("人口回流", s => 1 + Math.Max(0, 50 - s.pop) / 10),
        ("名医",     s => 1 + s.treasury / 80),
        ("大赦",     s => 1 + s.prestige / 120 + s.avgFear / 3),
        ("新生儿潮", s => 1 + Math.Max(0, 15 - s.avgFear) + s.granary / 200),
    };

    public static void Run(int seeds) {
        var sb = new StringBuilder();
        sb.AppendLine($"# 事件触发 · 第三轮：**条件换成权重**（{seeds} 种子 × 30 年）\n");
        sb.AppendLine("规则：每 1800 tick 抽一次，**每个事件永远可抽**，权重随状态变。");
        sb.AppendLine("**没有条件，就没有「永不触发」。**");
        sb.AppendLine("权重恒 >= 1，所以每个事件在任何玩法下都至少有一点机会。");
        sb.AppendLine("");
        sb.AppendLine("权重表：");
        sb.AppendLine("");
        sb.AppendLine("| 事件 | 权重 |");
        sb.AppendLine("|---|---|");
        sb.AppendLine("| 蝗灾 | `1 + 平均饥饿/4` |");
        sb.AppendLine("| 疫病 | `1 + 人口/15` |");
        sb.AppendLine("| 边患 | `3`（定期，与状态无关）|");
        sb.AppendLine("| 民变 | `1 + (300−威信)/12 + 平均恐惧/3` |");
        sb.AppendLine("| 贪墨 | `1 + 税库/100` |");
        sb.AppendLine("| 寒潮 | `3` |");
        sb.AppendLine("| 匪患 | `1 + 金币<10人数/5` |");
        sb.AppendLine("| 丰年 | `1 + max(0, 40−平均饥饿)/4` |");
        sb.AppendLine("| 商队 | `1 + 税库/60` |");
        sb.AppendLine("| 乡绅 | `1 + 威信/100` |");
        sb.AppendLine("| 人口回流 | `1 + max(0, 50−人口)/10` |");
        sb.AppendLine("| 名医 | `1 + 税库/80` |");
        sb.AppendLine("| 大赦 | `1 + 威信/120 + 平均恐惧/3` |");
        sb.AppendLine("| 新生儿潮 | `1 + max(0, 15−平均恐惧) + 粮仓/200` |");

        sb.AppendLine("\n## 各事件在四种玩法下的触发占比\n");
        sb.AppendLine("（每格 = 该玩法里，这个事件占全部触发的百分比。**全是 0 的格子 = 死事件**）\n");

        int nEv = EVENTS.Length;
        var counts = new int[GROUPS.Length][];
        var totals = new int[GROUPS.Length];

        for (int gi = 0; gi < GROUPS.Length; gi++) {
            counts[gi] = new int[nEv];
            var d0 = NewGame(1, GROUPS[gi].Item2);
            for (int s = 1; s <= seeds; s++) {
                var d = NewGame(s, GROUPS[gi].Item2);
                long nextCheck = CheckEveryTicks;
                while (d.tick < YEARS * 2920) {
                    int step = (int)Math.Min(240, nextCheck - d.tick);
                    if (step <= 0) step = 1;
                    VillageLifecycleService.Advance(d, step);
                    if (VillageLifecycleService.CountAlive(d) == 0) break;
                    if (d.tick >= nextCheck) {
                        nextCheck += CheckEveryTicks;
                        var snap = Take(d);
                        if (Rng.For(d.rngSeed, d.tick, 62, 0) % 100 >= 45) continue;   // 45% 触发
                        int total = 0;
                        var w = new int[nEv];
                        for (int e = 0; e < nEv; e++) { w[e] = Math.Max(1, EVENTS[e].Item2(snap)); total += w[e]; }
                        int roll = (int)(Rng.For(d.rngSeed, d.tick, 61, 0) % (uint)total);
                        int acc = 0;
                        for (int e = 0; e < nEv; e++) {
                            acc += w[e];
                            if (roll < acc) { counts[gi][e]++; totals[gi]++; break; }
                        }
                    }
                }
            }
        }

        sb.Append("| 事件 |");
        foreach (var (g, _) in GROUPS) sb.Append(" " + g + " |");
        sb.AppendLine(" 三组最低 |");
        sb.Append("|---|"); foreach (var _ in GROUPS) sb.Append("---|"); sb.AppendLine("---|");
        int dead = 0;
        for (int e = 0; e < nEv; e++) {
            sb.Append("| " + EVENTS[e].Item1 + " |");
            double lowest = 999;
            for (int gi = 0; gi < GROUPS.Length; gi++) {
                double pct = totals[gi] > 0 ? counts[gi][e] * 100.0 / totals[gi] : 0;
                sb.Append($" {pct:F1}% |");
                if (pct < lowest) lowest = pct;
            }
            if (lowest < 0.5) dead++;
            sb.AppendLine($" {(lowest < 0.5 ? "**❌ 死事件**" : "✅")} |");
        }
        sb.AppendLine($"\n**死事件数：{dead} / {nEv}**（上一轮：条件写法下有 9 / 14 永不触发）");

        // 事件总量
        sb.AppendLine("\n## 每局触发多少次\n");
        sb.AppendLine("| 玩法 | 30 年平均触发次数 |");
        sb.AppendLine("|---|---|");
        for (int gi = 0; gi < GROUPS.Length; gi++)
            sb.AppendLine($"| {GROUPS[gi].Item1} | {totals[gi] * 1.0 / seeds:F1} 次 |");

        File.WriteAllText("scan11-events.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan11-events.txt");
    }

    // ────────── 效果层：事件到底有没有用 ──────────
    public static void Effects(int seeds) {
        var sb = new StringBuilder();
        sb.AppendLine($"# 事件**效果层**实测（{seeds} 种子 × 30 年）");
        sb.AppendLine("触发层测过了。这一轮把效果真的实现出来，看它**有没有改变结局**。");

        var sets = new (string,int[])[]{
            ("不装法", new int[0]), ("默认", new[]{1,5,3}),
            ("开垦系", new[]{71,5,12}), ("苛政", new[]{54,39,3}),
        };
        var modes = new (string nm, int chance, int helper, int scale)[]{
            ("**关**（基线，当前构建）", 0, 0, 200),
            ("开 x1.0 · 无职业",       45, 0, 100),
            ("开 x1.0 · 有职业",       45, 1, 100),
            ("开 x1.5 · 无职业",       45, 0, 150),
            ("开 x1.5 · 有职业",       45, 1, 150),
            ("开 x2.0 · 有职业",       45, 1, 200),
        };

        sb.AppendLine("| 配置 |");
        foreach (var (g,_) in sets) sb.Append(" " + g + " |");
        sb.AppendLine(" 各法条组 末人口 中位 |");
        sb.Append("|---|"); foreach (var _ in sets) sb.Append("---|"); sb.AppendLine("---|");

        foreach (var (nm, ch, hp, sc) in modes) {
            Cfg.EventChance = ch; Cfg.ForceHelper = hp; Cfg.EventNegScale = sc;
            sb.Append("| " + nm + " |");
            for (int gi = 0; gi < sets.Length; gi++) {
                var fp = new List<int>();
                for (int s = 1; s <= seeds; s++) {
                    var d = NewGame(s, sets[gi].Item2);
                    for (int y = 0; y < YEARS; y++) {
                        VillageLifecycleService.Advance(d, 2920);
                        if (VillageLifecycleService.CountAlive(d) == 0) break;
                    }
                    fp.Add(VillageLifecycleService.CountAlive(d));
                }
                fp.Sort();
                sb.Append($" {fp[0]}/{fp[fp.Count/2]}/{fp[fp.Count-1]} |");
            }
            sb.AppendLine();
        }

        // 事件触发分布 + 净效果
        sb.AppendLine("");
        sb.AppendLine("## 事件触发次数（默认三法 · 开）");
        Cfg.EventChance = 45; Cfg.ForceHelper = 0; Cfg.EventNegScale = 150;
        for (int e = 0; e < 14; e++) Cfg.EventFired[e] = 0;
        for (int s = 1; s <= seeds; s++) {
            var d = NewGame(s, new[]{1,5,3});
            for (int y = 0; y < YEARS; y++) {
                VillageLifecycleService.Advance(d, 2920);
                if (VillageLifecycleService.CountAlive(d) == 0) break;
            }
        }
        int tot = 0; foreach (var v in Cfg.EventFired) tot += v;
        sb.AppendLine("| 事件 | 总次数 | 每次 |");
        sb.AppendLine("|---|---|---|");
        for (int e = 0; e < 14; e++)
            sb.AppendLine($"| {EventFx.Names[e]} | {Cfg.EventFired[e]} | {Cfg.EventFired[e]*1.0/seeds:F1} |");
        sb.AppendLine($"**合计 {tot} 次 / {seeds} 局 = 每局 {tot*1.0/seeds:F1} 次**");

        Cfg.EventChance = 0; Cfg.ForceHelper = 0; Cfg.EventNegScale = 200;
        File.WriteAllText("scan12-eveffects.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan12-eveffects.txt");
    }

}
