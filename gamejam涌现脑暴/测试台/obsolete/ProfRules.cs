using System;

/// <summary>
/// 职业（烙记）的**获取规则**。
///
/// 设计原则（见《突发事件与职业-设计》§5）：
///   每一个职业都是「**你写的法律 × 村里有人处于某种状态**」。
///   一旦烙上就永久保留 —— 13 个职业标记全在永久位 0–15。
///
/// 为什么要单独一个类：
///   Sim.cs 是 setup.py 从 DLL 反编译后生成的，写在里面的规则
///   下次重生成会被冲掉。规则放这里，setup.py 只负责插 6 个调用点。
///
/// 调用点（都由 setup.py 的 POST_PATCHES 插入）：
///   EnsureLifecycleArrays  → ProfRules.Ensure      数组补齐
///   AppendChild            → ProfRules.OnBirth     记父母 + 在世子女
///   AppendImmigrant        → ProfRules.OnImmigrant 直接烙「徙民」
///   KillVillager           → ProfRules.OnDeath     处决计数 / 邻居送葬 / 子女减一
///   ApplyAll（法条触发）   → ProfRules.OnLawFired  捐纳计数
///   SimulateTick（每 60t） → ProfRules.Scan        主判定
/// </summary>
public static class ProfRules
{
    // ══════════════════ 数组补齐 ══════════════════

    public static void Ensure(VillageSaveData d, int n)
    {
        if (d == null || n <= 0) return;
        Grow(ref d.villagerNeighborDeaths, n, 0);
        Grow(ref d.villagerDonations,     n, 0);
        Grow(ref d.villagerChildrenAlive, n, 0);
        Grow(ref d.villagerMother,        n, -1);
        Grow(ref d.villagerFather,        n, -1);
        Grow(ref d.villagerHungryTicks,   n, 0);
        Grow(ref d.villagerWealthPeak,    n, 0);
    }

    static void Grow(ref int[] a, int n, int fill)
    {
        if (a == null)
        {
            a = new int[n];
            for (int i = 0; i < n; i++) a[i] = fill;
            return;
        }
        if (a.Length >= n) return;
        int old = a.Length;
        Array.Resize(ref a, n);
        for (int i = old; i < n; i++) a[i] = fill;
    }

    // ══════════════════ 四个事件钩子 ══════════════════

    /// <summary>有人出生：记父母，给双方的在世子女数 +1。</summary>
    public static void OnBirth(VillageSaveData d, int child, int mother, int father)
    {
        Ensure(d, d.villagerAlive.Length);
        ResetSlot(d, child);                 // ★ 槽位是被回收复用的，新人的计数器必须清零
        Bump(d.villagerChildrenAlive, mother, +1);
        Bump(d.villagerChildrenAlive, father, +1);
        if (child >= 0 && child < d.villagerMother.Length)
        {
            d.villagerMother[child] = mother;
            d.villagerFather[child] = father;
        }
    }

    /// <summary>移民进来：直接烙「徙民」。这是唯一一个不需要条件的职业。</summary>
    public static void OnImmigrant(VillageSaveData d, int i)
    {
        Ensure(d, d.villagerAlive.Length);
        ResetSlot(d, i);
        if (i >= 0 && i < d.villagerMother.Length) { d.villagerMother[i] = -1; d.villagerFather[i] = -1; }
        if (Cfg.ProfOn > 0) Grant(d, i, Mark.Settler);
    }

    /// <summary>槽位复用：新人拿到一个死人用过的槽位，所有历史计数器归零。</summary>
    static void ResetSlot(VillageSaveData d, int i)
    {
        if (i < 0) return;
        if (i < d.villagerNeighborDeaths.Length) d.villagerNeighborDeaths[i] = 0;
        if (i < d.villagerDonations.Length)     d.villagerDonations[i]     = 0;
        if (i < d.villagerChildrenAlive.Length) d.villagerChildrenAlive[i] = 0;
        if (i < d.villagerHungryTicks.Length)   d.villagerHungryTicks[i]   = 0;
        if (i < d.villagerWealthPeak.Length)    d.villagerWealthPeak[i]    = 0;
    }

    /// <summary>有人死了：处决计数 / 父母在世子女 -1 / 左右邻居的「送葬数」+1。</summary>
    public static void OnDeath(VillageSaveData d, int i, string cause, int lawIndex)
    {
        Ensure(d, d.villagerAlive.Length);

        // ★ 处决：KillByLaw 传来的 cause 就是「律法」
        if (cause == "律法")
        {
            d.executions++;
            if (lawIndex >= 0 && lawIndex < d.lawIds.Length)
            {
                int id = d.lawIds[lawIndex];
                if (id > 0 && id < Cfg.LawKills.Length) Cfg.LawKills[id]++;
            }
        }

        // 父母在世子女 -1（绝户的判据）
        if (i >= 0 && i < d.villagerMother.Length)
        {
            Bump(d.villagerChildrenAlive, d.villagerMother[i], -1);
            Bump(d.villagerChildrenAlive, d.villagerFather[i], -1);
        }

        // 邻居送葬（尸亲的判据）。邻居 = 槽位 ±1（环绕），与 LawScope.Neighbors 同一套。
        int n = d.villagerAlive.Length;
        if (n > 1)
        {
            int nx = (i + 1) % n;
            int pv = (i - 1 + n) % n;
            if (nx != i && d.villagerAlive[nx]) d.villagerNeighborDeaths[nx]++;
            if (pv != i && d.villagerAlive[pv]) d.villagerNeighborDeaths[pv]++;
        }
    }

