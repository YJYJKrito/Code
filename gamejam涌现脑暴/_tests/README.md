# `_tests/` —— 《立法村》程序测试集

**60 条，全绿。** 详细说明见上级目录的 [`测试集-程序.md`](../测试集-程序.md)。

---

## 跑起来

```bash
cd _tests
dotnet test
```

约 15 秒。会编译 `../_proto/refimpl/Sim/`（纯确定性层）与 `../_proto/refimpl/Diag/`（统计层），
**不复制文件** —— 你改了 `Sim/` 里的东西，测试立刻反映出来。

只跑快的：

```bash
dotnet test (加 --filter 参数) Category=Fast
```

---

## 拷进 Unity

| 源 | 目标 |
|---|---|
| `EditMode/` 整个目录 | `Assets/Tests/EditMode/` |
| `PlayMode/SmokeTests.cs` | `Assets/Tests/PlayMode/` |
| `SimTests.csproj` | **不要拷**（那是脱机跑法用的） |

EditMode 的 `.cs` **一个字都不用改** —— Unity Test Framework 内置的也是 NUnit 3。
需要的 asmdef 配置见 `测试集-程序.md` §2.2。

---

## 目录

```
_test/
├─ SimTests.csproj              脱机跑法（NUnit + net8.0，LangVersion 9 对齐 Unity 2022.3）
├─ EditMode/                    可直接拷进 Unity
│   ├─ SimTestBase.cs           基类：static 快照/还原、常用跑法、56 种律法组合
│   ├─ T01_RngTests.cs          确定性随机（含 Bug 2 回归）
│   ├─ T02_TargetsAndTagsTests.cs  打包双射（Bug 1）+ 标签位图（Bug 4）
│   ├─ T03_DeterminismTests.cs  确定性 + 字段绊线
│   ├─ T04_SaveAndLawTests.cs   存档（Bug 3）+ 律法属主（Bug 5）
│   ├─ T05_EmergenceTests.cs    ★ 涌现（主题的守护测试）
│   ├─ T06_ContentTests.cs      死信与内容完整性
│   ├─ T07_PerfTests.cs         GC 与耗时
│   └─ T08_DisciplineTests.cs   源码静态审计（禁浮点/禁字典/禁非确定性随机）
└─ PlayMode/
    └─ SmokeTests.cs            真机冒烟（Unity 专用，脱机不可跑）
```

---

## 两条使用纪律

1. **一次只跑一个 dotnet 命令。** 同目录并发跑 `build` 与 `test`/`run` 会让进程随机崩溃
   （`AccessViolationException`，栈指向 `World.Tick`，崩点每次漂移）。
   详见 `测试集-程序.md` §7。

2. **红了先查表再改代码。** `测试集-程序.md` §4 有"红的测试 → 最可能的真因"对照表。
   大部分红意味着**设计退化了**，不是代码写错了 —— 改代码前先确认哪一头是对的。

---

## 加新测试

继承 `SimTestBase`（它负责快照/还原 `World` 的 static 标定参数，不继承会静默污染其他测试）。
规矩见 `测试集-程序.md` §8。
