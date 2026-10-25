using System; using System.IO; using System.Text;

/// <summary>
/// 给职业门槛**定数值**用的分布扫描。
///
/// 为什么必须单独扫：设计文档里那些门槛（恐惧 > 70、连续 2 年饥饿……）
/// 已经踩过两次「门槛落在参数区间外」的坑。定门槛之前先看**参数实际能到多大**。
/// </summary>
public static class ProfDiag
{
    const int YEARS = 30;

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

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 职业门槛定值用诊断（{seeds} 种子 × 30 年 · 每月采样）\n");
        sb.AppendLine("**先看参数能到多大，再定门槛。** 这份表就是 §5.3 那两次「门槛落在区间外」的解药。\n");

        var sets = new (string, int[])[]{
            ("默认三法", new[]{1, 5, 3}),
            ("苛政",     new[]{1, 5, 3, 54, 39, 7, 2, 16}),
            ("恐惧系",   new[]{1, 5, 3, 4, 7, 16, 60}),
            ("捐纳系",   new[]{1, 5, 3, 28, 27, 26}),
            ("祭祀系",   new[]{1, 5, 3, 35, 6, 63}),
            ("兵役系",   new[]{1, 5, 3, 15, 10}),
            ("劳役系",   new[]{1, 5, 3, 11, 39}),
            ("处决系",   new[]{1, 5, 3, 36, 64, 34, 4, 7}),
        };

        sb.AppendLine("## 一、个人恐惧 / 饥饿的分布（全部村民采样合并）\n");
        sb.AppendLine("| 玩法 | 恐惧 p50 | p90 | p99 | **max** | 饥饿 p50 | p90 | **max** |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");

        var lawRows = new StringBuilder();
        var killRows = new StringBuilder();
        var miscRows = new StringBuilder();

        foreach (var (nm, ids) in sets)
        {
            Cfg.ResetDiagnostics();
            int[] fear = new int[10], hunger = new int[10];
            long samples = 0, immigrants = 0, maxImm = 0;
            for (int s = 1; s <= seeds; s++)
            {
                var d = NewGame(s, ids);
                long immBefore = 0;
                for (int y = 0; y < YEARS; y++)
                {
                    for (int m = 0; m < 12; m++)
                    {
                        var rep = VillageLifecycleService.Advance(d, 240);
                        immigrants += rep.immigrants;
                        immBefore += rep.immigrants;
                        for (int i = 0; i < d.villagerAlive.Length; i++)
                        {
                            if (!d.villagerAlive[i]) continue;
                            fear[Math.Min(9, d.villagerFear[i] / 10)]++;
                            hunger[Math.Min(9, d.villagerHunger[i] / 10)]++;
                            samples++;
                        }
                    }
                    if (VillageLifecycleService.CountAlive(d) == 0) break;
                }
                if (immBefore > maxImm) maxImm = immBefore;
            }

            sb.AppendLine($"| {nm} | {Pct(fear, samples, 50)} | {Pct(fear, samples, 90)} | {Pct(fear, samples, 99)} | {Max(fear) * 10 + 9} "
                        + $"| {Pct(hunger, samples, 50)} | {Pct(hunger, samples, 90)} | {Max(hunger) * 10 + 9} |");

            // 每条法触发 / 致死
            var fires = new StringBuilder();
            var kills = new StringBuilder();
            for (int id = 1; id < 72; id++)
            {
                if (Cfg.LawFire[id] > 0)  fires.Append($"{LawCatalog.FindById(id)?.name}({id})={Cfg.LawFire[id]} ");
                if (Cfg.LawKills[id] > 0) kills.Append($"{LawCatalog.FindById(id)?.name}({id})={Cfg.LawKills[id]} ");
            }
            lawRows.AppendLine($"| {nm} | {fires} |");
            killRows.AppendLine($"| {nm} | {kills} |");
            miscRows.AppendLine($"| {nm} | {immigrants} | {maxImm / Math.Max(1, seeds):F1} |");
        }

        sb.AppendLine("\n## 二、每条法的实际触发次数（40 局合计）\n");
        sb.AppendLine("> 只有出现在这里的法才**真的在跑**。空着的法装了也没用 —— 这是「死法条」清单。\n");
        sb.AppendLine("| 玩法 | 有触发的法 |");
        sb.AppendLine("|---|---|");
        sb.Append(lawRows);

        sb.AppendLine("\n## 三、每条法真的杀了几个人（40 局合计）\n");
        sb.AppendLine("> **屠者的计数器就靠这一列。** 如果处决系也是空的，说明处决法的门槛也在区间外。\n");
        sb.AppendLine("| 玩法 | 有致死的法 |");
        sb.AppendLine("|---|---|");
        sb.Append(killRows);

        sb.AppendLine("\n## 四、移民\n");
        sb.AppendLine("| 玩法 | 40 局累计移民人数 | 单局最多 |");
        sb.AppendLine("|---|---|---|");
        sb.Append(miscRows);
        sb.AppendLine("\n> 移民只在人口 < 25 **且** 粮仓 > 20 时来。人口稳定在 25 以上就永远不来 ——");
        sb.AppendLine("> **这也是「徙民」拿不到的原因。**\n");

        File.WriteAllText("scan16-profdiag.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan16-profdiag.txt");
    }

    static int Max(int[] h) { for (int i = h.Length - 1; i >= 0; i--) if (h[i] > 0) return i; return 0; }
    static int Pct(int[] h, long total, int p)
    {
        if (total <= 0) return 0;
        long need = total * p / 100, acc = 0;
        for (int i = 0; i < h.Length; i++) { acc += h[i]; if (acc >= need) return i * 10; }
        return 90;
    }
}
