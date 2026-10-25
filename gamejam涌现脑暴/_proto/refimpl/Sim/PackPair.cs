// ═══════════════════════════════════════════════════════════════════
// Sim/PackPair.cs
//
// 【修 Bug 1 ★致命★】原设计用朴素的 a*N+b 打包有序对，而 NumTargets 预留的是
//   压缩后的 N*(N-1) 个关系槽。a*N+b 的取值范围是 [0, N²) 扣掉对角线，最大到 N²-2，
//   比 N(N-1)-1 大了整整 N-1。实测：
//     N=12: 合法下标 0..144，朴素最大 155 @(11,10) → 越界
//     N=20: 合法下标 0..400，朴素最大 419 @(19,18) → 越界
//   N≥3 就崩，N=20 时最多溢出 19 个下标。
//
// 本实现用正确的双射：把有序对 (a,b)（a≠b）映射到 [0, N*(N-1))。
// ═══════════════════════════════════════════════════════════════════
namespace SimLit
{
    public static class Targets
    {
        /// <summary>目标总数 = 1(村庄) + N(村民) + N*(N-1)(有序关系对)</summary>
        public static int Count(int n) => 1 + n + n * (n - 1);

        /// <summary>有序对 (a,b) → [0, N*(N-1))，要求 a != b。</summary>
        public static int PackPair(int a, int b, int n)
        {
            int m = n - 1;
            return a * m + (b < a ? b : b - 1);
        }

        /// <summary>槽位索引。t1 &lt; 0 表示目标是单个村民。</summary>
        public static int Slot(int t0, int t1, int n)
        {
            if (t1 < 0) return 1 + t0;
            return 1 + n + PackPair(t0, t1, n);
        }
    }
}
