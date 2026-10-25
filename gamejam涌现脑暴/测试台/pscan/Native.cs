using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 对**当前构建的原生实现**做一次体检：
///   ① 14 个事件各触发几次（VillageEvents）
///   ② 13 个职业烙记各自有多少人拿到（VillagerProfessions）
///   ③ 8 个事件用角色各自有多少人
///
/// 这是 2026-10-18 新包体的验收扫描 —— 之前的 EventFx/ProfRules 是测试台自己
/// 模拟的，现在构建里有了真东西，直接测真的。
/// </summary>
public static class Native
{
    const int YEARS = 30;

    static readonly (string nm, int[] ids)[] SETS = {
        ("不装法",   new int[0]),
        ("默认三法", new[]{1, 5, 3}),
        ("开垦系",   new[]{71, 5, 12}),
        ("苛政",     new[]{54, 39, 3, 7, 2, 16}),
        ("生育系",   new[]{8, 9, 12}),
        ("劳役系",   new[]{1, 5, 3, 11, 39}),
        ("兵役系",   new[]{1, 5, 3, 15, 10}),
        ("恐惧系",   new[]{1, 5, 3, 4, 7, 16, 60}),
        ("祭祀系",   new[]{1, 5, 3, 35, 6, 63}),
        ("满槽6条",  new[]{1, 5, 3, 9, 12, 71}),
        ("饥则抢(2)", new[]{2}),
        ("婚育(8)+童工", new[]{8, 39}),
        ("捐纳(28)", new[]{28, 3}),
        ("买粮(26)", new[]{26, 3}),
        ("常平(51)", new[]{51, 3}),
    };

    static readonly string[] EVENT_NAMES = {
        "蝗灾","疫病","边患","民变","贪墨","寒潮","匪患","丰年","商队","乡绅","人口回流","名医","大赦","新生儿潮"
    };

    // 13 个职业烙记（位号）
    static readonly (string nm, uint bit)[] MARKS = {
        ("罪人", 1u), ("劫匪", 2u), ("逃兵", 4u), ("绝户", 8u), ("光棍", 32u),
        ("债户", 64u), ("徙民", 128u), ("劳损", 1024u), ("积怨", 2048u),
        ("屠者", 4096u), ("尸亲", 8192u), ("恩主", 16384u), ("巫祝", 32768u),
    };