    /// <summary>某条法在某个人身上触发了：给「捐纳」记数。</summary>
    public static void OnLawFired(VillageSaveData d, int i, int lawId)
    {
        if (lawId > 0 && lawId < Cfg.LawFire.Length) Cfg.LawFire[lawId]++;
        if (lawId != 28) return;                       // 28 = 捐纳
        if (i < 0) return;
        Ensure(d, d.villagerAlive.Length);
        if (i < d.villagerDonations.Length) d.villagerDonations[i]++;
    }

    static void Bump(int[] a, int i, int delta)
    {
        if (a == null || i < 0 || i >= a.Length) return;
        a[i] = Math.Max(0, a[i] + delta);
    }

    // ══════════════════ 主判定（每 60 tick） ══════════════════

    public static void Scan(VillageSaveData d, int tick)
    {
        if (Cfg.ProfOn <= 0 || d == null) return;
        int n = d.villagerAlive.Length;
        Ensure(d, n);

        // 全村捐纳累计 —— 恩主按「全村捐够了」判，不是按某一个人的次数
        int totalDonations = 0;
        for (int q = 0; q < n && q < d.villagerDonations.Length; q++) totalDonations += d.villagerDonations[q];

        for (int i = 0; i < n; i++)
        {
            if (!d.villagerAlive[i])
            {
                d.villagerHungryTicks[i] = 0;
                continue;
            }

            // 「连续 N 年」用的累加器：Scan 每 60 tick 跑一次，所以一次 +60
            d.villagerHungryTicks[i] = (d.villagerHunger[i] >  Cfg.ProfBitterHunger) ? d.villagerHungryTicks[i] + 60 : 0;
            if (d.villagerGold[i] > d.villagerWealthPeak[i]) d.villagerWealthPeak[i] = d.villagerGold[i];

            int age = d.villagerAges[i];

            // ── 有法条门槛的（「你写的法律 → 村里出现了这种人」）──
            if (Law(d, 7)  && d.villagerFear[i]   > Cfg.ProfSinnerFear)     Grant(d, i, Mark.Sinner);
            if (Law(d, 2)  && d.villagerHunger[i] > Cfg.ProfBanditHunger)   Grant(d, i, Mark.Bandit);

            if (Law(d, 15) && age >= Cfg.ProfDeserterMinAge && age <= Cfg.ProfDeserterMaxAge
                           && d.villagerFear[i] > Cfg.ProfDeserterFear)     Grant(d, i, Mark.Deserter);

            if (Law(d, 11) && age >= Cfg.ProfWornAge
                           && d.villagerHunger[i] > Cfg.ProfWornHunger)     Grant(d, i, Mark.Worn);

            // 债户 = 曾经手里有钱、现在一分不剩（装买粮/丁税 → 「破产」是这两条法造出来的）
            if ((Law(d, 26) || Law(d, 27)) && age >= Cfg.ProfDebtorAge
                           && d.villagerGold[i] <= 0
                           && d.villagerWealthPeak[i] >= Cfg.ProfDebtorPeak)   Grant(d, i, Mark.Debtor);

            if (Law(d, 35) && d.villagerFear[i] > Cfg.ProfShamanFear)       Grant(d, i, Mark.Shaman);

            // ── 无门槛的（纯人生结局）──
            if (age >= Cfg.ProfLastHeirAge && d.villagerChildren[i] > 0
                                           && d.villagerChildrenAlive[i] <= 0) Grant(d, i, Mark.LastHeir);
            if (age >= Cfg.ProfBachelorAge && d.villagerChildren[i] <= 0)   Grant(d, i, Mark.Bachelor);
            if (d.villagerHungryTicks[i]   >= Cfg.ProfBitterTicks)          Grant(d, i, Mark.Bitter);
            if (d.villagerNeighborDeaths[i] >= Cfg.ProfMournerDeaths)       Grant(d, i, Mark.Mourner);
            if (totalDonations >= Cfg.ProfPatronDonations
                              && d.villagerDonations[i] >= 1)                Grant(d, i, Mark.Patron);
        }

        // ── 屠者：村里只要处决过人，就有一个「执刑者」──
        //   给**最不害怕的成年人**（并列取槽位最小）。
        //   ★ 同一时刻只保留一个人 —— 执刑的是同一个刽子手；
        //     他死了，下一个最不害怕的人才接刀。这样屠者永远不会烂大街。
        if (d.executions > 0 && CountMark(d, Mark.Butcher) < Cfg.ProfButcherMax)
        {
            int pick = -1, best = int.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (!d.villagerAlive[i]) continue;
                if (d.villagerAges[i] < 18) continue;
                if ((d.villagerMarks[i] & Mark.Butcher) != 0) continue;
                if (d.villagerFear[i] < best) { best = d.villagerFear[i]; pick = i; }
            }
            if (pick >= 0) Grant(d, pick, Mark.Butcher);
        }
    }

    // ══════════════════ 工具 ══════════════════

    static bool Law(VillageSaveData d, int id)
    {
        if (d.lawIds == null) return false;
        for (int k = 0; k < d.lawIds.Length; k++) if (d.lawIds[k] == id) return true;
        return false;
    }

    static int CountMark(VillageSaveData d, uint bit)
    {
        int c = 0;
        for (int i = 0; i < d.villagerAlive.Length; i++)
            if (d.villagerAlive[i] && (d.villagerMarks[i] & bit) != 0) c++;
        return c;
    }

    static void Grant(VillageSaveData d, int i, uint bit)
    {
        if (i < 0 || i >= d.villagerMarks.Length) return;
        if ((d.villagerMarks[i] & bit) != 0) return;
        Traits.AddMark(d, i, bit);
        Cfg.NoteGrant(bit, d.tick);
    }
}
