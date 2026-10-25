using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 测「对口法律改写事件」。
///
/// 方法：**强制单个事件反复触发**，比较装了对应法 vs 没装的**每次打击量**。
/// 这样能完全隔离掉"法条本身的其他效果"，只看它有没有改写事件。
/// </summary>
public static class LawMod
{
    const int YEARS = 30;

    static VillageSaveData NewGame(int seed, int[] ids) {
        var d = Bootstrap.CreateNewGame(0, seed * 7919 + 13);
        for (int k = 0; k < d.lawIds.Length; k++) { d.lawIds[k] = 0; d.lawNames[k] = null; }
        for (int k = 0; k < ids.Length && k < d.lawIds.Length; k++) {
            d.lawIds[k] = ids[k];
            d.lawNames[k] = LawCatalog.FindById(ids[k])?.name;
        }
        return d;
    }

    /// <summary>强制某个事件，跑 seeds 局，返回 (总触发, 总杀人数, 总粮仓损失, 总粮仓收益)</summary>
    static (int fires, int kills, int granary) Force(int ev, int[] ids, int seeds) {
        Cfg.EventChance = 25; Cfg.ForceEvent = ev; Cfg.ForceHelper = 0; Cfg.EventNegScale = 100;
        Cfg.EventKills = new int[14]; Cfg.EventGranary = new int[14]; Cfg.EventFired = new int[14];
        Cfg.EventScaleLog = new long[14];   // ★ 别忘了这个 —— 上一版漏了，数字全是累加出来的
        for (int s = 1; s <= seeds; s++) {
            var d = NewGame(s, ids);
            for (int y = 0; y < YEARS; y++) {
                VillageLifecycleService.Advance(d, 2920);
                if (VillageLifecycleService.CountAlive(d) == 0) break;
            }
        }
        Cfg.EventChance = 0; Cfg.ForceEvent = -1;
        return (Cfg.EventFired[ev], Cfg.EventKills[ev], Cfg.EventGranary[ev]);
    }

    public static void Run(int seeds) {
        var sb = new StringBuilder();
        sb.AppendLine($"# 对口法律改写事件 · 实测（{seeds} 种子 × 30 年 · 每次只强制一个事件）\n");
        sb.AppendLine("**这是隔离测试**：强制同一个事件反复触发，只改「装不装对口法」。");
        sb.AppendLine("所以下面的差值**完全来自律法改写**，不含法条自身的其他效果。\n");

        // (事件名, 指数, 对口法id, 法名, 基准法组, 对照法组)
        var cases = new (string ev, int idx, int law, string lawName, int[] without, int[] with)[]{
            ("蝗灾",   0,  5, "耕作",   new int[0],      new[]{5}),
            ("疫病",   1, 12, "医药",   new int[0],      new[]{12}),
            ("边患",   2, 15, "兵役征召", new int[0],      new[]{15}),
            ("民变",   3,  6, "安抚",   new int[0],      new[]{6}),
            ("匪患",   6, 10, "宵禁",   new int[0],      new[]{10}),
            ("寒潮",   5, 30, "屯粮",   new int[0],      new[]{30}),
        };

        sb.AppendLine("| 事件 | 对口法 | 没装 | 装了 | **实际折扣** | 粮仓 ×| 死人 ×| 判定 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        int good = 0;
        foreach (var (ev, idx, law, lawName, wo, wi) in cases) {
            var a = Force(idx, wo, seeds);
            var b = Force(idx, wi, seeds);
            double perA = a.fires > 0 ? (double)a.granary / a.fires : 0;
            double perB = b.fires > 0 ? (double)b.granary / b.fires : 0;
            double ratio = perA > 0 ? perB / perA : (perB == 1 ? 1 : 0);
            double perKillA = a.kills * 1.0 / Math.Max(1, a.fires);
            double perKillB = b.kills * 1.0 / Math.Max(1, b.fires);
            // ★ 判据要同时看粮和死人：死人类事件的粮仓损失是 0
            bool okKill = perKillB < perKillA * 0.8;
            bool okGrain = perA > 1 && perB < perA * 0.8;
            bool ok = okKill || okGrain;
            if (ok) good++;
            double scale = Cfg.EventScaleLog[idx] * 1.0 / Math.Max(1, b.fires);
            sb.AppendLine($"| {ev} | {lawName} | 粮{perA:F0}/死{perKillA:F1}"
                        + $" | 粮{perB:F0}/死{perKillB:F1}"
                        + $" | **{scale:F0}%** | ×{ratio:F2} | ×{(perKillA>0?perKillB/perKillA:1):F2}"
                        + $" | {(ok ? "✅ 有效" : "**❌ 无效**")} |");
        }

        // 反向：装了加重法
        sb.AppendLine("\n## 反向：装了「加重」的法（苛政侧）\n");
        sb.AppendLine("| 事件 | 加重法 | 没装：每次打击 | 装了：每次打击 | 判定 |");
        sb.AppendLine("|---|---|---|---|---|");
        var neg = new (string ev, int idx, int law, string lawName)[]{
            ("民变", 3, 16, "连坐"),
            ("匪患", 6, 2, "饥则抢"),
            ("边患", 2, 48, "威权"),
        };
        foreach (var (ev, idx, law, lawName) in neg) {
            var a = Force(idx, new int[0], seeds);
            var b = Force(idx, new[]{law}, seeds);
            double perA = a.fires > 0 ? (double)a.granary / a.fires : 0;
            double perB = b.fires > 0 ? (double)b.granary / b.fires : 0;
            bool ok = perB > perA * 1.1;
            sb.AppendLine($"| {ev} | {lawName} | −{perA:F0} | −{perB:F0} | {(ok ? "✅ 加重了" : "⚠ 没加重")} |");
        }

        // 正面对口
        sb.AppendLine("\n## 正面事件的对口增益\n");
        sb.AppendLine("| 事件 | 对口法 | 没装：每次收益 | 装了：每次收益 | 判定 |");
        sb.AppendLine("|---|---|---|---|---|");
        var pos = new (string ev, int idx, int law, string lawName)[]{
            ("丰年", 7, 5, "耕作"),
            ("人口回流", 10, 17, "流民法"),
        };
        foreach (var (ev, idx, law, lawName) in pos) {
            var a = Force(idx, new int[0], seeds);
            var b = Force(idx, new[]{law}, seeds);
            double perA = a.fires > 0 ? (double)a.granary / a.fires : 0;
            double perB = b.fires > 0 ? (double)b.granary / b.fires : 0;
            bool ok = perB > perA * 1.1;
            sb.AppendLine($"| {ev} | {lawName} | +{perA:F0} | +{perB:F0} | {(ok ? "✅ 增益" : "⚠ 没增益")} |");
        }

        sb.AppendLine($"\n**减轻类有效：{good} / {cases.Length}**");
        Cfg.LawModOn = 1;
        File.WriteAllText("scan14-lawmod.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan14-lawmod.txt");
    }
}