    static readonly (string nm, uint role)[] ROLES = {
        ("耕者", 1u), ("医师", 2u), ("游侠", 4u), ("里正", 8u),
        ("账房", 16u), ("役夫", 32u), ("粮商", 64u), ("稳婆", 128u),
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

    public static void Run(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 新构建原生实现体检（{seeds} 种子 × 30 年 × {SETS.Length} 玩法）\n");
        sb.AppendLine("产出/税库/威信/私粮/职业/事件**全部是构建里的原生代码**，不是测试台模拟。\n");

        Cfg.NativeEventChance = 45;

        // ── 事件 ──
        var fired = new long[14];
        var popFin = new List<int>();
        int dead = 0, runs = 0;
        for (int gi = 0; gi < SETS.Length; gi++)
            for (int s = 1; s <= seeds; s++)
            {
                for (int e = 0; e < 14; e++) Cfg.EventFired[e] = 0;
                var d = NewGame(s, SETS[gi].ids);
                for (int y = 0; y < YEARS; y++)
                {
                    VillageLifecycleService.Advance(d, 2920);
                    if (VillageLifecycleService.CountAlive(d) == 0) break;
                }
                for (int e = 0; e < 14; e++) fired[e] += Cfg.EventFired[e];
                int f = VillageLifecycleService.CountAlive(d);
                popFin.Add(f); if (f == 0) dead++;
                runs++;
            }
        popFin.Sort();

        long total = 0; foreach (var v in fired) total += v;
        sb.AppendLine("## 一、14 个事件\n");
        sb.AppendLine($"**每局 {total * 1.0 / Math.Max(1, runs):F1} 次**（30 年）\n");
        sb.AppendLine("| 事件 | 总次数 | 每局 |");
        sb.AppendLine("|---|---|---|");
        for (int e = 0; e < 14; e++)
            sb.AppendLine($"| {EVENT_NAMES[e]} | {fired[e]} | {fired[e] * 1.0 / Math.Max(1, runs):F1} |");
        sb.AppendLine($"\n末人口 最小/中位/最大 = {popFin[0]}/{popFin[popFin.Count/2]}/{popFin[popFin.Count-1]} · 灭村率 {dead * 100.0 / Math.Max(1, runs):F0}%\n");

        // 触发率 100% 时的上限（判断瓶颈到底是概率还是单兆头）
        Cfg.NativeEventChance = 100;
        long total100 = 0; int runs100 = 0;
        for (int gi = 0; gi < SETS.Length; gi++)
            for (int s = 1; s <= seeds; s++)
            {
                for (int e = 0; e < 14; e++) Cfg.EventFired[e] = 0;
                var d = NewGame(s, SETS[gi].ids);
                for (int y = 0; y < YEARS; y++)
                {
                    VillageLifecycleService.Advance(d, 2920);
                    if (VillageLifecycleService.CountAlive(d) == 0) break;
                }
                for (int e = 0; e < 14; e++) total100 += Cfg.EventFired[e];
                runs100++;
            }
        Cfg.NativeEventChance = 45;
        sb.AppendLine($"**触发率 100% 时：每局 {total100 * 1.0 / Math.Max(1, runs100):F1} 次**\n");

        // ── 职业烙记 / 角色 ──
        var markRuns = new int[MARKS.Length];
        var roleRuns = new int[ROLES.Length];
        var markHold = new double[MARKS.Length];
        var roleHold = new double[ROLES.Length];
        int runs2 = 0, pop2 = 0;
        for (int gi = 0; gi < SETS.Length; gi++)
            for (int s = 1; s <= seeds; s++)
            {
                var d = NewGame(s, SETS[gi].ids);
                for (int y = 0; y < YEARS; y++)
                {
                    VillageLifecycleService.Advance(d, 2920);
                    if (VillageLifecycleService.CountAlive(d) == 0) break;
                }
                int pop = VillageLifecycleService.CountAlive(d);
                if (pop <= 0) continue;
                runs2++; pop2 += pop;
                for (int k = 0; k < MARKS.Length; k++)
                {
                    int n = 0;
                    for (int i = 0; i < d.villagerAlive.Length; i++)
                        if (d.villagerAlive[i] && (d.villagerMarks[i] & MARKS[k].bit) != 0) n++;
                    if (n > 0) markRuns[k]++;
                    markHold[k] += n * 100.0 / pop;
                }
                for (int k = 0; k < ROLES.Length; k++)
                {
                    int n = 0;
                    for (int i = 0; i < d.villagerAlive.Length; i++)
                        if (d.villagerAlive[i] && (d.villagerRoles[i] & ROLES[k].role) != 0) n++;
                    if (n > 0) roleRuns[k]++;
                    roleHold[k] += n * 100.0 / pop;
                }
            }

        sb.AppendLine("## 二、13 个职业烙记（年末存活者里）\n");
        sb.AppendLine("| 职业 | 出现率 | 平均持有率 |");
        sb.AppendLine("|---|---|---|");
        for (int k = 0; k < MARKS.Length; k++)
            sb.AppendLine($"| {MARKS[k].nm} | {markRuns[k] * 100.0 / Math.Max(1, runs2):F0}% | {markHold[k] / Math.Max(1, runs2):F1}% |");

        sb.AppendLine("\n## 三、8 个事件角色\n");
        sb.AppendLine("| 角色 | 出现率 | 平均持有率 |");
        sb.AppendLine("|---|---|---|");
        for (int k = 0; k < ROLES.Length; k++)
            sb.AppendLine($"| {ROLES[k].nm} | {roleRuns[k] * 100.0 / Math.Max(1, runs2):F0}% | {roleHold[k] / Math.Max(1, runs2):F1}% |");

        File.WriteAllText("scan23-native.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan23-native.txt");
    }
}
