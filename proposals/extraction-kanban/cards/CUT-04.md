**2026-10-15 源码复核更正：** 下方“kill/wait 交接、超时升级全部通过”不能作为真实进程证据。`requirements/managed-session-lifecycle/tests/025.test.mjs` 的真实 TERM→等待→KILL→exit 仍为 GAP-133 TODO；受控端口及直接 Untrack 不等于物理退出。另已确认生产持久预留未接通、提前清账、同步完成丢失和 blocker 查询吞错。历史计数保留，后续见 [源码报告](../SOURCE-AUDIT-2026-10-15.md) 与 [CUT-04-R1](../BOARD.md#source-audit-repairs)。本轮只纠正文档，未修复实现或关闭 GAP。

## 验证
- 正控与反例：`requirements/work/tests/009.test.mjs`、`044.test.mjs`、`requirements/managed-session-lifecycle/tests/025.test.mjs`。
- 验证命令（交由 `devops` 执行）：
  - `node scripts/build.mjs`
  - `node --test requirements/work/tests/009.test.mjs requirements/work/tests/044.test.mjs requirements/managed-session-lifecycle/tests/025.test.mjs`
  - `node scripts/check.mjs`


### 验证记录（2026-10-15，执行人：固定 DevOps）——CUT-04 套件全绿

| 步骤 | 命令 | 结果 | 关键输出 |
|---|---|---|---|
| 构建 | `node scripts/build.mjs` | ✅ exit 0 | 构建转绿前有一处普通缺陷修复（见下） |
| 静态门禁 | `npm run check` | ✅ exit 0 | tsc 与静态门禁零错误 |
| 套件 | `node --test requirements/work/tests/009.test.mjs requirements/work/tests/044.test.mjs requirements/managed-session-lifecycle/tests/025.test.mjs` | ✅ exit 0 | 11 tests：10 pass、0 fail、1 todo（预先声明的 GAP-133） |

绿的实据：work-009（Reserve→Started 幂等、Cancel/Drain 保留 blocker、物理 Ended 清障）、work-044（Relayed 保留固定 DevOps、blockers 排除固定 DevOps PTY）、managed-session-lifecycle-025（kill/wait 交接、超时升级、DevOps run 终态排空 PTY、ToolRuntimeScope 生产接线排空）全部通过。

构建侧修复（devops 自修，一处 upcast）：`Host/Runtime.fs:632` `PtyRuns` 实现显式转换为 `.fsi` 声明的 `ICollection<string>`——eng-cut04 交回的 `ptyLeases.Keys` 形态与签名不匹配曾稳定红（error 34）。修复后同一命令四轮复验转绿，卡内 `HostForkRuntime` 的 PtyRuns 现为 `ICollection<string>` 与本卡叙述一致。

git 未 commit；本卡验收（构建零错、check 零错、009/044/025 全绿）在工作树上成立。
