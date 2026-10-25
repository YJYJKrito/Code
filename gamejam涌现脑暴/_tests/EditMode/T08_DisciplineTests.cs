// ═══════════════════════════════════════════════════════════════════
// T08 · 纪律（源码静态审计）
//
// 【这一批为什么存在】技术验证报告的结论是：
//   **AI 能写"算法"，不能写"纪律"。** 它会把 Dictionary 用进模拟、
//   会用 float 算百分比、会调 UnityEngine.Random、会拿 struct.GetHashCode() 做哈希 ——
//   而其中**只有一部分会导致编译错误**，其余全是静默的行为退化。
//
//   Unity 侧第一道防线是 asmdef 的 noEngineReferences：任何 UnityEngine 泄漏直接编译不过。
//   但它挡不住 float、System.Random、容器迭代顺序 —— 那些只能靠这一层查。
//
// 【它是源码文本扫描，不是反射】因为要抓的是"写了但没跑到"的代码。
//   扫描前会剥掉注释 —— 本项目注释里大量出现 float/double 这类词（就是在解释为什么禁用它们）。
// ═══════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace SimLit.Tests
{
    [TestFixture]
    [Category("Fast")]
    public class T08_DisciplineTests
    {
        static string _simDir;

        [OneTimeSetUp]
        public void LocateSimSources()
        {
            _simDir = FindSimDir();
        }

        /// <summary>从测试程序集所在目录向上找含 World.cs 的 Sim 目录。</summary>
        static string FindSimDir()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            for (int up = 0; up < 12 && dir != null; up++, dir = dir.Parent)
            {
                // 候选：Unity 工程的 Assets/Sim、独立工程根下的 Sim、以及本仓库的原型布局
                string[] candidates =
                {
                    Path.Combine(dir.FullName, "Sim"),
                    Path.Combine(dir.FullName, "Assets", "Sim"),
                    Path.Combine(dir.FullName, "_proto", "refimpl", "Sim"),
                };
                foreach (var guess in candidates)
                    if (Directory.Exists(guess) && File.Exists(Path.Combine(guess, "World.cs"))) return guess;
            }
            return null;
        }

        static string StripComments(string s)
        {
            var sb = new StringBuilder(s.Length);
            bool line = false, block = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i], n = i + 1 < s.Length ? s[i + 1] : '\0';
                if (line) { if (c == '\n') { line = false; sb.Append(c); } continue; }
                if (block) { if (c == '*' && n == '/') { block = false; i++; } continue; }
                if (c == '/' && n == '/') { line = true; continue; }
                if (c == '/' && n == '*') { block = true; i++; continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        static IEnumerable<string> SimCodeFiles()
        {
            if (_simDir == null) yield break;
            foreach (var f in Directory.GetFiles(_simDir, "*.cs", SearchOption.AllDirectories))
                yield return f;
        }

        [Test]
        public void T08_01_Sim层禁用浮点()
        {
            if (_simDir == null) Assert.Ignore("没找到 Sim 源码目录（Unity 侧由 asmdef 承担，可忽略本测试）");

            var offenders = new List<string>();
            foreach (var f in SimCodeFiles())
            {
                string code = StripComments(File.ReadAllText(f));
                string[] banned = { "float ", "float.", "double ", "Mathf", "Math.Sqrt", "Math.Pow", "Math.Round", "MathF" };
                foreach (var b in banned)
                    if (code.Contains(b)) offenders.Add($"{Path.GetFileName(f)}: 出现 `{b}`");
            }
            Assert.IsEmpty(offenders,
                "Sim 层出现浮点 —— 浮点是不可跨端复现的（x86 与 ARM64 对 float→int 越界转换的规范行为不同）：\n  "
                + string.Join("\n  ", offenders) +
                "\n用整数百分数（×100 或 ×1000）代替，所有除法都要显式定义截断方向。");
        }

        [Test]
        public void T08_02_Sim层禁用UnityEngine()
        {
            if (_simDir == null) Assert.Ignore("没找到 Sim 源码目录");

            var offenders = new List<string>();
            foreach (var f in SimCodeFiles())
            {
                string code = StripComments(File.ReadAllText(f));
                if (code.Contains("UnityEngine")) offenders.Add(Path.GetFileName(f));
            }
            Assert.IsEmpty(offenders,
                "Sim 层引用了 UnityEngine —— 这一层必须能脱离引擎跑（headless 批跑、确定性验证都靠它）：\n  "
                + string.Join("\n  ", offenders) +
                "\nUnity 侧应当在 asmdef 上勾 noEngineReferences，让这类泄漏直接编译不过。");
        }

        [Test]
        public void T08_03_禁用非确定性随机源()
        {
            if (_simDir == null) Assert.Ignore("没找到 Sim 源码目录");

            var offenders = new List<string>();
            foreach (var f in SimCodeFiles())
            {
                string code = StripComments(File.ReadAllText(f));
                string[] banned = { "System.Random", "UnityEngine.Random", "Guid.NewGuid", "DateTime.Now",
                                    "DateTime.UtcNow", "Environment.TickCount", "Random.Range" };
                foreach (var b in banned)
                    if (code.Contains(b)) offenders.Add($"{Path.GetFileName(f)}: 出现 `{b}`");
            }
            Assert.IsEmpty(offenders,
                "Sim 层用了非确定性随机/时间源 —— 同 seed 复现会当场破掉：\n  " + string.Join("\n  ", offenders) +
                "\n只能走 Rng.For / Rng.Range / Rng.Chance（唯一入口）。");
        }

        [Test]
        public void T08_04_禁用字典与哈希集的迭代()
        {
            if (_simDir == null) Assert.Ignore("没找到 Sim 源码目录");

            var offenders = new List<string>();
            foreach (var f in SimCodeFiles())
            {
                string code = StripComments(File.ReadAllText(f));
                // Dictionary/HashSet 的枚举顺序在不同运行时/不同版本上不保证一致
                if (code.Contains("Dictionary<")) offenders.Add($"{Path.GetFileName(f)}: Dictionary<>");
                if (code.Contains("HashSet<")) offenders.Add($"{Path.GetFileName(f)}: HashSet<>");
                if (code.Contains("foreach") && code.Contains("List<")) { /* List 迭代是确定的，允许 */ }
            }
            Assert.IsEmpty(offenders,
                "Sim 层用了 Dictionary / HashSet —— 它们的枚举顺序不保证跨运行时一致，" +
                "是「同 seed 双端哈希不同」最难查的一类根因：\n  " + string.Join("\n  ", offenders) +
                "\n换成定长数组或 List<T>，并且排序一律给全序比较器。");
        }

        [Test]
        public void T08_05_禁用struct的GetHashCode()
        {
            if (_simDir == null) Assert.Ignore("没找到 Sim 源码目录");

            var offenders = new List<string>();
            foreach (var f in SimCodeFiles())
            {
                string code = StripComments(File.ReadAllText(f));
                if (code.Contains(".GetHashCode()")) offenders.Add(Path.GetFileName(f));
            }
            Assert.IsEmpty(offenders,
                "Sim 层调用了 .GetHashCode() —— struct 的默认实现（尤其含 string/引用字段时）" +
                "在 Mono 与 CoreCLR 上不同，且 .NET 的字符串哈希有每进程随机盐：" + string.Join(", ", offenders) +
                "\n哈希一律用 Rng.Mix64 显式逐字段混合。");
        }
    }
}
