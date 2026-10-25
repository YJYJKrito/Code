using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 测 13 个职业（烙记）**补完之后**：
///   ① 拿得到吗     ② 会不会烂大街     ③ 法条门槛真的起作用吗
///
/// 判定规则本体在 ProfRules.cs —— 这里只负责**跑**和**数**。
///
/// ★ 方法：**每个玩法 × 每个种子只跑一局**，同时记录 13 个职业。
///   （上一版每个职业单独跑一遍，既慢又看不出「哪个玩法出哪种人」。）
/// </summary>
public static class Prof
{
    const int YEARS = 30;

    struct P
    {
        public string name;    // 职业名
        public uint   bit;     // 烙记位
        public int[]  gate;    // 门槛法（空 = 无门槛）
        public P(string n, uint b, params int[] g) { name = n; bit = b; gate = g; }
    }

    static readonly P[] PROF = {
        new P("罪人 Sinner",   Mark.Sinner,   7),
        new P("劫匪 Bandit",   Mark.Bandit,   2),
        new P("逃兵 Deserter", Mark.Deserter, 15),
        new P("劳损 Worn",     Mark.Worn,     11),
        new P("债户 Debtor",   Mark.Debtor,   26, 27),
        new P("巫祝 Shaman",   Mark.Shaman,   35),
        new P("恩主 Patron",   Mark.Patron,   28),
        new P("屠者 Butcher",  Mark.Butcher),           // 任何致死的法
        new P("绝户 LastHeir", Mark.LastHeir),
        new P("光棍 Bachelor", Mark.Bachelor),
        new P("积怨 Bitter",   Mark.Bitter),
        new P("尸亲 Mourner",  Mark.Mourner),
        new P("徙民 Settler",  Mark.Settler),
    };

    static readonly (string nm, int[] ids)[] SETS = {
        ("默认三法", new[]{1, 5, 3}),
        ("苛政",     new[]{1, 5, 3, 54, 39, 7, 2, 16}),
        ("恐惧系",   new[]{1, 5, 3, 4, 7, 16, 60}),
        ("捐纳系",   new[]{1, 5, 3, 28, 27, 26}),
        ("祭祀系",   new[]{1, 5, 3, 35, 6, 63}),
        ("兵役系",   new[]{1, 5, 3, 15, 10}),
        ("劳役系",   new[]{1, 5, 3, 11, 39}),
        ("处决系",   new[]{1, 5, 3, 36, 64, 34, 4, 60}),
        ("荒年",     new[]{1, 5, 3, 54, 2, 39, 64}),
    };

    static VillageSaveData NewGame(int seed, int[] ids)
    {
        var d = Bootstrap.CreateNewGame(0, seed * 7919 + 13);
        for (int k = 0; k < d.lawIds.Length; k++) { d.lawIds[k] = 0; d.lawNames[k] = null; }
        for (int k = 0; k < ids.Length && k < d.lawIds.Length; k++) {
            d.lawIds[k] = ids[k];
            d.lawNames[k] = LawCatalog.FindById(ids[k])?.name;
        }
        return d;
    }

    static int AliveWith(VillageSaveData d, uint bit)
    {
        int c = 0;
        for (int i = 0; i < d.villagerAlive.Length; i++)
            if (d.villagerAlive[i] && (d.villagerMarks[i] & bit) != 0) c++;
        return c;
    }
    static int MaxOf(int[] a) { int m = 0; for (int i = 0; i < a.Length; i++) if (a[i] > m) m = a[i]; return m; }
    static int Bit(uint bit) { int b = 0; while (bit > 1) { bit >>= 1; b++; } return b; }

    struct St { public int runs, hitRuns, grants, firstTick; public double hold, pop; }

