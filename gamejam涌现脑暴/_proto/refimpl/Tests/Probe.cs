// 复现 T05_05 的崩溃：56 组合 × N seed
using System;
using System.Text;
namespace SimLit
{
    public static class Probe
    {
        static int[][] AllLawsets()
        {
            var list = new System.Collections.Generic.List<int[]>();
            int P = World.PRESETS_COUNT;
            for (int a = 0; a < P; a++)
                for (int b = a + 1; b < P; b++)
                    for (int c = b + 1; c < P; c++)
                        for (int d = c + 1; d < P; d++)
                            for (int e = d + 1; e < P; e++)
                                list.Add(new[] { a, b, c, d, e });
            return list.ToArray();
        }
        public static string Run()
        {
            var sb = new StringBuilder();
            var lawsets = AllLawsets();
            const int SEEDS = 16;
            Console.WriteLine($"lawsets={lawsets.Length}");
            for (int ci = 0; ci < lawsets.Length; ci++)
            {
                double sum = 0;
                for (int si = 0; si < SEEDS; si++)
                {
                    var w = new World((ulong)(si * 101 + 3), lawsets[ci]);
                    for (int t = 0; t < World.MAXT; t++) { w.Tick(); if (w.AliveCount() == 0) break; }
                    sum += w.AliveCount();
                }
                Console.WriteLine($"  combo {ci,2} [{string.Join(",", lawsets[ci])}] alive={sum / SEEDS:F2}");
            }
            sb.AppendLine("完成，无崩溃");
            return sb.ToString();
        }
    }
}
