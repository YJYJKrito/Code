// ═══════════════════════════════════════════════════════════════════
// Sim/Rng.cs —— 确定性随机（唯一入口）
//
// 【修 Bug 2】原设计 RngFor(seed, tick, channel) 没有 seq，
//   同一 tick 内多次调用返回完全相同的 roll（实测：8 个优先级段只产出 1 个不同值），
//   导致抽卡在各段之间完全相关，加权随机退化成"高权重卡被系统性偏好"。
//   本实现加 seq 参数：同一 tick 内第几次掷骰子。
//
// 【纪律】channel 号有语义，永不被当计数器消耗。
//   seq 才是计数器。两者不可互相顶替。
//
// 【确定性】纯函数 + 全 unchecked + 只用 ulong 做位运算。
//   绝不用 int 做 >>（有符号右移是算术右移，与 JS 的 >>> 不同，这是 PRNG 的第一号错误来源）。
// ═══════════════════════════════════════════════════════════════════
namespace SimLit
{
    public static class Rng
    {
        // splitmix64 混合器：同一输入永远同一输出，无状态、无副作用
        public static ulong Mix64(ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }

        // ── 通道表（有语义，永不复用，永不作计数器）──
        public const int CH_DEAL         = 1;   // 抽卡
        public const int CH_WOBBLE       = 2;   // 权重微扰
        public const int CH_EPILOG       = 3;   // 讣告选词（表现层）
        public const int CH_AUTH         = 4;   // 卡给威信充值
        public const int CH_CONSTITUTION = 5;   // 立宪候选
        public const int CH_PRESENTATION = 6;   // 表现层抖动（不进哈希）
        public const int CH_VILLAGER     = 7;   // 村民个体随机（生产/消耗/恐惧）

        /// <summary>同一 (seed, tick, channel, seq) 永远返回同一个值。</summary>
        public static ulong For(ulong seed, int tick, int channel, int seq)
        {
            unchecked
            {
                ulong h = Mix64(seed ^ ((ulong)(uint)tick * 0x100000001B3UL));
                h = Mix64(h ^ ((ulong)(uint)channel << 48) ^ ((ulong)(uint)seq << 16));
                return h;
            }
        }

        /// <summary>无符号取模区间 [minInc, maxExc)。span&lt;=0 时返回 minInc（防 %0 崩）。</summary>
        public static int Range(ulong seed, int tick, int channel, int seq, int minInc, int maxExc)
        {
            int span = maxExc - minInc;
            if (span <= 0) return minInc;
            return minInc + (int)(For(seed, tick, channel, seq) % (ulong)span);
        }

        /// <summary>百分比判定，pct ∈ [0,100]。</summary>
        public static bool Chance(ulong seed, int tick, int channel, int seq, int pct)
        {
            if (pct <= 0) return false;
            if (pct >= 100) return true;
            return Range(seed, tick, channel, seq, 0, 100) < pct;
        }
    }
}