    public static void Run(int seeds)
    {
        Cfg.ProfOn = 1;
        int NS = SETS.Length, NP = PROF.Length;
        var S = new St[NS, NP];
        var maxExec = new int[NS]; var maxNbr = new int[NS]; var maxDon = new int[NS];
        var immg = new long[NS];

        // ── 跑：每个玩法 × 每个种子一局，同时记 13 个职业 ──
        for (int si = 0; si < NS; si++)
        {
            for (int s = 1; s <= seeds; s++)
            {
                Cfg.ResetProf();
                Cfg.ResetDiagnostics();
                var d = NewGame(s, SETS[si].ids);
                long imm = 0;
                for (int y = 0; y < YEARS; y++)
                {
                    var rep = VillageLifecycleService.Advance(d, 2920);
                    imm += rep.immigrants;
                    if (VillageLifecycleService.CountAlive(d) == 0) break;
                }
                immg[si] += imm;
                int pop = VillageLifecycleService.CountAlive(d);
                if (d.executions > maxExec[si]) maxExec[si] = d.executions;
                if (MaxOf(d.villagerNeighborDeaths) > maxNbr[si]) maxNbr[si] = MaxOf(d.villagerNeighborDeaths);
                if (MaxOf(d.villagerDonations) > maxDon[si]) maxDon[si] = MaxOf(d.villagerDonations);

                for (int p = 0; p < NP; p++)
                {
                    int b = Bit(PROF[p].bit);
                    long g = Cfg.ProfGrantCount[b];
                    S[si, p].runs++;
                    if (g > 0)
                    {
                        S[si, p].hitRuns++;
                        int ft = Cfg.ProfFirstTick[b];
                        if (S[si, p].firstTick == 0 || (ft > 0 && ft < S[si, p].firstTick)) S[si, p].firstTick = ft;
                    }
                    S[si, p].grants += (int)g;
                    S[si, p].hold += pop > 0 ? AliveWith(d, PROF[p].bit) * 100.0 / pop : 0;
                    S[si, p].pop += pop;
                }
            }
        }

        // ── 报表 ──
        var sb = new StringBuilder();
        sb.AppendLine($"# 职业（烙记）补完后的实测（{seeds} 种子 × 30 年 × {NS} 种玩法）\n");
        sb.AppendLine("判定规则本体在 `ProfRules.cs`。这里测三件事：**拿得到吗 / 会不会烂大街 / 法条门槛起作用吗**。\n");
        sb.AppendLine("「持有率」= 年末活着的村民当中带着这个烙记的比例（再对 40 局取平均）。\n");
        sb.AppendLine("「最好玩法」= 9 种玩法里出现率最高的那一组 —— **没出现过的职业在哪一组都出不来**。\n");

        sb.AppendLine("## 一、总表（13 个职业 × 最好玩法）\n");
        sb.AppendLine("| 职业 | 门槛 | 最好玩法 | 出现率 | 平均持有率 | 授予人次/局 | 30 年里第一次出现的 tick | 判定 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        int reach = 0, tooMany = 0;
        for (int p = 0; p < NP; p++)
        {
            int best = 0; double bestRate = -1;
            for (int si = 0; si < NS; si++)
            {
                double r = S[si, p].runs > 0 ? S[si, p].hitRuns * 100.0 / S[si, p].runs : 0;
                if (r > bestRate) { bestRate = r; best = si; }
            }
            var st = S[best, p];
            double rate = bestRate;
            double hold = st.runs > 0 ? st.hold / st.runs : 0;
            string gate = PROF[p].gate.Length == 0 ? "—"
                        : string.Join("·", Array.ConvertAll(PROF[p].gate, id => LawCatalog.FindById(id)?.name ?? id.ToString()));
            string verdict;
            if (rate < 1) verdict = "**❌ 拿不到**";
            else if (hold > 35) { verdict = "⚠ **太普遍**"; tooMany++; }
            else verdict = "✅";
            if (rate >= 1) reach++;
            string first = st.firstTick > 0 ? $"第 {st.firstTick / 2920.0:F1} 年" : "—";
            sb.AppendLine($"| {PROF[p].name} | {gate} | {SETS[best].nm} | {rate:F0}% | {hold:F1}% "
                        + $"| {(double)st.grants / Math.Max(1, st.runs):F1} | {first} | {verdict} |");
        }
        sb.AppendLine($"\n**拿得到：{reach} / {NP} · 太普遍（持有率 > 35%）：{tooMany} 个**\n");

        sb.AppendLine("## 二、法条门槛验证：装了 vs 没装\n");
        sb.AppendLine("有门槛的职业：装了对口法的那一组，出现率必须明显高于默认三法。\n");
        sb.AppendLine("| 职业 | 装了 | 没装（默认三法） | 判定 |");
        sb.AppendLine("|---|---|---|---|");
        for (int p = 0; p < NP; p++)
        {
            if (PROF[p].gate.Length == 0) continue;
            double on = 0;
            for (int si = 0; si < NS; si++)
            {
                // 门槛是「或」：装了其中任意一条就算这一组验得了
                bool has = false;
                foreach (var g in PROF[p].gate)
                    foreach (var id in SETS[si].ids) if (id == g) has = true;
                if (!has) continue;
                double r = S[si, p].runs > 0 ? S[si, p].hitRuns * 100.0 / S[si, p].runs : 0;
                if (r > on) on = r;
            }
            double off = S[0, p].runs > 0 ? S[0, p].hitRuns * 100.0 / S[0, p].runs : 0;
            sb.AppendLine($"| {PROF[p].name} | **{on:F0}%** | {off:F0}% | {(on >= 1 && on >= off + 20 ? "✅ 门槛生效" : (on < 1 ? "**❌ 还是拿不到**" : "⚠ 没区分度"))} |");
        }

        sb.AppendLine("\n## 三、玩法 × 职业 出现率矩阵\n");
        sb.AppendLine("行 = 玩法，列 = 职业。**这是「你写的法律 → 村里出现了什么人」最直接的证据。**\n");
        sb.Append("| 玩法 |");
        for (int p = 0; p < NP; p++) sb.Append(" " + PROF[p].name.Split(' ')[0] + " |");
        sb.AppendLine();
        sb.Append("|---|");
        for (int p = 0; p < NP; p++) sb.Append("---|");
        sb.AppendLine();
        for (int si = 0; si < NS; si++)
        {
            sb.Append($"| {SETS[si].nm} |");
            for (int p = 0; p < NP; p++)
            {
                double r = S[si, p].runs > 0 ? S[si, p].hitRuns * 100.0 / S[si, p].runs : 0;
                sb.Append(r >= 1 ? $" {r:F0}% |" : " — |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("\n## 四、计数器的实际量级（给门槛定值用）\n");
        sb.AppendLine("| 玩法 | 最大处决次数 | 最大邻居死亡累计 | 最大个人捐纳 | 40 局累计移民 |");
        sb.AppendLine("|---|---|---|---|---|");
        for (int si = 0; si < NS; si++)
            sb.AppendLine($"| {SETS[si].nm} | {maxExec[si]} | {maxNbr[si]} | {maxDon[si]} | {immg[si]} |");

        sb.AppendLine("\n## 五、当前门槛\n");
        sb.AppendLine("```");
        sb.AppendLine($"罪人   装私刑倾向(7)  且 恐惧 > {Cfg.ProfSinnerFear}");
        sb.AppendLine($"劫匪   装饥则抢(2)    且 饥饿 > {Cfg.ProfBanditHunger}");
        sb.AppendLine($"逃兵   装兵役征召(15) 且 年龄 ∈ [{Cfg.ProfDeserterMinAge},{Cfg.ProfDeserterMaxAge}] 且 恐惧 > {Cfg.ProfDeserterFear}");
        sb.AppendLine($"劳损   装劳役(11)     且 年龄 ≥ {Cfg.ProfWornAge} 且 饥饿 > {Cfg.ProfWornHunger}");
        sb.AppendLine($"债户   装买粮/丁税   且 年龄 ≥ {Cfg.ProfDebtorAge} 且 金币 = 0 且 曾经有 ≥ {Cfg.ProfDebtorPeak} 金（破产过）");
        sb.AppendLine($"巫祝   装祭祀(35)     且 恐惧 > {Cfg.ProfShamanFear}");
        sb.AppendLine($"恩主   捐纳累计 ≥ {Cfg.ProfPatronDonations} 次（全村）");
        sb.AppendLine($"屠者   任何致死法条处决过人 —— 给「最不害怕的成年人」（同一人反复执刑，所以人数自然受限）");
        sb.AppendLine($"绝户   年龄 ≥ {Cfg.ProfLastHeirAge} 且 曾有子女 且 在世子女 = 0");
        sb.AppendLine($"光棍   年龄 ≥ {Cfg.ProfBachelorAge} 且 曾有子女 = 0");
        sb.AppendLine($"积怨   饥饿 > {Cfg.ProfBitterHunger} 连续 ≥ {Cfg.ProfBitterTicks / 2920.0:F1} 年");
        sb.AppendLine($"尸亲   邻居死亡累计 ≥ {Cfg.ProfMournerDeaths}");
        sb.AppendLine($"徙民   移民进村（人口 < {Cfg.ImmThreshold} 且 粮仓 > {Cfg.ImmGranary}）");
        sb.AppendLine("```");

        Cfg.ProfOn = 0;   // ★ 测完关掉，别把后面别的测试拖下水
        File.WriteAllText("scan15-prof.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan15-prof.txt");
    }
}
