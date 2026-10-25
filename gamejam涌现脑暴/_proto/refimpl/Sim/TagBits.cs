// ═══════════════════════════════════════════════════════════════════
// Sim/TagBits.cs
//
// 【修 Bug 4】原设计写 `public const byte Coward = 1 << 0;` 共 8 位。
//   扩到 16 位后 `const byte X = 1 << 8;` 是 CS0031 编译错误（常量 256 无法转换为 byte），
//   且 `v.tags &= (byte)~(1 << bit)` 会把高 8 位整个抹掉。
//   本实现全部改 ushort，并把掩码运算集中在 ApplyRemoveTag 一处。
//
// 【原设计的位图冲突】旧文档里两套 8 位集合只共享「劫匪/逃兵」两个标签，
//   重映射后瘟疫链/饥荒链/军管链的门禁全部落空（卡永不触发，且不报错）。
//   本实现把布局固定为：
//     低 4 位 性格（可增删，卡可以改）
//     中 4 位 伤疤（律法打上，终身，RemoveTag 免疫）
//     高 8 位 行为标签（事件卡用）
// ═══════════════════════════════════════════════════════════════════
namespace SimLit
{
    public static class TagBits
    {
        // ── 低 4 位：性格（调制动力学，也是唯一的异质性来源）──
        public const ushort Coward   = 1 << 0;   // 怯懦：更容易恐惧
        public const ushort Greedy   = 1 << 1;   // 贪婪：更能攒钱
        public const ushort Diligent = 1 << 2;   // 勤勉：产出多、吃得省
        public const ushort Sickly   = 1 << 3;   // 病弱：产出少、饿得快

        // ── 中 4 位：伤疤（终身，废法不清）──
        public const ushort Bandit   = 1 << 4;
        public const ushort Debtor   = 1 << 5;
        public const ushort Deserter = 1 << 6;
        public const ushort Accused  = 1 << 7;

        // ── 高 8 位：行为标签（事件卡门禁用）──
        public const ushort Taxpayer = 1 << 8;
        public const ushort Believer = 1 << 9;
        public const ushort Starving = 1 << 10;
        public const ushort Guard    = 1 << 11;
        public const ushort Sinner   = 1 << 12;

        public const ushort TRAIT_MASK = Coward | Greedy | Diligent | Sickly;
        public const ushort SCAR_MASK  = Bandit | Debtor | Deserter | Accused;
        public const ushort BEHAV_MASK = Taxpayer | Believer | Starving | Guard | Sinner;

        /// <summary>伤疤免疫清除。这是「废法 = 止血，不是撤销」的唯一实现点。</summary>
        public static void ApplyRemoveTag(ref ushort tags, ushort bits)
        {
            // 高 8 位与低 4 位可清，中 4 位（伤疤）永不
            tags &= (ushort)~(bits & (ushort)(TRAIT_MASK | BEHAV_MASK));
        }

        public static string Describe(ushort t)
        {
            var sb = new System.Text.StringBuilder();
            void Add(ushort b, string n) { if ((t & b) != 0) sb.Append(n).Append(' '); }
            Add(Coward, "怯懦"); Add(Greedy, "贪婪"); Add(Diligent, "勤勉"); Add(Sickly, "病弱");
            Add(Bandit, "劫匪"); Add(Debtor, "负债"); Add(Deserter, "逃兵"); Add(Accused, "受审");
            Add(Taxpayer, "税民"); Add(Believer, "信徒"); Add(Starving, "饥民"); Add(Guard, "守卫"); Add(Sinner, "罪人");
            return sb.Length == 0 ? "（无）" : sb.ToString().TrimEnd();
        }
    }
}
