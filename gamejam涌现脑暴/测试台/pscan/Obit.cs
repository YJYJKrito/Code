using System; using System.Collections.Generic; using System.IO; using System.Text;

/// <summary>
/// 讣告归因验收：
///   ① 保护性法律（幼童口粮/医药/济贫/安抚）**绝不能**以「致死法」出现
///   ② 归因分布（致死/缺位/无）
///   ③ 打印几条真实讣告看文案
/// </summary>
public static class Obit
{
    const int YEARS = 30;

    static readonly (string nm, int[] ids)[] SETS = {
        ("默认三法", new[]{1, 5, 3}),
        ("生育系",   new[]{8, 9, 12}),
        ("苛政",     new[]{54, 39, 3, 7, 2, 16}),
        ("恐惧系",   new[]{1, 5, 3, 4, 7, 16, 60}),
        ("劳役系",   new[]{1, 5, 3, 11, 39}),
        ("无保护",   new[]{3, 11, 39}),
    };

    static readonly string[] PROTECTIVE = { "济贫", "医药", "幼童口粮", "安抚", "宵禁", "祭祀", "耕作", "开垦", "平粜", "常平", "买粮", "抚孤" };

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
        sb.AppendLine($"# 讣告归因验收（{seeds} 种子 × 30 年 × {SETS.Length} 玩法）\n");

        var byCauseKind = new Dictionary<string, int>();
        var causedLaws = new Dictionary<string, int>();
        var missingLaws = new Dictionary<string, int>();
        var samples = new List<string>();
        int total = 0;
        bool protectiveAsCause = false;

        foreach (var (snm, ids) in SETS)
            for (int s = 1; s <= seeds; s++)
            {
                var d = NewGame(s, ids);
                for (int y = 0; y < YEARS; y++)
                {
                    VillageLifecycleService.Advance(d, 2920);
                    if (VillageLifecycleService.CountAlive(d) == 0) break;
                }
                for (int i = 0; i < d.obituaryCount; i++)
                {
                    total++;
                    string cause = d.obituaryCauses[i];
                    byte kind = (i < d.obituaryLawKinds.Length) ? d.obituaryLawKinds[i] : (byte)0;
                    string law = d.obituaryLawNames[i] ?? "";
                    string key = cause + " · " + (kind == 1 ? "致死法" : kind == 2 ? "缺位法" : "无");
                    if (byCauseKind.ContainsKey(key)) byCauseKind[key]++; else byCauseKind[key] = 1;
                    if (kind == 1)
                    {
                        if (causedLaws.ContainsKey(law)) causedLaws[law]++; else causedLaws[law] = 1;
                        foreach (var p in PROTECTIVE)
                            if (law == p) protectiveAsCause = true;
                    }
                    else if (kind == 2)
                    {
                        if (missingLaws.ContainsKey(law)) missingLaws[law]++; else missingLaws[law] = 1;
                    }
                    if (samples.Count < 5 && kind != 0 && i % 7 == 0 && d.obituaryTexts[i].Length > 30)
                        samples.Add(d.obituaryTexts[i]);
                }
            }

        sb.AppendLine($"共 {total} 篇讣告\n");
        sb.AppendLine("## ① 保护性法律是否被当成死因\n");
        sb.AppendLine(protectiveAsCause
            ? "**❌ 有保护性法律被标成致死法**"
            : "✅ **没有一条保护性法律被当成死因**（济贫/医药/幼童口粮/安抚/宵禁/祭祀/耕作…）");

        sb.AppendLine("\n## ② 归因分布\n");
        sb.AppendLine("| 死因 · 归因 | 篇数 |");
        sb.AppendLine("|---|---|");
        var keys = new List<string>(byCauseKind.Keys); keys.Sort((a, b) => byCauseKind[b].CompareTo(byCauseKind[a]));
        foreach (var k in keys) sb.AppendLine($"| {k} | {byCauseKind[k]} |");

        sb.AppendLine("\n## ③ 被点名的致死法\n");
        sb.AppendLine("| 法 | 篇数 |");
        sb.AppendLine("|---|---|");
        var cl = new List<string>(causedLaws.Keys); cl.Sort((a, b) => causedLaws[b].CompareTo(causedLaws[a]));
        foreach (var k in cl) sb.AppendLine($"| {k} | {causedLaws[k]} |");

