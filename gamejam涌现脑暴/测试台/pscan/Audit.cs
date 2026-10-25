using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 全流程不变量检查。
///
/// 每 240 tick 检查一次，把**任何**违反基本不变量的状态记下来：
///   资源越界/负数、数组长度不一致、村民字段越界、法条 id 非法、
///   兆头 id 越界、职业位越界。
///
/// 跑两遍：BugFixes=0（当前包体）和 1（三个修复全开）。
/// </summary>
public static class Audit
{
    const int YEARS = 30;

    static readonly (string nm, int[] ids)[] SETS = {
        ("不装法",   new int[0]),
        ("默认三法", new[]{1, 5, 3}),
        ("开垦系",   new[]{71, 5, 12}),
        ("苛政",     new[]{54, 39, 3, 7, 2, 16}),
        ("生育系",   new[]{8, 9, 12}),
        ("恐惧系",   new[]{1, 5, 3, 4, 7, 16, 60}),
        ("劳役系",   new[]{1, 5, 3, 11, 39}),
        ("满槽6条",  new[]{1, 5, 3, 9, 12, 71}),
        ("幼童+童工", new[]{9, 39}),
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

    static Dictionary<string, int> _counts;
    static Dictionary<string, string> _first;

    static void Hit(string key, string ctx)
    {
        if (_counts.ContainsKey(key)) _counts[key]++;
        else { _counts[key] = 1; _first[key] = ctx; }
    }

    static void Check(VillageSaveData d, string ctx)
    {
        if (d.granaryFood < 0 || d.granaryFood > d.granaryCapacity)
            Hit("粮仓越界", ctx);
        if (d.treasuryGold < 0) Hit("税库为负", ctx);
        if (d.prestige < 0) Hit("威信为负", ctx);
        if (d.prestige > 300) Hit("威信超上限", ctx);
        if (d.totalDeaths < d.obituaryCount) Hit("总死亡 < 讣告数", ctx);

        int n = d.villagerAlive.Length;
        if (d.villagerHunger.Length != n || d.villagerFear.Length != n ||
            d.villagerGold.Length != n || d.villagerFood.Length != n ||
            d.villagerAges.Length != n || d.villagerMarks.Length != n ||
            d.villagerRoles.Length != n || d.villagerChildrenAlive.Length != n)
            Hit("村民数组长度不一致", ctx);

        for (int i = 0; i < n; i++)
        {
            if (!d.villagerAlive[i]) continue;
            if (d.villagerHunger[i] < 0 || d.villagerHunger[i] > 100) Hit("饥饿越界", ctx);
            if (d.villagerFear[i] < 0 || d.villagerFear[i] > 100) Hit("恐惧越界", ctx);
            if (d.villagerGold[i] < 0) Hit("村民金币为负", ctx);
            if (d.villagerFood[i] < 0) Hit("个人存粮为负", ctx);
            if (d.villagerAges[i] < 0 || d.villagerAges[i] > 130) Hit("年龄越界", ctx);
            if (d.villagerChildrenAlive[i] < 0) Hit("在世子女为负", ctx);
            if ((d.villagerMarks[i] & 0xFFFF0000u) != 0) Hit("烙记越界位", ctx);
            if ((d.villagerRoles[i] & ~255u) != 0) Hit("角色越界位", ctx);
        }

        for (int k = 0; k < d.lawIds.Length; k++)
        {
            int id = d.lawIds[k];
            if (id < 0 || (id > 0 && LawCatalog.FindById(id) == null)) Hit("法条 id 非法", ctx);
        }

        for (int q = 0; q < d.omenEventIds.Length; q++)
            if (d.omenEventIds[q] < -1 || d.omenEventIds[q] > 13) Hit("兆头 id 越界", ctx);
    }

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 全流程不变量审计（{seeds} 种子 × 30 年 × {SETS.Length} 玩法 · 每月检查）\n");

        foreach (int fix in new[]{ 0, 1 })
        {
            Cfg.BugFixes = fix;
            _counts = new Dictionary<string, int>();
            _first = new Dictionary<string, string>();
            int runs = 0, checks = 0;

            foreach (var (nm, ids) in SETS)
                for (int s = 1; s <= seeds; s++)
                {
                    var d = NewGame(s, ids);
                    for (int y = 0; y < YEARS; y++)
                    {
                        for (int m = 0; m < 12; m++)
                        {
                            VillageLifecycleService.Advance(d, 240);
                            Check(d, $"{nm} seed={s} y={y + 1} m={m + 1}");
                            checks++;
                            if (VillageLifecycleService.CountAlive(d) == 0) break;
                        }
                        if (VillageLifecycleService.CountAlive(d) == 0) break;
                    }
                    runs++;
                }

            sb.AppendLine($"## BugFixes = {fix}（采样 {checks} 点 / {runs} 局）\n");
            if (_counts.Count == 0)
            {
                sb.AppendLine("✅ **没有发现任何不变量违规**\n");
            }
            else
            {
                int total = 0; foreach (var v in _counts.Values) total += v;
                sb.AppendLine($"⚠ **{_counts.Count} 类违规，共 {total} 次**\n");
                sb.AppendLine("| 违规 | 次数 | 第一次出现 |");
                sb.AppendLine("|---|---|---|");
                var keys = new List<string>(_counts.Keys);
                keys.Sort((a, b) => _counts[b].CompareTo(_counts[a]));
                int shown = 0;
                foreach (var k in keys)
                {
                    if (shown++ >= 8) break;
                    sb.AppendLine($"| {k} | {_counts[k]} | {_first[k]} |");
                }
                if (keys.Count > 8) sb.AppendLine($"| …其余 {keys.Count - 8} 类省略 | | |");
                sb.AppendLine();
            }
        }

        Cfg.BugFixes = 0;
        File.WriteAllText("scan28-audit.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan28-audit.txt");
    }
}
