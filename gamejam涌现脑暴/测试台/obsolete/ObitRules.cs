using System;

/// <summary>
/// 讣告归因规则。
///
/// 旧实现的问题：`PreferredLaw` 只看「装没装」，不看「是不是它干的」。
/// 所以装了「幼童口粮」「医药」之后，每一个幼儿死亡都会被写成
/// 「他死于你写的『医药』」—— 保护性法律被当成死因。
///
/// 新规则：每次死亡先看 **有没有致死的法**；没有的话，再看 **缺了哪条能救命的法**。
///   致死法 → 「是你写的『X』。」
///   缺位法 → 「村里没有『X』。」
///   都没有 → 「没有一条法能替他负责。」
/// </summary>
public static class ObitRules
{
    public const byte KindNone = 0;
    public const byte KindCaused = 1;
    public const byte KindMissing = 2;

    // 会让人饿死的法（增加饥饿 / 抽干粮仓 / 抽干买粮的钱）
    static readonly int[] HungerHarm = { 11, 27, 31, 37, 39, 44, 45, 48, 61, 63, 3 };
    // 能救饿死的法
    static readonly int[] HungerHelp = { 1, 26, 29, 46, 51, 52, 71, 5 };

    // 会让人吓死的法
    static readonly int[] FearHarm = { 4, 7, 16, 34, 50, 60, 62, 2 };
    static readonly int[] FearHelp = { 6, 10, 13, 18, 22, 23, 25, 35 };

    // 会弄死幼儿的法 / 能救幼儿的法
    static readonly int[] InfantHarm = { 54, 47, 65, 39 };
    static readonly int[] InfantHelp = { 9, 12, 13, 8, 52 };

    // 事件对应的「本可以减轻它」的法
    static readonly int[] EpidemicHelp = { 12, 13 };
    static readonly int[] RaidHelp = { 15, 25 };
    static readonly int[] RiotHelp = { 6, 1 };
    static readonly int[] BanditHelp = { 10, 25 };

    /// <summary>最近一次归因（测试台用：AppendObituary 同步读取后清空）</summary>
    public static byte LastKind;
    public static string LastName;

    /// <summary>接线用：算归因、记下 kind/name，返回槽位。</summary>
    public static int AttributeSlot(VillageSaveData d, string cause)
    {
        int slot; string name;
        LastKind = Attribute(d, cause, out slot, out name);
        LastName = name;
        return slot;
    }

    /// <summary>给某次死亡定「法」，返回槽位（缺位时为 -1）与法名。</summary>
    public static byte Attribute(VillageSaveData d, string cause, out int slot, out string lawName)
    {
        slot = -1; lawName = null;

        int[] harm, help;
        switch (cause)
        {
            case "饥饿": harm = HungerHarm; help = HungerHelp; break;
            case "惊吓": harm = FearHarm;   help = FearHelp;   break;
            case "夭折": harm = InfantHarm; help = InfantHelp; break;
            default:
                harm = null; help = HelpForEvent(cause);
                break;
        }

        if (harm != null)
        {
            int h = FirstInstalled(d, harm);
            if (h >= 0)
            {
                slot = h;
                lawName = NameAt(d, h);
                return KindCaused;
            }
        }

        if (help != null)
        {
            int miss = FirstMissing(d, help);
            if (miss > 0)
            {
                lawName = LawCatalog.FindById(miss)?.name;
                return KindMissing;
            }
        }
        return KindNone;
    }

    static int[] HelpForEvent(string cause)
    {
        switch (cause)
        {
            case "疫病": return EpidemicHelp;
            case "边患": return RaidHelp;
            case "民变": return RiotHelp;
            case "匪患": return BanditHelp;
            default: return null;
        }
    }

    static int FirstInstalled(VillageSaveData d, int[] ids)
    {
        if (d.lawIds == null) return -1;
        for (int k = 0; k < ids.Length; k++)
            for (int i = 0; i < d.lawIds.Length; i++)
                if (d.lawIds[i] == ids[k]) return i;
        return -1;
    }

    static int FirstMissing(VillageSaveData d, int[] ids)
    {
        for (int k = 0; k < ids.Length; k++)
            if (!HasLaw(d, ids[k])) return ids[k];
        return -1;
    }

    static bool HasLaw(VillageSaveData d, int id)
    {
        if (d.lawIds == null) return false;
        for (int i = 0; i < d.lawIds.Length; i++)
            if (d.lawIds[i] == id) return true;
        return false;
    }

    static string NameAt(VillageSaveData d, int slot)
    {
        if (d.lawNames != null && slot >= 0 && slot < d.lawNames.Length) return d.lawNames[slot];
        return "";
    }

    // ────────────── 文本 ──────────────

    public static string LawLine(byte kind, string lawName)
    {
        if (kind == KindCaused && !string.IsNullOrEmpty(lawName)) return "是你写的『" + lawName + "』。";
        if (kind == KindMissing && !string.IsNullOrEmpty(lawName)) return "村里没有『" + lawName + "』。";
        return "没有一条法能替他负责。";
    }

    /// <summary>一句个人史（没有就返回空串）。确定性：只看当事人状态。</summary>
    public static string History(VillageSaveData d, int i)
    {
        if (d.villagerChildren[i] > 0 && d.villagerChildrenAlive[i] <= 0)
            return "他没能留下一个孩子。";
        if (d.villagerNeighborDeaths[i] >= 15)
            return "他送走过 " + d.villagerNeighborDeaths[i] + " 个邻居。";
        if (d.villagerDonations[i] >= 3)
            return "他给村里捐过 " + d.villagerDonations[i] + " 次。";
        if (d.villagerHungryTicks[i] >= 730)
            return "他饿了很久。";
        if (Traits.HasMark(d, i, Mark.Bandit)) return "他抢过别人的粮。";
        if (Traits.HasMark(d, i, Mark.Mourner)) return "他替别人送过葬。";
        if (Traits.HasMark(d, i, Mark.Settler)) return "他是从外面来的。";
        if (Traits.HasMark(d, i, Mark.Butcher)) return "他动过手。";
        if (Traits.HasMark(d, i, Mark.Shaman)) return "他替村子求过神。";
        return "";
    }

    public static string Build(VillageSaveData d, int i, string cause, int lawIndex, byte kind, string lawName)
    {
        string s = d.villagerNames[i] + "，" + d.villagerAges[i] + " 岁。\n";
        string date = VillageTime.FormatDay(d.tick);
        if (cause == "夭折") s += "死于 " + date + "。还没满三岁。\n";
        else if (cause == "寿终") s += "死于 " + date + "，寿终。\n";
        else s += "死于 " + date + " 的" + cause + "。\n";
        s += LawLine(kind, lawName) + "\n";
        string h = History(d, i);
        if (h.Length > 0) s += h;
        return s;
    }
}
