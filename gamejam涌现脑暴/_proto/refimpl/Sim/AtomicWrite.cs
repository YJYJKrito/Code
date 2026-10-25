// ═══════════════════════════════════════════════════════════════════
// Sim/AtomicWrite.cs
//
// 【修 Bug 3 ★严重★】原设计的原子写是 `File.Replace(tmp, x, x.bak)`。
//   File.Replace 要求 destination 必须已存在，实测：
//     首次保存（目标不存在）→ 抛 FileNotFoundException: Unable to find the specified file.
//   如果没被 try/catch 包住，就是「新玩家第一次退出就崩」。
//
//   本实现：目标不存在时退化为 File.Move（同卷上原子），存在时才 File.Replace。
//   并且任何一次写盘都保证「要么是新的完整档，要么是旧的完整档」，绝不出现半截文件。
// ═══════════════════════════════════════════════════════════════════
using System;
using System.IO;

namespace SimLit
{
    public static class AtomicWrite
    {
        /// <summary>原子写：写 tmp → 替换目标（目标不存在时用 Move）。</summary>
        public static void Write(string path, string content)
        {
            string tmp = path + ".tmp";
            string bak = path + ".bak";

            // 1. 先落 tmp（这一步失败不影响原档）
            File.WriteAllText(tmp, content);

            // 2. 替换
            if (!File.Exists(path))
            {
                // ★ 首次保存：File.Replace 会抛 FileNotFoundException
                File.Move(tmp, path);
                return;
            }

            try
            {
                File.Replace(tmp, path, bak);
            }
            catch (IOException)
            {
                // 某些文件系统/权限下 Replace 不可用 —— 退化为「备份 + 覆盖」
                try { if (File.Exists(bak)) File.Delete(bak); } catch { /* 备份失败不阻塞主流程 */ }
                try { File.Copy(path, bak, true); } catch { /* 同上 */ }
                File.Delete(path);
                File.Move(tmp, path);
            }
        }

        /// <summary>读档：主档坏了读备份；都坏了返回 null（由调用方走分级兜底）。</summary>
        public static string Read(string path)
        {
            if (File.Exists(path))
            {
                try { return File.ReadAllText(path); } catch { /* 落到备份 */ }
            }
            string bak = path + ".bak";
            if (File.Exists(bak))
            {
                try { return File.ReadAllText(bak); } catch { /* 真的没了 */ }
            }
            return null;
        }
    }
}
