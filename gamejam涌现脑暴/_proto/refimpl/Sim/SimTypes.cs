// ═══════════════════════════════════════════════════════════════════
// Sim/SimTypes.cs
//
// 【修 Bug 5】原设计里 LawSlot.hitsTotal 标了 [哈希][存档]，
//   LawRuntime.hitsTotal 也标了 [哈希]，而疲劳逻辑读的是后者。
//   两个字段一旦漂移，哈希（读 LawSlot）和玩法（读 LawRuntime）说的不是一回事，
//   而且不会报错，只会让重放静默分叉。
//   本实现：hitsTotal 只有一个主（LawSlot），LawRuntime 降级为本 tick 的只读视图，不进哈希。
//
// 【确定性】全部显式底层类型；禁 float/double；禁属性（存档只认字段）。
// ═══════════════════════════════════════════════════════════════════
namespace SimLit
{
    public enum LawState : byte { Empty = 0, Enacted = 1, Deliberating = 2, Voided = 3 }

    public static class LawFlag
    {
        public const byte Constitutional = 1 << 0;
        public const byte Temp           = 1 << 1;
        public const byte Fatigued       = 1 << 2;
        public const byte Suspended      = 1 << 3;
    }

    public struct Villager
    {
        public int  gold;      // 0..99 软回拉
        public int  hunger;    // 0..99，到 99 死
        public int  fear;      // 0..99 软回拉
        // 个人存粮。★ 存在的理由：公共粮仓是**全局同步器** —— 20 个人吃同一口锅，
        //   锅要么有粮要么没粮，于是所有人同时活或同时死，结局非 0 即 20（实测 0 人 50.5% / 20 人 32.7%）。
        //   个人存粮把「集体命运」拆成 20 条独立的时间线：同一天，有人还有余粮，有人已经断炊。
        //   注意守恒：公仓与个人存粮之间只发生**转移**，凭空不许生粮（否则配平失效）。
        public int  food;      // 0..FOOD_CAP
        public ushort tags;    // 见 TagBits
        public bool alive;
        public int  deathTick;
        // 历史计数 —— 事件卡门禁「读历史」用（这是卡→卡耦合的来源）
        public int  nRobbed, nFed, nTaxed, nProduced;
        // 本 tick 的临时值（不进哈希）：用于结算顺序
        public int  fearSrc;
    }

    public struct Law
    {
        public LawState state;
        public byte condType;      // 0..5
        public int  condParam;
        public byte effType;       // 0..8
        public int  effParam;
        public ushort tagArg;
        public byte flags;         // LawFlag
        // ★ hitsTotal 唯一的家（修 Bug 5）
        public int  hitsTotal;
        public int  pendingParam;  // 审议中的新值
        public int  deliberateEndTick;
        // 非模拟字段（存档但不进哈希）
        public int  delayAfterDeath;
    }

    /// <summary>本 tick 的只读视图，每 tick 重算，不进哈希、不进存档。</summary>
    public struct LawRuntimeView
    {
        public int hitsThisTick;
        public int lastHitTick;
        public int victimsTotal;
    }

    public struct SimState
    {
        public int  tick;
        public int  aliveCount, popPeak, sumFear, granary, authority;
        public bool suspendedAll;
        public bool fatigueArmed;
    }
}