        sb.AppendLine("\n## ④ 被点名的缺位法\n");
        sb.AppendLine("| 法 | 篇数 |");
        sb.AppendLine("|---|---|");
        var ml = new List<string>(missingLaws.Keys); ml.Sort((a, b) => missingLaws[b].CompareTo(missingLaws[a]));
        foreach (var k in ml) sb.AppendLine($"| {k} | {missingLaws[k]} |");

        sb.AppendLine("\n## ⑤ 样例\n");
        foreach (var x in samples) sb.AppendLine("```\n" + x + "\n```");

        File.WriteAllText("scan34-obit.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan34-obit.txt");
    }

    /// <summary>验证「最致命的法」是否会把没装过的法算进去。</summary>
    public static void MostLethal(int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 「最致命的法」bug 验证（{seeds} 种子 × 6 玩法）\n");
        int runs = 0, wrongAll = 0, wrongCaused = 0;
        var examples = new List<string>();
        foreach (var (snm, ids) in SETS)
            for (int s = 1; s <= seeds; s++)
            {
                var d = NewGame(s, ids);
                for (int y = 0; y < YEARS; y++)
                {
                    VillageLifecycleService.Advance(d, 2920);
                    if (VillageLifecycleService.CountAlive(d) == 0) break;
                }
                if (d.obituaryCount < 10) continue;
                runs++;
                // (a) 现在：统计所有法名
                var all = new Dictionary<string,int>();
                var caused = new Dictionary<string,int>();
                for (int i = 0; i < d.obituaryCount; i++)
                {
                    string nm = d.obituaryLawNames[i];
                    if (string.IsNullOrEmpty(nm)) continue;
                    if (all.ContainsKey(nm)) all[nm]++; else all[nm] = 1;
                    if (i < d.obituaryLawKinds.Length && d.obituaryLawKinds[i] == 1)
                    { if (caused.ContainsKey(nm)) caused[nm]++; else caused[nm] = 1; }
                }
                string topAll = Top(all), topCaused = Top(caused);
                bool installedAll = IsInstalled(d, topAll), installedCaused = IsInstalled(d, topCaused);
                if (!string.IsNullOrEmpty(topAll) && !installedAll)
                {
                    wrongAll++;
                    if (examples.Count < 5) examples.Add($"{snm} seed={s}：最致命 = 『{topAll}』，但它没装；装了的是 {LawList(d)}");
                }
                if (!string.IsNullOrEmpty(topCaused) && !installedCaused) wrongCaused++;
            }

        sb.AppendLine($"统计 {runs} 局（讣告 ≥10 篇）\n");
        sb.AppendLine("| 算法 | 把「没装的法」当成最致命 |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| 现在（统计所有法名） | **{wrongAll} 局 / {runs}** |");
        sb.AppendLine($"| 修后（只统计 kind==1） | **{wrongCaused} 局 / {runs}** |");
        sb.AppendLine("\n**例子：**\n");
        foreach (var e in examples) sb.AppendLine("- " + e);
        File.WriteAllText("scan36-mostlethal.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine("  -> scan36-mostlethal.txt");
    }

    static string Top(Dictionary<string,int> m)
    {
        string best = ""; int n = 0;
        foreach (var kv in m) if (kv.Value > n) { n = kv.Value; best = kv.Key; }
        return best;
    }
    static bool IsInstalled(VillageSaveData d, string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        for (int i = 0; i < d.lawIds.Length; i++)
        {
            if (d.lawIds[i] <= 0) continue;
            var def = LawCatalog.FindById(d.lawIds[i]);
            if (def != null && def.name == name) return true;
        }
        return false;
    }
    static string LawList(VillageSaveData d)
    {
        var l = new List<string>();
        for (int i = 0; i < d.lawIds.Length; i++)
        {
            if (d.lawIds[i] <= 0) continue;
            var def = LawCatalog.FindById(d.lawIds[i]);
            if (def != null) l.Add(def.name);
        }
        return string.Join("·", l);
    }
}
